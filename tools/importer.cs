using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

return await ImporterProgram.RunAsync(args);

static class ImporterProgram
{
    const string AndroidReference = "https://developer.android.com/reference/";
    const string JavaReference = "https://docs.oracle.com/en/java/javase/21/docs/api/";
    const string UserAgent = "dotnet-android-api-docs-importer/1.0 (+https://github.com/dotnet/android-api-docs)";
    const int MaximumDownloadBytes = 12 * 1024 * 1024;
    const string AndroidAttribution =
        "Portions of this page are modifications based on work created and shared by the " +
        "<format type=\"text/html\"><a href=\"https://developers.google.com/terms/site-policies\" " +
        "title=\"Android Open Source Project\">Android Open Source Project</a></format> and used " +
        "according to terms described in the <format type=\"text/html\"><a " +
        "href=\"https://creativecommons.org/licenses/by/2.5/\" " +
        "title=\"Creative Commons 2.5 Attribution License\">Creative Commons 2.5 Attribution License." +
        "</a></format>";

    public static async Task<int> RunAsync(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine($"ERROR: {error.Message}");
            Options.PrintHelp();
            return 2;
        }

        if (options.Help)
        {
            Options.PrintHelp();
            return 0;
        }

        var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
        if (repositoryRoot is null)
        {
            Console.Error.WriteLine("ERROR: Could not find the android-api-docs repository root.");
            return 2;
        }

        if (options.SelfTest)
            return RunSelfTest(repositoryRoot);

        var report = new ImportReport
        {
            Mode = options.Apply ? "apply" : "dry-run",
            Offline = options.Offline,
            MaxChanges = options.MaxChanges,
        };

        try
        {
            ValidateScope(options);
            var docsRoot = Path.Combine(repositoryRoot, "docs", "xml");
            var files = SelectFiles(repositoryRoot, docsRoot, options);
            report.FilesScanned = files.Count;

            var loadedFiles = new List<LoadedFile>();
            foreach (var path in files)
            {
                try
                {
                    var file = LoadedFile.Load(repositoryRoot, path);
                    if (!MatchesNamespace(file.Root, options.Namespace))
                        continue;
                    file.SelectOwners(options.Member);
                    loadedFiles.Add(file);
                }
                catch (Exception error) when (error is XmlException or IOException or UnauthorizedAccessException)
                {
                    report.Entries.Add(ReportEntry.Error(
                        Relative(repositoryRoot, path), "", "", "malformed_xml", error.Message));
                }
            }

            var sourceRequests = loadedFiles
                .SelectMany(file => file.Owners)
                .Where(owner =>
                    owner.Placeholders.Count > 0 ||
                    IsEnumSummaryRepairCandidate(owner))
                .Select(owner => owner.SourceRequest)
                .Where(request => request is not null)
                .Cast<SourceRequest>()
                .DistinctBy(request => request.Url, StringComparer.Ordinal)
                .OrderBy(request => request.Url, StringComparer.Ordinal)
                .ToList();

            var cacheDirectory = options.CacheDirectory is null
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "dotnet-android-api-doc-importer",
                    "cache")
                : ResolvePath(repositoryRoot, options.CacheDirectory);

            using var fetcher = new SourceFetcher(
                cacheDirectory,
                options.Offline,
                options.Concurrency,
                options.Retries);
            var fetchedPages = await fetcher.FetchAsync(sourceRequests);
            var pages = new SortedDictionary<string, SourceLoadResult>(StringComparer.Ordinal);
            foreach (var request in sourceRequests)
            {
                var fetched = fetchedPages[request.Url];
                if (fetched.Error is not null)
                {
                    pages[request.Url] = SourceLoadResult.Failure(fetched.Reason!, fetched.Error);
                    continue;
                }
                try
                {
                    pages[request.Url] = SourceLoadResult.Success(
                        SourcePage.Parse(request, fetched.Content!));
                }
                catch (Exception error) when (
                    error is ArgumentException or FormatException or InvalidOperationException)
                {
                    pages[request.Url] = SourceLoadResult.Failure(
                        "source_parse_error",
                        $"Could not parse the official page {request.Url}: {error.Message}");
                }
            }

            var remaining = options.MaxChanges;
            var changedFiles = new List<(LoadedFile File, string Text)>();
            foreach (var file in loadedFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            {
                var text = file.Text;
                var fileChanged = false;
                foreach (var owner in file.Owners.OrderBy(item => item.Order))
                {
                    var ownerChanged = false;
                    var mapping = MapOwner(owner, pages);
                    if (ReportMappingFailure(report, file, owner, mapping))
                        continue;

                    foreach (var placeholder in owner.Placeholders.OrderBy(item => item.Order))
                    {
                        var replacement = ReplacementFor(
                            placeholder,
                            mapping.Docs!,
                            owner.IsEnumField);
                        if (replacement.Text is null)
                        {
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                placeholder.Target,
                                replacement.Reason!,
                                replacement.Detail,
                                mapping.SourceUrl));
                            continue;
                        }

                        if (remaining == 0)
                        {
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                placeholder.Target,
                                "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                            continue;
                        }

                        if (!TryReplacePlaceholder(
                            text,
                            file.DocsBlocks[owner.Order],
                            placeholder,
                            replacement.Text,
                            out var replacedText,
                            out var replacementError))
                        {
                            report.Entries.Add(ReportEntry.Error(
                                file.RelativePath,
                                owner.Id,
                                placeholder.Target,
                                "source_xml_layout_mismatch",
                                replacementError,
                                mapping.SourceUrl));
                            continue;
                        }

                        text = replacedText;
                        file.UpdateBlockOffsets(owner.Order, text);
                        fileChanged = true;
                        ownerChanged = true;
                        remaining--;
                        report.Entries.Add(ReportEntry.Changed(
                            "would_apply",
                            file.RelativePath,
                            owner.Id,
                            placeholder.Target,
                            mapping.SourceUrl));
                    }

                    if (!ownerChanged &&
                        mapping.Docs is not null &&
                        IsEnumSummaryRepairCandidate(owner))
                    {
                        var refreshed = AddSourceDocumentationIfSafe(
                            text,
                            file,
                            owner,
                            mapping.Docs,
                            allowEnumCreation: false);
                        if (!refreshed.Equals(text, StringComparison.Ordinal))
                        {
                            if (remaining == 0)
                            {
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    "summary",
                                    "max_changes_reached",
                                    $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl));
                            }
                            else
                            {
                                text = refreshed;
                                file.UpdateBlockOffsets(owner.Order, text);
                                fileChanged = true;
                                ownerChanged = true;
                                remaining--;
                                report.Entries.Add(ReportEntry.Changed(
                                    "would_apply",
                                    file.RelativePath,
                                    owner.Id,
                                    "summary",
                                    mapping.SourceUrl));
                            }
                        }
                    }

                    if (ownerChanged && mapping.Docs is not null)
                    {
                        text = AddSourceDocumentationIfSafe(text, file, owner, mapping.Docs);
                        file.UpdateBlockOffsets(owner.Order, text);
                    }
                }

                if (!fileChanged)
                    continue;

                try
                {
                    _ = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
                    changedFiles.Add((file, text));
                }
                catch (XmlException error)
                {
                    report.Entries.Add(ReportEntry.Error(
                        file.RelativePath, "", "", "generated_xml_invalid", error.Message));
                }
            }

            report.SourcesFetched = fetcher.NetworkFetches;
            report.SourcesFromCache = fetcher.CacheHits;
            if (options.Apply)
            {
                report.FilesChanged = 0;
                if (!report.Entries.Any(entry => entry.Status == "error"))
                    ApplyChangedFiles(changedFiles, report);
            }
            else
            {
                report.FilesChanged = changedFiles.Count;
            }
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            report.Entries.Add(ReportEntry.Error("", "", "", "fatal", error.Message));
        }

        report.SortAndCount();
        var humanReport = report.ToHumanText();
        Console.Write(humanReport);
        if (options.ReportPath is not null)
        {
            try
            {
                WriteReports(repositoryRoot, options.ReportPath, report, humanReport);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"ERROR: Could not write reports: {error.Message}");
                return 1;
            }
        }

        return report.ErrorCount == 0 ? 0 : 1;
    }

    static void ValidateScope(Options options)
    {
        if (options.Paths.Count == 0 && options.Namespace is null && options.Member is null)
            throw new ArgumentException(
                "Specify at least one --path, --namespace, or --member filter. Unscoped repository scans are disabled.");
        if (options.Apply && options.Paths.Count == 0 && options.Namespace is null)
            throw new ArgumentException(
                "--apply requires a --path or --namespace write scope; --member alone is not sufficient.");
    }

    static string? FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
        }
        return null;
    }

    static List<string> SelectFiles(string repositoryRoot, string docsRoot, Options options)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var docsFullPath = Path.GetFullPath(docsRoot);
        var docsPrefix = docsFullPath + Path.DirectorySeparatorChar;
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        if (options.Paths.Count == 0)
        {
            foreach (var file in Directory.EnumerateFiles(docsRoot, "*.xml", SearchOption.AllDirectories))
                paths.Add(Path.GetFullPath(file));
        }
        else
        {
            foreach (var value in options.Paths)
            {
                var path = ResolvePath(repositoryRoot, value);
                if (!path.Equals(docsFullPath, comparer) && !path.StartsWith(docsPrefix, comparer))
                    throw new ArgumentException($"--path must be under docs/xml: {value}");
                if (File.Exists(path))
                {
                    paths.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*.xml", SearchOption.AllDirectories))
                        paths.Add(Path.GetFullPath(file));
                }
                else
                {
                    throw new ArgumentException($"--path does not exist: {value}");
                }
            }
        }

        var selected = paths
            .Where(path => path.StartsWith(docsPrefix, comparer))
            .Where(path => !path.Equals(Path.Combine(docsRoot, "index.xml"), comparer))
            .Where(path => Path.GetExtension(path).Equals(".xml", comparer))
            .ToList();
        if (options.Paths.Count > 0 && selected.Count == 0)
            throw new ArgumentException("No --path selections resolved to XML files under docs/xml.");
        return selected;
    }

    static bool MatchesNamespace(XElement root, string? filter)
    {
        if (filter is null)
            return true;
        var fullName = (string?)root.Attribute("FullName") ?? "";
        return fullName.Equals(filter, StringComparison.Ordinal) ||
            fullName.StartsWith(filter + ".", StringComparison.Ordinal) ||
            fullName.StartsWith(filter + "+", StringComparison.Ordinal);
    }

    static MappingResult MapOwner(
        DocsOwner owner,
        IReadOnlyDictionary<string, SourceLoadResult> pages)
    {
        if (owner.SourceRequest is null)
            return MappingResult.Skip("missing_type_registration",
                "No supported Android or Java type registration was found.");
        if (!pages.TryGetValue(owner.SourceRequest.Url, out var loaded))
            return MappingResult.Skip(
                "source_not_loaded",
                "The official source page was not loaded.",
                owner.SourceRequest.Url);
        if (loaded.Error is not null)
            return MappingResult.Skip(loaded.Reason!, loaded.Error, owner.SourceRequest.Url);

        var page = loaded.Page!;
        if (owner.Member is null)
        {
            if (page.TypeDocs is null || string.IsNullOrWhiteSpace(page.TypeDocs.Summary))
                return MappingResult.Skip(
                    "type_documentation_missing",
                    "The official page did not contain a usable declared-type description.",
                    owner.SourceRequest.Url);
            return MappingResult.Success(page.TypeDocs);
        }

        var registration = Registration.Member(owner.Member);
        if (registration is null)
            return MappingResult.Skip(
                "missing_member_registration",
                "The managed member has no JNI registration; no name-based guess was attempted.",
                owner.SourceRequest.Url);

        if (registration.IsField)
        {
            var fields = page.Members
                .Where(member => member.IsField)
                .Where(member => member.Name.Equals(registration.Name, StringComparison.Ordinal))
                .ToList();
            if (fields.Count > 1)
                return MappingResult.Skip(
                    "ambiguous_exact_match",
                    $"The official page contained {fields.Count} exact field matches for {registration.Name}.",
                    owner.SourceRequest.Url);
            if (fields.Count == 0)
                return MappingResult.Skip(
                    "member_not_declared_on_source_page",
                    "No declared field detail section matched the registered Java field name.",
                    owner.SourceRequest.Url);
            var fieldDocs = fields[0].Docs;
            if (fieldDocs is null || string.IsNullOrWhiteSpace(fieldDocs.Summary))
                return MappingResult.Skip(
                    "source_documentation_empty",
                    "The exact source field had no usable prose.",
                    fields[0].Url);
            return MappingResult.Success(fieldDocs);
        }

        var expectedArguments = Descriptor.ParseArguments(registration.Descriptor!);
        if (expectedArguments is null)
            return MappingResult.Skip(
                "malformed_jni_signature",
                $"The JNI descriptor '{registration.Descriptor}' could not be parsed.",
                owner.SourceRequest.Url);

        var javaName = registration.Name == ".ctor"
            ? owner.SourceRequest.JavaPath.Split('/', '$').Last()
            : registration.Name;
        var named = page.Members
            .Where(member => MemberNameMatches(member, javaName, registration.Name == ".ctor"))
            .ToList();
        var exact = named
            .Where(member => member.ArgumentDescriptors is not null)
            .Where(member => member.ArgumentDescriptors!.SequenceEqual(expectedArguments, StringComparer.Ordinal))
            .ToList();

        if (exact.Count > 1)
            return MappingResult.Skip(
                "ambiguous_exact_match",
                $"The official page contained {exact.Count} exact matches for {registration.Name}{registration.Descriptor}.",
                owner.SourceRequest.Url);
        if (exact.Count == 0)
        {
            var reason = named.Count == 0
                ? "member_not_declared_on_source_page"
                : "overload_signature_mismatch";
            var detail = named.Count == 0
                ? "No declared detail section matched the registered Java member name; inherited-only members are not imported."
                : $"No declared overload exactly matched JNI descriptor {registration.Descriptor}.";
            return MappingResult.Skip(reason, detail, owner.SourceRequest.Url);
        }

        var docs = exact[0].Docs;
        if (docs is null || string.IsNullOrWhiteSpace(docs.Summary))
            return MappingResult.Skip(
                "source_documentation_empty",
                "The exact source member had no usable prose.",
                exact[0].Url);
        return MappingResult.Success(docs);
    }

    static bool ReportMappingFailure(
        ImportReport report,
        LoadedFile file,
        DocsOwner owner,
        MappingResult mapping)
    {
        if (mapping.ErrorReason is null)
            return false;

        foreach (var placeholder in owner.Placeholders)
        {
            report.Entries.Add(ReportEntry.Skipped(
                file.RelativePath,
                owner.Id,
                placeholder.Target,
                mapping.ErrorReason,
                mapping.Detail,
                mapping.SourceUrl));
        }
        if (IsEnumSummaryRepairCandidate(owner) &&
            !owner.Placeholders.Any(placeholder => placeholder.Name == "summary"))
        {
            report.Entries.Add(ReportEntry.Skipped(
                file.RelativePath,
                owner.Id,
                "summary",
                mapping.ErrorReason,
                mapping.Detail,
                mapping.SourceUrl));
        }
        return true;
    }

    static bool MemberNameMatches(SourceMember member, string name, bool constructor) =>
        constructor
            ? member.IsConstructor && (
                member.Name.Equals(name, StringComparison.Ordinal) ||
                member.Name.Equals("<init>", StringComparison.Ordinal))
            : !member.IsConstructor && member.Name.Equals(name, StringComparison.Ordinal);

    static Replacement ReplacementFor(
        Placeholder placeholder,
        SourceDocs docs,
        bool isEnumField = false)
    {
        if (isEnumField && placeholder.Name is "remarks" or "para")
            return Replacement.Skip(
                "enum_field_remarks_not_rendered",
                "Enum field remarks are not emitted by ECMA2Yaml; authoritative prose is imported into the summary.");

        return placeholder.Name switch
        {
            "summary" => ChannelValueOrSkip(
                isEnumField ? docs.Paragraphs.FirstOrDefault() : docs.Summary,
                "summary",
                "source_summary_missing"),
            "remarks" or "para" => ChannelValueOrSkip(
                docs.Paragraphs.FirstOrDefault(),
                "remarks",
                "source_remarks_missing"),
            "param" => docs.Parameters.TryGetValue(placeholder.Key, out var parameter)
                ? ChannelValueOrSkip(parameter, "param", "source_parameter_missing")
                : Replacement.Skip(
                    "source_parameter_missing",
                    $"The exact source member did not document parameter '{placeholder.Key}'."),
            "returns" or "value" => ChannelValueOrSkip(
                docs.Returns,
                placeholder.Name,
                "source_return_missing"),
            "exception" => ExceptionReplacement(placeholder, docs),
            _ => Replacement.Skip(
                "unsupported_placeholder_target",
                $"Placeholder element <{placeholder.Name}> is not imported."),
        };
    }

    static bool IsEnumSummaryRepairCandidate(DocsOwner owner)
    {
        if (!owner.IsEnumField ||
            owner.Docs.Element("summary") is not XElement summary ||
            owner.Member is null ||
            owner.SourceRequest is null ||
            Registration.Member(owner.Member) is not MemberRegistration registration ||
            !registration.IsField)
        {
            return false;
        }
        return IsEnumSummaryRepairCandidate(
            summary,
            owner.SourceRequest.Url + "#" + registration.Name,
            $"{owner.SourceRequest.JavaPath.Replace('/', '.').Replace('$', '.')}.{registration.Name}",
            owner.SourceRequest.Kind);
    }

    static bool IsEnumSummaryRepairCandidate(
        XElement summary,
        string sourceUrl,
        string sourceLabel,
        string sourceKind)
    {
        if (summary.HasAttributes ||
            summary.Nodes().Any(node => node switch
            {
                XElement => false,
                XText text => !string.IsNullOrWhiteSpace(text.Value),
                _ => true,
            }))
        {
            return false;
        }

        var paragraphs = summary.Elements().ToList();
        if (paragraphs.Count != 3 ||
            paragraphs.Any(paragraph =>
                paragraph.Name.LocalName != "para" || paragraph.HasAttributes))
        {
            return false;
        }

        var sourceName = sourceKind == "android" ? "Android" : "Java";
        var expectedSource = XElement.Parse(
            $"<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(sourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceName} reference for <code>{XmlEscape(sourceLabel)}</code>." +
            "</a></format></para>");
        var expectedAttribution = XElement.Parse($"<para>{AndroidAttribution}</para>");
        return !paragraphs[0].HasElements &&
            IsDeprecationParagraph(paragraphs[0].Value) &&
            XNode.DeepEquals(paragraphs[1], expectedSource) &&
            XNode.DeepEquals(paragraphs[2], expectedAttribution);
    }

    static Replacement ChannelValueOrSkip(string? value, string channel, string missingReason)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Replacement.Skip(
                missingReason,
                "The exact source member did not provide this documentation channel.");

        var cleaned = channel is "param" or "returns" or "value"
            ? RemoveLeadingJavaType(value)
            : CleanSourceText(value);
        if (!IsMeaningfulChannel(cleaned, channel))
            return Replacement.Skip(
                "source_channel_not_meaningful",
                $"The official {channel} text was only a type, nullability marker, cross-reference heading, or deprecation boilerplate.");
        return Replacement.Use(cleaned);
    }

    static string RemoveLeadingJavaType(string value)
    {
        var cleaned = CleanSourceText(value);
        return Regex.Replace(
            cleaned,
            @"^(?:[\w.$]+(?:<[^>]+>)?(?:\[\])?)\s*:\s*(?=\S)",
            "",
            RegexOptions.CultureInvariant).Trim();
    }

    static bool IsMeaningfulChannel(string value, string channel)
    {
        var normalized = NormalizeText(value).TrimEnd('.').Trim();
        if (normalized.Length == 0 ||
            normalized.Equals("See also:", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("See also", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(
                "Content and code samples on this page are subject to the licenses described in the Content License",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(
                "Java and OpenJDK are trademarks or registered trademarks of Oracle and/or its affiliates",
                StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(
                normalized,
                @"^Last updated \d{4}-\d{2}-\d{2} UTC$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            Regex.IsMatch(
                normalized,
                @"^Constant Value:\s*\S+(?:\s+\(\S+\))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;
        if (channel is "returns" or "value" or "param")
        {
            if (Regex.IsMatch(
                normalized,
                @"^(?:boolean|byte|char|double|float|int|long|short|void|[A-Z][\w$]*(?:<[^>]+>)?(?:\[\])?|[\w$]+(?:\.[\w$]+)+(?:<[^>]+>)?(?:\[\])?)$",
                RegexOptions.CultureInvariant))
                return false;
            if (Regex.IsMatch(
                normalized,
                @"^This value (?:cannot|can|may|must not) be null$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return false;
        }
        if (channel == "summary" && Regex.IsMatch(
            normalized,
            @"^This (?:constant|method|field|class|interface) (?:is|was) deprecated(?: in API level \d+)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;
        return true;
    }

    static Replacement ExceptionReplacement(Placeholder placeholder, SourceDocs docs)
    {
        var simpleName = placeholder.Key
            .Replace('+', '.')
            .Split('.')
            .LastOrDefault() ?? "";
        var matches = docs.Exceptions
            .Where(item => item.Key.Equals(simpleName, StringComparison.Ordinal) ||
                item.Key.EndsWith("." + simpleName, StringComparison.Ordinal))
            .Select(item => item.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return matches.Count switch
        {
            1 => Replacement.Use(matches[0]),
            > 1 => Replacement.Skip(
                "ambiguous_source_exception",
                $"Multiple source exceptions matched '{placeholder.Key}'."),
            _ => Replacement.Skip(
                "source_exception_missing",
                $"The exact source member did not document exception '{placeholder.Key}'."),
        };
    }

    static bool TryReplacePlaceholder(
        string text,
        DocsBlock block,
        Placeholder placeholder,
        string replacement,
        out string updated,
        out string error)
    {
        var blockText = text[block.Start..block.End];
        var attributeLookahead = placeholder.Name switch
        {
            "param" => $@"(?=[^>]*\bname\s*=\s*""{Regex.Escape(placeholder.Key)}"")",
            "exception" => $@"(?=[^>]*\bcref\s*=\s*""{Regex.Escape(placeholder.Key)}"")",
            _ => "",
        };
        var pattern =
            $@"(<{placeholder.Name}\b{attributeLookahead}[^>]*>)" +
            @"(?<value>\s*To be added\.?\s*)" +
            $@"(</{placeholder.Name}>)";
        var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var match = regex.Match(blockText);
        if (!match.Success)
        {
            updated = text;
            error = $"Could not locate the structurally identified {placeholder.Target} placeholder in its <Docs> block.";
            return false;
        }

        var escaped = XmlEscape(replacement);
        if (placeholder.Name == "remarks")
        {
            var newline = blockText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lineStart = blockText.LastIndexOf(newline, match.Index, StringComparison.Ordinal);
            lineStart = lineStart < 0 ? 0 : lineStart + newline.Length;
            var indent = blockText[lineStart..match.Index];
            if (string.IsNullOrWhiteSpace(indent))
            {
                escaped =
                    newline + indent + "  " + $"<para>{escaped}</para>" +
                    newline + indent;
            }
        }
        var localStart = match.Groups["value"].Index;
        var localEnd = localStart + match.Groups["value"].Length;
        var replacementBlock = blockText[..localStart] + escaped + blockText[localEnd..];
        updated = text[..block.Start] + replacementBlock + text[block.End..];
        error = "";
        return true;
    }

    static string AddSourceDocumentationIfSafe(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs,
        bool allowEnumCreation = true)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];

        if (owner.IsEnumField)
        {
            var updatedBlock = AddEnumSummaryMetadata(
                blockText,
                file,
                docs,
                allowEnumCreation);
            if (updatedBlock.Equals(blockText, StringComparison.Ordinal))
                return text;
            updatedBlock = RemoveEnumDiscardedMetadata(updatedBlock);
            return text[..block.Start] + updatedBlock + text[block.End..];
        }

        blockText = RemoveStaleSourceLinks(blockText, docs.SourceUrl, removeAll: false);
        if (ContainsSourceUrl(blockText, docs.SourceUrl))
            return text[..block.Start] + blockText + text[block.End..];

        var remarks = owner.Docs.Element("remarks");
        var remarksText = remarks is null ? "" : NormalizeText(remarks.Value);
        var attributionOnly = remarks is null ||
            remarksText.Length == 0 ||
            remarksText.Equals("To be added.", StringComparison.Ordinal) ||
            remarksText.StartsWith(
                "Portions of this page are modifications based on work created and shared by",
                StringComparison.Ordinal);

        var newline = file.Newline;
        var docsIndent = file.IndentAt(block.Start);
        var childIndent = docsIndent + "  ";
        var paraIndent = childIndent + "  ";
        var additions = new List<string>();
        var replacedRemarksPlaceholder = owner.Placeholders.Any(
            placeholder => placeholder.Name is "remarks" or "para");
        if (attributionOnly && !replacedRemarksPlaceholder && docs.Paragraphs.Count > 0)
        {
            foreach (var paragraph in docs.Paragraphs)
                additions.Add($"{paraIndent}<para>{XmlEscape(paragraph)}</para>");
        }
        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        additions.Add(
            $"{paraIndent}<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceLabel} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
            "</a></format></para>");
        if (docs.SourceKind == "android" &&
            !blockText.Contains("https://developers.google.com/terms/site-policies", StringComparison.Ordinal))
        {
            additions.Add($"{paraIndent}<para>{AndroidAttribution}</para>");
        }

        string replacementBlock;
        var selfClosingRemarks = Regex.Match(
            blockText,
            @"<remarks\b[^>]*/>",
            RegexOptions.CultureInvariant);
        var attribution = blockText.IndexOf(
            "https://developers.google.com/terms/site-policies",
            StringComparison.Ordinal);
        var attributionPara = attribution < 0
            ? -1
            : blockText.LastIndexOf("<para", attribution, StringComparison.Ordinal);
        var remarksClose = blockText.LastIndexOf("</remarks>", StringComparison.Ordinal);
        if (selfClosingRemarks.Success)
        {
            var expanded =
                $"<remarks>{newline}" +
                string.Join(newline, additions) + newline +
                $"{childIndent}</remarks>";
            replacementBlock =
                blockText[..selfClosingRemarks.Index] + expanded +
                blockText[(selfClosingRemarks.Index + selfClosingRemarks.Length)..];
        }
        else if (remarksClose >= 0)
        {
            var insertionTarget = attributionPara >= 0 ? attributionPara : remarksClose;
            var insertion = ClosingInsertionPoint(blockText, insertionTarget, newline);
            var separator = insertion == insertionTarget ? newline : "";
            replacementBlock =
                blockText[..insertion] + separator +
                string.Join(newline, additions) + newline +
                (insertion == insertionTarget ? childIndent : "") +
                blockText[insertion..];
        }
        else
        {
            var docsClose = blockText.LastIndexOf("</Docs>", StringComparison.Ordinal);
            if (docsClose < 0)
                return text;
            var insertion = ClosingInsertionPoint(blockText, docsClose, newline);
            var separator = insertion == docsClose ? newline : "";
            replacementBlock =
                blockText[..insertion] + separator +
                $"{childIndent}<remarks>{newline}" +
                string.Join(newline, additions) + newline +
                $"{childIndent}</remarks>{newline}{docsIndent}" +
                blockText[docsClose..];
        }
        return text[..block.Start] + replacementBlock + text[block.End..];
    }

    static string RemoveStaleSourceLinks(
        string blockText,
        string sourceUrl,
        bool removeAll)
    {
        var expectedMember = SourceAnchorMember(sourceUrl);
        return Regex.Replace(
            blockText,
            @"^[ \t]*<para\b[^>]*>(?:(?!</para>).)*?title=""Reference documentation""(?:(?!</para>).)*?</para>\r?\n?",
            match =>
            {
                var href = Regex.Match(
                    match.Value,
                    @"\bhref=""(?<url>[^""]+)""",
                    RegexOptions.CultureInvariant);
                if (!href.Success)
                    return match.Value;
                var existingUrl = WebUtility.HtmlDecode(href.Groups["url"].Value);
                if (UrlsEqual(existingUrl, sourceUrl))
                    return match.Value;
                return removeAll ||
                    SourceAnchorMember(existingUrl).Equals(expectedMember, StringComparison.Ordinal)
                    ? ""
                    : match.Value;
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    static string RemoveEnumDiscardedMetadata(string blockText) =>
        Regex.Replace(
            blockText,
            @"<remarks\b(?<attrs>[^>]*)>(?<body>.*?)</remarks>",
            match =>
            {
                var body = Regex.Replace(
                    match.Groups["body"].Value,
                    @"^[ \t]*<para\b[^>]*>(?:(?!</para>).)*?(?:title=""Reference documentation""|https://developers\.google\.com/terms/site-policies)(?:(?!</para>).)*?</para>\r?\n?",
                    "",
                    RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);
                return $"<remarks{match.Groups["attrs"].Value}>{body}</remarks>";
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    static string AddEnumSummaryMetadata(
        string blockText,
        LoadedFile file,
        SourceDocs docs,
        bool allowCreation)
    {
        var summary = Regex.Match(
            blockText,
            @"<summary\b[^>]*>(?<value>.*?)</summary>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!summary.Success)
            return blockText;

        var summaryElement = XElement.Parse(summary.Value, LoadOptions.PreserveWhitespace);
        var existingProse = summaryElement.Elements("para")
            .Where(paragraph => !paragraph.Descendants("a").Any(link =>
                (string?)link.Attribute("title") == "Reference documentation" ||
                ((string?)link.Attribute("href"))?.Equals(
                    "https://developers.google.com/terms/site-policies",
                    StringComparison.Ordinal) == true))
            .Select(paragraph => CleanSourceText(paragraph.Value))
            .Where(value => value.Length > 0)
            .ToList();
        if (existingProse.Count == 0)
        {
            var directText = CleanSourceText(string.Concat(
                summaryElement.Nodes().OfType<XText>().Select(node => node.Value)));
            if (directText.Length > 0)
                existingProse.Add(directText);
        }

        var sourceParagraphs = docs.Paragraphs
            .Select(CleanSourceText)
            .Where(value => IsMeaningfulChannel(value, "remarks"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hasReferenceMetadata = summaryElement.Descendants("a").Any(link =>
            (string?)link.Attribute("title") == "Reference documentation");
        var hasAttribution = summaryElement.Descendants("a").Any(link =>
            ((string?)link.Attribute("href"))?.Equals(
                "https://developers.google.com/terms/site-policies",
                StringComparison.Ordinal) == true);
        var hasCorrectSource = ContainsSourceUrl(summary.Value, docs.SourceUrl);
        var repairEligible = IsEnumSummaryRepairCandidate(
                summaryElement,
                docs.SourceUrl,
                docs.SourceLabel,
                docs.SourceKind) &&
            hasCorrectSource &&
            sourceParagraphs.Count > 0 &&
            existingProse.Count == 1 &&
            NormalizeText(existingProse[0]).Equals(
                NormalizeText(sourceParagraphs[0]),
                StringComparison.Ordinal) &&
            IsDeprecationParagraph(sourceParagraphs[0]) &&
            sourceParagraphs.Skip(1).Any(paragraph =>
                !IsDeprecationParagraph(paragraph));
        var creationEligible = !hasReferenceMetadata &&
            !hasAttribution &&
            allowCreation &&
            sourceParagraphs.Count > 0 &&
            existingProse.Count == 1 &&
            NormalizeText(existingProse[0]).Equals(
                NormalizeText(sourceParagraphs[0]),
                StringComparison.Ordinal);
        var alreadyComplete = existingProse.SequenceEqual(
            sourceParagraphs,
            StringComparer.Ordinal);
        if (hasReferenceMetadata || hasAttribution)
        {
            if (alreadyComplete || !repairEligible)
                return blockText;
        }
        else if (!allowCreation)
        {
            return blockText;
        }

        var prose = repairEligible || creationEligible
            ? sourceParagraphs
            : existingProse;
        if (prose.Count == 0)
            return blockText;

        var newline = file.Newline;
        var lineStart = blockText.LastIndexOf(newline, summary.Index, StringComparison.Ordinal);
        lineStart = lineStart < 0 ? 0 : lineStart + newline.Length;
        var summaryIndent = blockText[lineStart..summary.Index];
        var paraIndent = summaryIndent + "  ";
        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        var additions = prose
            .Select(paragraph => $"{paraIndent}<para>{XmlEscape(paragraph)}</para>")
            .ToList();
        additions.Add(
            $"{paraIndent}<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceLabel} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
            "</a></format></para>");
        if (docs.SourceKind == "android")
            additions.Add($"{paraIndent}<para>{AndroidAttribution}</para>");

        var replacement =
            newline +
            string.Join(newline, additions) +
            $"{newline}{summaryIndent}";
        var valueStart = summary.Groups["value"].Index;
        var valueEnd = valueStart + summary.Groups["value"].Length;
        return blockText[..valueStart] + replacement + blockText[valueEnd..];
    }

    static bool IsDeprecationParagraph(string value) =>
        Regex.IsMatch(
            NormalizeText(value),
            @"^(?:This\s+)?(?:constant|field|member|method|class|interface)\s+(?:is|was)\s+deprecated\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static bool ContainsSourceUrl(string text, string sourceUrl) =>
        Regex.Matches(
            text,
            @"\bhref=""(?<url>[^""]+)""",
            RegexOptions.CultureInvariant)
            .Any(match => UrlsEqual(
                WebUtility.HtmlDecode(match.Groups["url"].Value),
                sourceUrl));

    static bool UrlsEqual(string left, string right) =>
        Uri.UnescapeDataString(left).Equals(
            Uri.UnescapeDataString(right),
            StringComparison.Ordinal);

    static string SourceAnchorMember(string url)
    {
        var anchor = WebUtility.HtmlDecode(url).Split('#', 2).ElementAtOrDefault(1) ?? "";
        return Uri.UnescapeDataString(anchor).Split('(', 2)[0];
    }

    static void ApplyChangedFiles(
        IReadOnlyList<(LoadedFile File, string Text)> changedFiles,
        ImportReport report)
    {
        foreach (var (file, text) in changedFiles)
        {
            file.WriteAtomically(text);
            report.MarkApplied(file.RelativePath);
            report.FilesChanged++;
        }

        foreach (var (file, _) in changedFiles)
            _ = XDocument.Load(file.Path, LoadOptions.PreserveWhitespace);
    }

    static int ClosingInsertionPoint(string text, int closingTag, string newline)
    {
        var lineStart = text.LastIndexOf(newline, closingTag, StringComparison.Ordinal);
        lineStart = lineStart < 0 ? 0 : lineStart + newline.Length;
        return string.IsNullOrWhiteSpace(text[lineStart..closingTag])
            ? lineStart
            : closingTag;
    }

    static string XmlEscape(string value) =>
        new XText(CleanSourceText(value)).ToString(SaveOptions.DisableFormatting);

    static string XmlAttributeEscape(string value) =>
        SecurityElementEscape(value).Replace("\"", "&quot;", StringComparison.Ordinal);

    static string SecurityElementEscape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    static string CleanSourceText(string value)
    {
        var text = NormalizeText(value);
        text = Regex.Replace(
            text,
            @"\\u(?<hex>[0-9a-fA-F]{4})",
            match =>
            {
                var character = (char)Convert.ToInt32(match.Groups["hex"].Value, 16);
                return char.IsSurrogate(character) ? match.Value : character.ToString();
            },
            RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"\{@(?:link|linkplain|code|literal|value)\s+([^}]+)\}", "$1");
        text = Regex.Replace(text, @"\{@\w+(?:\s+[^}]*)?\}", "");
        text = Regex.Replace(text, @"(?<!\w)#(?=[A-Za-z_])", "");
        text = Regex.Replace(text, @"\s+([,.:;])", "$1");
        return NormalizeText(text);
    }

    static string CleanSourceParagraph(string value)
    {
        var text = CleanSourceText(value);
        string[] footerMarkers =
        [
            "Content and code samples on this page are subject to the licenses described in the Content License.",
            "Java and OpenJDK are trademarks or registered trademarks of Oracle and/or its affiliates.",
            "Java is a registered trademark of Oracle and/or its affiliates.",
            "Last updated ",
        ];
        foreach (var marker in footerMarkers)
        {
            var markerIndex = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
                text = text[..markerIndex].Trim();
        }
        return text;
    }

    static string NormalizeText(string value) =>
        Regex.Replace(WebUtility.HtmlDecode(value).Replace('\u00a0', ' '), @"\s+", " ").Trim();

    static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    static string ResolvePath(string repositoryRoot, string path)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        var repositoryRelative = Path.GetFullPath(Path.Combine(repositoryRoot, path));
        if (File.Exists(repositoryRelative) || Directory.Exists(repositoryRelative))
            return repositoryRelative;
        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
    }

    static void WriteReports(
        string repositoryRoot,
        string reportPath,
        ImportReport report,
        string humanReport)
    {
        var jsonPath = ResolveOutputPath(repositoryRoot, reportPath);
        var directory = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
        }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", report.Schema);
            writer.WriteString("mode", report.Mode);
            writer.WriteBoolean("offline", report.Offline);
            writer.WriteNumber("maxChanges", report.MaxChanges);
            writer.WriteNumber("filesScanned", report.FilesScanned);
            writer.WriteNumber("filesChanged", report.FilesChanged);
            writer.WriteNumber("sourcesFetched", report.SourcesFetched);
            writer.WriteNumber("sourcesFromCache", report.SourcesFromCache);
            writer.WriteNumber("appliedCount", report.AppliedCount);
            writer.WriteNumber("wouldApplyCount", report.WouldApplyCount);
            writer.WriteNumber("skippedCount", report.SkippedCount);
            writer.WriteNumber("errorCount", report.ErrorCount);
            writer.WriteStartArray("entries");
            foreach (var entry in report.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("status", entry.Status);
                writer.WriteString("path", entry.Path);
                writer.WriteString("member", entry.Member);
                writer.WriteString("target", entry.Target);
                writer.WriteString("reason", entry.Reason);
                writer.WriteString("detail", entry.Detail);
                writer.WriteString("sourceUrl", entry.SourceUrl);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        File.WriteAllBytes(jsonPath, [.. stream.ToArray(), (byte)'\n']);
        var textPath = Path.ChangeExtension(jsonPath, ".txt");
        File.WriteAllText(textPath, humanReport, new UTF8Encoding(false));
    }

    static string ResolveOutputPath(string repositoryRoot, string path)
    {
        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.Combine(repositoryRoot, path);
        if (!Path.GetExtension(resolved).Equals(".json", StringComparison.OrdinalIgnoreCase))
            resolved += ".json";
        return Path.GetFullPath(resolved);
    }

    static int RunSelfTest(string repositoryRoot)
    {
        var fixtureRoot = Path.Combine(repositoryRoot, "tools", "importer-fixtures");
        var sourcePath = Path.Combine(fixtureRoot, "source.xml");
        var androidHtml = File.ReadAllText(Path.Combine(fixtureRoot, "android-reference.html"));
        var javaHtml = File.ReadAllText(Path.Combine(fixtureRoot, "java-reference.html"));
        var file = LoadedFile.Load(repositoryRoot, sourcePath);
        file.SelectOwners(null);
        Assert(file.Owners.Count == 6, "fixture owner count");

        var request = file.Owners[0].SourceRequest!;
        var androidPage = SourcePage.Parse(request, androidHtml);
        Assert(androidPage.TypeDocs?.Summary == "Represents a fixture widget.", "Android type summary");

        var setTitle = file.Owners.Single(owner => owner.Id.Contains("SetTitle", StringComparison.Ordinal));
        var pages = new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
        {
            [request.Url] = SourceLoadResult.Success(androidPage),
        };
        var mapped = MapOwner(setTitle, pages);
        var mappedDocs = mapped.Docs ?? throw new InvalidOperationException(
            "SELF-TEST FAIL: exact Android JNI match");
        var titleParameter = setTitle.Placeholders.Single(item => item.Name == "param");
        Assert(
            ReplacementFor(titleParameter, mappedDocs).Text == "the title to display",
            "Android parameter type-prefix cleanup");
        Assert(mappedDocs.Returns == "the number of displayed characters", "Android return");
        Assert(mappedDocs.Exceptions["IllegalArgumentException"] == "if title is empty", "Android exception");

        var mismatch = file.Owners.Single(owner => owner.Id.Contains("SetCount", StringComparison.Ordinal));
        var mismatchResult = MapOwner(mismatch, pages);
        Assert(mismatchResult.ErrorReason == "overload_signature_mismatch", "overload mismatch skip");

        var favorite = file.Owners.Single(owner => owner.Id.Contains("Favorite", StringComparison.Ordinal));
        var favoriteResult = MapOwner(favorite, pages);
        Assert(
            favoriteResult.Docs?.Summary == "Identifies the favorite fixture value for the user\u2019s selection.",
            "exact field match");
        var typeOnly = ReplacementFor(
            new Placeholder(0, "returns", "", "returns"),
            favoriteResult.Docs! with { Returns = "String" });
        Assert(typeOnly.Reason == "source_channel_not_meaningful", "type-only return skip");
        var simpleTypeOnly = ReplacementFor(
            new Placeholder(0, "param", "items", "param:items"),
            favoriteResult.Docs! with
            {
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["items"] = "List",
                },
            });
        Assert(simpleTypeOnly.Reason == "source_channel_not_meaningful", "simple type-only parameter skip");
        Assert(
            CleanSourceText(@"the user\u2019s \u201cvalue\u201d") == "the user\u2019s \u201cvalue\u201d",
            "literal Unicode escape decoding");
        Assert(
            androidPage.Members.Single(member => member.Name == "Widget").Docs is null,
            "boilerplate-only member documentation skip");
        Assert(
            !favoriteResult.Docs.Paragraphs.Any(
                paragraph => paragraph.Contains("Content and code samples", StringComparison.Ordinal) ||
                    paragraph.StartsWith("Last updated ", StringComparison.Ordinal)),
            "Android footer paragraphs filtered");

        var javaRequest = new SourceRequest(
            "java/lang/String",
            JavaReference + "java.base/java/lang/String.html",
            "java");
        var javaPage = SourcePage.Parse(javaRequest, javaHtml);
        Assert(
            javaPage.TypeDocs?.Summary == "Represents a sequence of characters." &&
                !javaPage.TypeDocs.Paragraphs.Any(
                    paragraph => paragraph.Contains("Deprecated", StringComparison.Ordinal)),
            "Java deprecated type block excluded");
        var length = javaPage.Members.Single(member => member.Name == "length");
        Assert(length.ArgumentDescriptors?.Count == 0, "Java no-argument descriptor");
        Assert(length.Docs?.Returns == "the length of this string", "Java return extraction");
        Assert(
            length.Docs?.Summary == "Returns the length of this string." &&
                !length.Docs.Paragraphs.Any(
                    paragraph => paragraph.Contains("Deprecated", StringComparison.Ordinal)),
            "Java deprecated member block excluded");
        var empty = javaPage.Members.Single(member => member.Name == "EMPTY");
        Assert(empty.IsField && empty.Docs?.Summary == "An empty fixture string.", "Java field extraction");

        var block = file.DocsBlocks[setTitle.Order];
        var summary = setTitle.Placeholders.Single(item => item.Name == "summary");
        Assert(TryReplacePlaceholder(
            file.Text,
            block,
            summary,
            mappedDocs.Summary,
            out var updated,
            out _), "surgical placeholder replacement");
        Assert(updated.Contains("<summary>Sets the widget title.</summary>", StringComparison.Ordinal),
            "summary was replaced");
        Assert(updated.Contains("<para>Keep this existing prose.</para>", StringComparison.Ordinal),
            "existing prose was preserved");
        file.UpdateBlockOffsets(setTitle.Order, updated);
        var withRemarks = AddSourceDocumentationIfSafe(updated, file, setTitle, mappedDocs);
        Assert(withRemarks.Contains(mappedDocs.SourceUrl, StringComparison.Ordinal), "source link was added");
        Assert(
            !withRemarks.Contains(
                "Widget#setTitle(java.lang.String)",
                StringComparison.Ordinal),
            "stale source overload link was removed");
        Assert(
            withRemarks.IndexOf(mappedDocs.SourceUrl, StringComparison.Ordinal) <
                withRemarks.IndexOf(
                    "https://developers.google.com/terms/site-policies",
                    StringComparison.Ordinal),
            "source link precedes existing attribution");
        _ = XDocument.Parse(withRemarks, LoadOptions.PreserveWhitespace);

        file.UpdateBlockOffsets(setTitle.Order, withRemarks);
        var favoriteText = withRemarks;
        var favoriteSummary = favorite.Placeholders.Single(item => item.Name == "summary");
        Assert(TryReplacePlaceholder(
            favoriteText,
            file.DocsBlocks[favorite.Order],
            favoriteSummary,
            favoriteResult.Docs!.Summary,
            out favoriteText,
            out _), "field summary replacement");
        file.UpdateBlockOffsets(favorite.Order, favoriteText);
        var favoriteRemarks = favorite.Placeholders.Single(item => item.Name == "remarks");
        Assert(TryReplacePlaceholder(
            favoriteText,
            file.DocsBlocks[favorite.Order],
            favoriteRemarks,
            favoriteResult.Docs.Paragraphs[0],
            out favoriteText,
            out _), "inline remarks replacement");
        file.UpdateBlockOffsets(favorite.Order, favoriteText);
        favoriteText = AddSourceDocumentationIfSafe(favoriteText, file, favorite, favoriteResult.Docs);
        var favoriteDocument = XDocument.Parse(favoriteText, LoadOptions.PreserveWhitespace);
        var favoriteDocs = favoriteDocument.Root!.Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "Favorite")
            .Element("Docs")!;
        Assert(
            favoriteDocs.Element("remarks")!.Elements("para").First().Value
                .StartsWith(
                    "Identifies the favorite fixture value for the user\u2019s selection.",
                    StringComparison.Ordinal),
            "inline remarks replacement uses paragraph markup");
        Assert(
            favoriteDocument.Root is not null,
            "inline remarks source-link insertion produced valid XML");

        var emptyReturn = androidPage.Members.Single(member => member.Name == "emptyReturn");
        Assert(emptyReturn.Docs?.Returns.Length == 0, "empty return description preserved");
        var networkScan = androidPage.Members.Single(member => member.Name == "requestNetworkScan");
        Assert(
            networkScan.Docs?.Returns ==
                "a scan handle that can be used to stop the network scan",
            "exact Android Returns heading selected");
        Assert(
            !favoriteResult.Docs!.Paragraphs[0].Contains(")&quot;&gt;", StringComparison.Ordinal) &&
                !favoriteResult.Docs.Paragraphs[0].Contains(")\"&gt;", StringComparison.Ordinal) &&
                favoriteResult.Docs.Paragraphs[0].Contains("consume(List)", StringComparison.Ordinal),
            "quoted generic link stripped without corrupt fragments");

        var enumFile = LoadedFile.Load(
            repositoryRoot,
            Path.Combine(fixtureRoot, "enum-source.xml"));
        enumFile.SelectOwners(null);
        var enumFavorite = enumFile.Owners.Single(
            owner => (string?)owner.Member?.Attribute("MemberName") == "Favorite");
        Assert(enumFavorite.IsEnumField, "enum field detection");
        var enumMapped = MapOwner(enumFavorite, pages);
        var enumSummary = enumFavorite.Placeholders.Single(item => item.Name == "summary");
        Assert(TryReplacePlaceholder(
            enumFile.Text,
            enumFile.DocsBlocks[enumFavorite.Order],
            enumSummary,
            ReplacementFor(enumSummary, enumMapped.Docs!, true).Text!,
            out var enumText,
            out _), "enum summary replacement");
        enumFile.UpdateBlockOffsets(enumFavorite.Order, enumText);
        enumText = AddSourceDocumentationIfSafe(
            enumText,
            enumFile,
            enumFavorite,
            enumMapped.Docs!);
        var enumDocument = XDocument.Parse(enumText, LoadOptions.PreserveWhitespace);
        var enumDocs = enumDocument.Root!.Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "Favorite")
            .Element("Docs")!;
        Assert(
            enumDocs.Element("summary")!.Descendants("a").Any() &&
                enumDocs.Element("summary")!.Value.Contains(
                    "Android Open Source Project",
                    StringComparison.Ordinal),
            "enum source metadata is rendered in summary");
        Assert(
            !enumDocs.Element("remarks")!.Descendants("a").Any() &&
                NormalizeText(enumDocs.Element("remarks")!.Value)
                    .Equals("To be added.", StringComparison.Ordinal),
            "enum discarded remarks retain only placeholder");

        enumFile.UpdateBlockOffsets(enumFavorite.Order, enumText);
        var enumDeprecated = enumFile.Owners.Single(
            owner => (string?)owner.Member?.Attribute("MemberName") == "Deprecated");
        var deprecatedMapped = MapOwner(enumDeprecated, pages);
        var deprecatedSummary = enumDeprecated.Placeholders.Single(item => item.Name == "summary");
        Assert(TryReplacePlaceholder(
            enumText,
            enumFile.DocsBlocks[enumDeprecated.Order],
            deprecatedSummary,
            ReplacementFor(deprecatedSummary, deprecatedMapped.Docs!, true).Text!,
            out enumText,
            out _), "deprecated enum summary replacement");
        enumFile.UpdateBlockOffsets(enumDeprecated.Order, enumText);
        enumText = AddSourceDocumentationIfSafe(
            enumText,
            enumFile,
            enumDeprecated,
            deprecatedMapped.Docs!);
        var deprecatedDocument = XDocument.Parse(enumText, LoadOptions.PreserveWhitespace);
        var deprecatedDocs = deprecatedDocument.Root!.Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "Deprecated")
            .Element("Docs")!;
        var deprecatedProse = deprecatedDocs.Element("summary")!.Elements("para")
            .Where(paragraph => !paragraph.Descendants("a").Any())
            .Select(paragraph => NormalizeText(paragraph.Value))
            .ToList();
        Assert(
            deprecatedProse.Count == 2 &&
                deprecatedProse[0].StartsWith(
                    "This constant was deprecated in API level 31.",
                    StringComparison.Ordinal) &&
                deprecatedProse[1] == "Identifies the deprecated fixture value.",
            "deprecated enum publishes caution and semantic prose");

        var legacyEnumText = Regex.Replace(
            enumText,
            @"^[ \t]*<para>Identifies the deprecated fixture value\.</para>\r?\n",
            "",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        enumFile.UpdateBlockOffsets(enumDeprecated.Order, legacyEnumText);
        var refreshedEnumText = AddSourceDocumentationIfSafe(
            legacyEnumText,
            enumFile,
            enumDeprecated,
            deprecatedMapped.Docs!,
            allowEnumCreation: false);
        Assert(
            !refreshedEnumText.Equals(legacyEnumText, StringComparison.Ordinal) &&
                refreshedEnumText.Contains(
                    "<para>Identifies the deprecated fixture value.</para>",
                    StringComparison.Ordinal),
            "prior generated enum summary refreshes semantic prose");

        const string generatedCaution =
            "<para>This constant was deprecated in API level 31. Use FAVORITE instead.</para>";
        var decoratedEnumText = legacyEnumText.Replace(
            generatedCaution,
            "<para data-preserve=\"true\"><c>This constant was deprecated in API level 31. Use FAVORITE instead.</c></para>",
            StringComparison.Ordinal);
        decoratedEnumText = Regex.Replace(
            decoratedEnumText,
            @"<summary>(?=\s*<para data-preserve=""true"")",
            "<summary data-summary-preserve=\"true\">",
            RegexOptions.CultureInvariant);
        Assert(
            !decoratedEnumText.Equals(legacyEnumText, StringComparison.Ordinal) &&
                decoratedEnumText.Contains(
                    "<summary data-summary-preserve=\"true\">",
                    StringComparison.Ordinal),
            "non-qualifying enum fixture decoration");
        enumFile.UpdateBlockOffsets(enumDeprecated.Order, decoratedEnumText);
        Assert(
            AddSourceDocumentationIfSafe(
                decoratedEnumText,
                enumFile,
                enumDeprecated,
                deprecatedMapped.Docs!,
                allowEnumCreation: false).Equals(
                    decoratedEnumText,
                    StringComparison.Ordinal),
            "non-qualifying enum summary preserved verbatim");

        var linkedEnumText = legacyEnumText.Replace(
            "        </summary>",
            "          <para><format type=\"text/html\"><a href=\"https://example.invalid/unrelated\">Keep this linked content.</a></format></para>\r\n" +
                "        </summary>",
            StringComparison.Ordinal);
        Assert(
            !linkedEnumText.Equals(legacyEnumText, StringComparison.Ordinal),
            "unrelated linked enum fixture");
        enumFile.UpdateBlockOffsets(enumDeprecated.Order, linkedEnumText);
        Assert(
            AddSourceDocumentationIfSafe(
                linkedEnumText,
                enumFile,
                enumDeprecated,
                deprecatedMapped.Docs!,
                allowEnumCreation: false).Equals(
                    linkedEnumText,
                    StringComparison.Ordinal),
            "enum summary with unrelated linked content preserved verbatim");

        var emptyRemarks = file.Owners.Single(
            owner => owner.Id.Contains("EmptyRemarks", StringComparison.Ordinal));
        var emptyRemarksMapping = MapOwner(emptyRemarks, pages);
        var emptyRemarksSummary = emptyRemarks.Placeholders.Single(
            item => item.Name == "summary");
        Assert(TryReplacePlaceholder(
            file.Text,
            file.DocsBlocks[emptyRemarks.Order],
            emptyRemarksSummary,
            emptyRemarksMapping.Docs!.Summary,
            out var emptyRemarksText,
            out _), "self-closing remarks summary replacement");
        file.UpdateBlockOffsets(emptyRemarks.Order, emptyRemarksText);
        emptyRemarksText = AddSourceDocumentationIfSafe(
            emptyRemarksText,
            file,
            emptyRemarks,
            emptyRemarksMapping.Docs);
        var emptyRemarksDocument = XDocument.Parse(
            emptyRemarksText,
            LoadOptions.PreserveWhitespace);
        var emptyRemarksDocs = emptyRemarksDocument.Root!.Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "EmptyRemarks")
            .Element("Docs")!;
        Assert(
            emptyRemarksDocs.Elements("remarks").Count() == 1 &&
                emptyRemarksDocs.Element("remarks")!.Descendants("a").Any(),
            "self-closing remarks expanded in place");

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"android-api-doc-importer-self-test-{Environment.ProcessId}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var repairFailureDocument = XDocument.Parse(
                legacyEnumText,
                LoadOptions.PreserveWhitespace);
            repairFailureDocument.Root!.Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "Deprecated")
                .Element("Docs")!
                .Element("remarks")!
                .Remove();
            var repairFailurePath = Path.Combine(tempDirectory, "repair-failure.xml");
            File.WriteAllText(
                repairFailurePath,
                repairFailureDocument.ToString(SaveOptions.DisableFormatting),
                new UTF8Encoding(false));
            var repairFailureFile = LoadedFile.Load(repositoryRoot, repairFailurePath);
            repairFailureFile.SelectOwners("Deprecated");
            var repairFailureOwner = repairFailureFile.Owners.Single();
            Assert(
                repairFailureOwner.Placeholders.Count == 0 &&
                    IsEnumSummaryRepairCandidate(repairFailureOwner),
                "repair-only enum fixture has no placeholders");
            var repairFailureReport = new ImportReport
            {
                Mode = "dry-run",
                Offline = true,
                MaxChanges = 1,
            };
            var repairFailureMapping = MapOwner(
                repairFailureOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [repairFailureOwner.SourceRequest!.Url] = SourceLoadResult.Failure(
                        "offline_cache_miss",
                        "No cached official page exists for the fixture."),
                });
            Assert(
                ReportMappingFailure(
                    repairFailureReport,
                    repairFailureFile,
                    repairFailureOwner,
                    repairFailureMapping) &&
                    repairFailureReport.Entries.Count == 1 &&
                    repairFailureReport.Entries[0].Target == "summary" &&
                    repairFailureReport.Entries[0].Reason == "offline_cache_miss" &&
                    repairFailureReport.Entries[0].SourceUrl.Length > 0,
                "repair-only mapping failure reported for summary");

            var tempPath = Path.Combine(tempDirectory, "source.xml");
            File.WriteAllText(
                tempPath,
                favoriteText.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\n", "\r\n", StringComparison.Ordinal),
                new UTF8Encoding(false));
            var writable = LoadedFile.Load(repositoryRoot, tempPath);
            writable.WriteAtomically(writable.Text);
            var written = File.ReadAllText(tempPath);
            Assert(
                written.Replace("\r\n", "", StringComparison.Ordinal).IndexOf('\n') < 0,
                "atomic write preserved CRLF");
            _ = XDocument.Load(tempPath, LoadOptions.PreserveWhitespace);
            Assert(true, "atomic write produced valid XML");

            var pendingReport = new ImportReport
            {
                Mode = "apply",
                Offline = true,
                MaxChanges = 1,
                SourcesFetched = 2,
                SourcesFromCache = 3,
            };
            pendingReport.Entries.Add(ReportEntry.Changed(
                "would_apply",
                writable.RelativePath,
                "fixture",
                "summary",
                ""));
            var blockedPath = Path.Combine(tempDirectory, "blocked.xml");
            File.WriteAllText(blockedPath, writable.Text, new UTF8Encoding(false));
            var blocked = LoadedFile.Load(repositoryRoot, blockedPath);
            pendingReport.Entries.Add(ReportEntry.Changed(
                "would_apply",
                blocked.RelativePath,
                "blocked-fixture",
                "summary",
                ""));
            Directory.CreateDirectory(blockedPath + ".importer.tmp");
            try
            {
                try
                {
                    ApplyChangedFiles(
                        [(writable, writable.Text), (blocked, blocked.Text)],
                        pendingReport);
                    Assert(false, "blocked apply must fail");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Assert(
                        pendingReport.Entries.Single(
                            entry => entry.Path == writable.RelativePath).Status == "applied" &&
                            pendingReport.Entries.Single(
                                entry => entry.Path == blocked.RelativePath).Status == "would_apply",
                        "partial apply reports per-file status");
                    Assert(
                        pendingReport.FilesChanged == 1 &&
                            pendingReport.SourcesFetched == 2 &&
                            pendingReport.SourcesFromCache == 3,
                        "partial apply aggregate counters remain consistent");
                }
            }
            finally
            {
                Directory.Delete(blockedPath + ".importer.tmp");
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }

        Console.WriteLine("SELF-TEST PASS: 50 assertions; exact Android/Java method and field matching, exact Android table headings, deprecated Java block exclusion, exact-structure deprecated enum repair and failure reporting, self-closing remarks expansion, table alignment, quote-aware HTML cleanup, stale-link replacement, partial-write reporting, mismatch and low-value channel skipping, source cleanup, channel extraction, preservation, paragraph remarks, source-link ordering, CRLF atomic writes, and XML parsing.");
        return 0;
    }

    static void Assert(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException($"SELF-TEST FAIL: {description}");
    }

    sealed class Options
    {
        public bool Apply { get; private set; }
        public bool Offline { get; private set; }
        public bool SelfTest { get; private set; }
        public bool Help { get; private set; }
        public int MaxChanges { get; private set; } = 25;
        public int Concurrency { get; private set; } = 4;
        public int Retries { get; private set; } = 3;
        public string? Namespace { get; private set; }
        public string? Member { get; private set; }
        public string? CacheDirectory { get; private set; }
        public string? ReportPath { get; private set; }
        public List<string> Paths { get; } = [];

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                string Value()
                {
                    if (++index >= args.Length)
                        throw new ArgumentException($"{argument} requires a value.");
                    return args[index];
                }

                switch (argument)
                {
                    case "--apply":
                        options.Apply = true;
                        break;
                    case "--dry-run":
                        options.Apply = false;
                        break;
                    case "--offline":
                        options.Offline = true;
                        break;
                    case "--self-test":
                        options.SelfTest = true;
                        break;
                    case "--path":
                        options.Paths.Add(Value());
                        break;
                    case "--namespace":
                        options.Namespace = Value();
                        break;
                    case "--member":
                        options.Member = Value();
                        break;
                    case "--cache":
                        options.CacheDirectory = Value();
                        break;
                    case "--report":
                        options.ReportPath = Value();
                        break;
                    case "--max-changes":
                        options.MaxChanges = PositiveInt(Value(), argument, 10_000);
                        break;
                    case "--concurrency":
                        options.Concurrency = PositiveInt(Value(), argument, 8);
                        break;
                    case "--retries":
                        options.Retries = NonNegativeInt(Value(), argument, 6);
                        break;
                    case "-h":
                    case "--help":
                        options.Help = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {argument}");
                }
            }
            return options;
        }

        static int PositiveInt(string value, string name, int maximum)
        {
            if (!int.TryParse(value, out var result) || result < 1 || result > maximum)
                throw new ArgumentException($"{name} must be between 1 and {maximum}.");
            return result;
        }

        static int NonNegativeInt(string value, string name, int maximum)
        {
            if (!int.TryParse(value, out var result) || result < 0 || result > maximum)
                throw new ArgumentException($"{name} must be between 0 and {maximum}.");
            return result;
        }

        public static void PrintHelp() => Console.WriteLine(
            """
            Conservative importer for exact Android and Java reference documentation.

            Usage:
              dotnet run importer.cs -- --path <file-or-directory> [filters] [options]

            Scope (at least one required):
              --path <path>          XML file or directory under docs/xml; repeatable
              --namespace <name>     Exact managed namespace/type prefix
              --member <text>        Exact managed member name or DocId substring

            Safety and I/O:
              --dry-run              Preview only (default)
              --apply                Write changes; requires --path or --namespace
              --max-changes <n>      Maximum placeholder elements (default: 25)
              --offline              Read only from cache; never use the network
              --cache <directory>    Cache official pages by URL hash
              --report <path>        Write deterministic JSON and adjacent text reports

            Network:
              --concurrency <1-8>    Bounded source fetches (default: 4)
              --retries <0-6>        Retry count with deterministic backoff (default: 3)

            Validation:
              --self-test            Run local fixture tests without network access
              -h, --help             Show help
            """);
    }

    sealed class LoadedFile
    {
        static readonly Regex DocsRegex = new(
            @"<Docs\b[^>]*>.*?</Docs>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        public required string Path { get; init; }
        public required string RelativePath { get; init; }
        public required string Text { get; set; }
        public required string Newline { get; init; }
        public required bool HasUtf8Bom { get; init; }
        public required XElement Root { get; init; }
        public List<DocsBlock> DocsBlocks { get; private set; } = [];
        public List<DocsOwner> Owners { get; } = [];

        public static LoadedFile Load(string repositoryRoot, string path)
        {
            var bytes = File.ReadAllBytes(path);
            var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            var text = Encoding.UTF8.GetString(bytes.AsSpan(hasBom ? Encoding.UTF8.Preamble.Length : 0));
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var root = document.Root ?? throw new XmlException("XML document has no root element.");
            return new LoadedFile
            {
                Path = path,
                RelativePath = Relative(repositoryRoot, path),
                Text = text,
                Newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n",
                HasUtf8Bom = hasBom,
                Root = root,
                DocsBlocks = FindDocsBlocks(text),
            };
        }

        public void SelectOwners(string? memberFilter)
        {
            Owners.Clear();
            var typeRegistration = Registration.Type(Root);
            var typeRequest = SourceRequest.Create(typeRegistration);
            var typeName = (string?)Root.Attribute("FullName") ?? (string?)Root.Attribute("Name") ?? "";
            var isEnum = Root.Elements("TypeSignature").Any(signature =>
                (string?)signature.Attribute("Language") == "C#" &&
                ((string?)signature.Attribute("Value"))?.StartsWith(
                    "public enum ",
                    StringComparison.Ordinal) == true);
            var ordered = new List<(XElement Docs, XElement? Member)>
            {
                (Root.Element("Docs") ?? new XElement("Docs"), null),
            };
            ordered.AddRange(
                Root.Element("Members")?.Elements("Member")
                    .Select(member => (member.Element("Docs") ?? new XElement("Docs"), (XElement?)member))
                ?? []);
            if (ordered.Count != DocsBlocks.Count)
                throw new XmlException(
                    $"Expected {ordered.Count} <Docs> blocks from XML structure, found {DocsBlocks.Count} lexical blocks.");

            for (var order = 0; order < ordered.Count; order++)
            {
                var (docs, member) = ordered[order];
                var id = member is null ? $"T:{typeName}" : MemberId(typeName, member);
                var name = (string?)member?.Attribute("MemberName");
                if (memberFilter is not null &&
                    !string.Equals(name, memberFilter, StringComparison.Ordinal) &&
                    !id.Contains(memberFilter, StringComparison.Ordinal))
                {
                    continue;
                }

                var placeholders = docs
                    .Descendants()
                    .Where(element => !element.HasElements && IsPlaceholder(element.Value))
                    .Select((element, index) => Placeholder.Create(element, index))
                    .ToList();
                var memberField = member is null ? null : Registration.JniField(member);
                var request = member is null
                    ? typeRequest
                    : SourceRequest.Create(memberField?.Owner) ?? typeRequest;
                Owners.Add(new DocsOwner(
                    order,
                    id,
                    docs,
                    member,
                    request,
                    placeholders,
                    isEnum && (string?)member?.Element("MemberType") == "Field"));
            }
        }

        static string MemberId(string typeName, XElement member)
        {
            var docId = member.Elements("MemberSignature")
                .FirstOrDefault(signature => (string?)signature.Attribute("Language") == "DocId");
            return (string?)docId?.Attribute("Value") ??
                $"{typeName}.{(string?)member.Attribute("MemberName")}";
        }

        static bool IsPlaceholder(string value)
        {
            var normalized = NormalizeText(value);
            return normalized.Equals("To be added", StringComparison.Ordinal) ||
                normalized.Equals("To be added.", StringComparison.Ordinal);
        }

        public void UpdateBlockOffsets(int changedOrder, string text)
        {
            Text = text;
            DocsBlocks = FindDocsBlocks(text);
            if (DocsBlocks.Count <= changedOrder)
                throw new XmlException("A <Docs> edit changed the number of documentation blocks.");
        }

        static List<DocsBlock> FindDocsBlocks(string text) =>
            DocsRegex.Matches(text)
                .Select((match, order) => new DocsBlock(order, match.Index, match.Index + match.Length))
                .ToList();

        public string IndentAt(int offset)
        {
            var lineStart = Text.LastIndexOf('\n', Math.Max(0, offset - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            return Regex.Match(Text[lineStart..], @"^[ \t]*").Value;
        }

        public void WriteAtomically(string text)
        {
            var encoding = new UTF8Encoding(HasUtf8Bom);
            var temp = Path + ".importer.tmp";
            File.WriteAllText(temp, text, encoding);
            try
            {
                _ = XDocument.Load(temp, LoadOptions.PreserveWhitespace);
                File.Move(temp, Path, true);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }
    }

    sealed record DocsBlock(int Order, int Start, int End);
    sealed record DocsOwner(
        int Order,
        string Id,
        XElement Docs,
        XElement? Member,
        SourceRequest? SourceRequest,
        List<Placeholder> Placeholders,
        bool IsEnumField);

    sealed record Placeholder(int Order, string Name, string Key, string Target)
    {
        public static Placeholder Create(XElement element, int order)
        {
            var name = element.Name.LocalName;
            var key = name switch
            {
                "param" => (string?)element.Attribute("name") ?? "",
                "exception" => (string?)element.Attribute("cref") ?? "",
                _ => "",
            };
            var target = key.Length == 0 ? name : $"{name}:{key}";
            return new Placeholder(order, name, key, target);
        }
    }

    sealed record MemberRegistration(string Name, string? Descriptor, bool IsField);
    sealed record JniFieldRegistration(string Owner, string Name);

    static class Registration
    {
        static readonly Regex TypeRegex = new(
            @"Register\(""(?<name>[^""]+)""",
            RegexOptions.CultureInvariant);
        static readonly Regex MemberRegex = new(
            @"Register\(""(?<name>[^""]+)""\s*,\s*""(?<descriptor>[^""]*)""",
            RegexOptions.CultureInvariant);
        static readonly Regex JniFieldRegex = new(
            @"JniField=""(?<owner>[^""]+)\.(?<name>[^"".]+)""",
            RegexOptions.CultureInvariant);

        public static string? Type(XElement root)
        {
            foreach (var attribute in root
                .Element("Attributes")?.Elements("Attribute")
                .SelectMany(item => item.Elements("AttributeName")) ?? [])
            {
                var match = TypeRegex.Match(attribute.Value);
                if (match.Success)
                    return match.Groups["name"].Value;
            }
            return null;
        }

        public static MemberRegistration? Member(XElement member)
        {
            foreach (var attribute in member
                .Element("Attributes")?.Elements("Attribute")
                .SelectMany(item => item.Elements("AttributeName")) ?? [])
            {
                var match = MemberRegex.Match(attribute.Value);
                if (match.Success)
                    return new MemberRegistration(
                        match.Groups["name"].Value,
                        match.Groups["descriptor"].Value,
                        false);
            }
            if (member.Element("MemberType")?.Value == "Field")
            {
                var jniField = JniField(member);
                if (jniField is not null)
                    return new MemberRegistration(jniField.Name, null, true);
                foreach (var attribute in member
                    .Element("Attributes")?.Elements("Attribute")
                    .SelectMany(item => item.Elements("AttributeName")) ?? [])
                {
                    var match = TypeRegex.Match(attribute.Value);
                    if (match.Success)
                        return new MemberRegistration(match.Groups["name"].Value, null, true);
                }
            }
            return null;
        }

        public static JniFieldRegistration? JniField(XElement member)
        {
            foreach (var attribute in member
                .Element("Attributes")?.Elements("Attribute")
                .SelectMany(item => item.Elements("AttributeName")) ?? [])
            {
                var match = JniFieldRegex.Match(attribute.Value);
                if (match.Success)
                    return new JniFieldRegistration(
                        match.Groups["owner"].Value,
                        match.Groups["name"].Value);
            }
            return null;
        }
    }

    sealed record SourceRequest(string JavaPath, string Url, string Kind)
    {
        public static SourceRequest? Create(string? javaPath)
        {
            if (string.IsNullOrWhiteSpace(javaPath))
                return null;
            if (javaPath.StartsWith("android/", StringComparison.Ordinal))
            {
                var urlPath = javaPath.Replace('$', '.');
                return new SourceRequest(javaPath, AndroidReference + urlPath, "android");
            }
            if (javaPath.StartsWith("java/", StringComparison.Ordinal) ||
                javaPath.StartsWith("javax/", StringComparison.Ordinal))
            {
                var module = JavaModule(javaPath);
                var urlPath = javaPath.Replace('$', '.');
                return new SourceRequest(javaPath, $"{JavaReference}{module}/{urlPath}.html", "java");
            }
            return null;
        }

        static string JavaModule(string path)
        {
            if (path.StartsWith("java/sql/", StringComparison.Ordinal) ||
                path.StartsWith("javax/sql/", StringComparison.Ordinal))
                return "java.sql";
            if (path.StartsWith("java/xml/", StringComparison.Ordinal) ||
                path.StartsWith("javax/xml/", StringComparison.Ordinal))
                return "java.xml";
            if (path.StartsWith("java/net/http/", StringComparison.Ordinal))
                return "java.net.http";
            return "java.base";
        }
    }

    sealed class SourceFetcher : IDisposable
    {
        readonly string cacheDirectory;
        readonly bool offline;
        readonly int concurrency;
        readonly int retries;
        readonly HttpClient client;
        int networkFetches;
        int cacheHits;

        public int NetworkFetches => networkFetches;
        public int CacheHits => cacheHits;

        public SourceFetcher(string cacheDirectory, bool offline, int concurrency, int retries)
        {
            this.cacheDirectory = cacheDirectory;
            this.offline = offline;
            this.concurrency = concurrency;
            this.retries = retries;
            client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        public async Task<IReadOnlyDictionary<string, SourceFetchResult>> FetchAsync(
            IReadOnlyList<SourceRequest> requests)
        {
            Directory.CreateDirectory(cacheDirectory);
            var results = new ConcurrentDictionary<string, SourceFetchResult>(StringComparer.Ordinal);
            await Parallel.ForEachAsync(
                requests,
                new ParallelOptions { MaxDegreeOfParallelism = concurrency },
                async (request, cancellationToken) =>
                {
                    results[request.Url] = await FetchOneAsync(request.Url, cancellationToken);
                });
            return new SortedDictionary<string, SourceFetchResult>(results, StringComparer.Ordinal);
        }

        async Task<SourceFetchResult> FetchOneAsync(string url, CancellationToken cancellationToken)
        {
            var cachePath = Path.Combine(cacheDirectory, Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant() + ".html");
            if (File.Exists(cachePath))
            {
                Interlocked.Increment(ref cacheHits);
                return SourceFetchResult.Success(await File.ReadAllTextAsync(cachePath, cancellationToken));
            }
            if (offline)
                return SourceFetchResult.Failure(
                    "offline_cache_miss",
                    $"No cached official page exists for {url}.");

            for (var attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return SourceFetchResult.Failure("source_not_found", $"Official page returned 404: {url}");
                    if (!response.IsSuccessStatusCode)
                    {
                        if (attempt < retries && IsTransient(response.StatusCode))
                        {
                            await Task.Delay(Backoff(attempt, response), cancellationToken);
                            continue;
                        }
                        return SourceFetchResult.Failure(
                            "source_http_error",
                            $"Official page returned {(int)response.StatusCode}: {url}");
                    }

                    var length = response.Content.Headers.ContentLength;
                    if (length > MaximumDownloadBytes)
                        return SourceFetchResult.Failure(
                            "source_too_large",
                            $"Official page exceeded {MaximumDownloadBytes} bytes: {url}");
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var memory = new MemoryStream();
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var read = await stream.ReadAsync(buffer, cancellationToken);
                        if (read == 0)
                            break;
                        if (memory.Length + read > MaximumDownloadBytes)
                            return SourceFetchResult.Failure(
                                "source_too_large",
                                $"Official page exceeded {MaximumDownloadBytes} bytes: {url}");
                        memory.Write(buffer, 0, read);
                    }
                    var content = Encoding.UTF8.GetString(memory.ToArray());
                    var temp = cachePath + ".tmp." + Environment.ProcessId;
                    await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false), cancellationToken);
                    File.Move(temp, cachePath, true);
                    Interlocked.Increment(ref networkFetches);
                    return SourceFetchResult.Success(content);
                }
                catch (Exception error) when (
                    error is HttpRequestException or TaskCanceledException &&
                    !cancellationToken.IsCancellationRequested)
                {
                    if (attempt < retries)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
                        continue;
                    }
                    return SourceFetchResult.Failure("source_fetch_failed", $"{url}: {error.Message}");
                }
            }
            return SourceFetchResult.Failure("source_fetch_failed", $"Could not fetch {url}.");
        }

        static bool IsTransient(HttpStatusCode status) =>
            status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)status >= 500;

        static TimeSpan Backoff(int attempt, HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter is not null)
                return retryAfter.Value > TimeSpan.FromSeconds(10)
                    ? TimeSpan.FromSeconds(10)
                    : retryAfter.Value;
            return TimeSpan.FromMilliseconds(250 * (1 << attempt));
        }

        public void Dispose() => client.Dispose();
    }

    sealed record SourceFetchResult(string? Content, string? Reason, string? Error)
    {
        public static SourceFetchResult Success(string content) => new(content, null, null);
        public static SourceFetchResult Failure(string reason, string error) => new(null, reason, error);
    }

    sealed record SourceLoadResult(SourcePage? Page, string? Reason, string? Error)
    {
        public static SourceLoadResult Success(SourcePage page) => new(page, null, null);
        public static SourceLoadResult Failure(string reason, string error) => new(null, reason, error);
    }

    sealed class SourcePage
    {
        public SourceDocs? TypeDocs { get; init; }
        public List<SourceMember> Members { get; init; } = [];

        public static SourcePage Parse(SourceRequest request, string html) =>
            request.Kind == "android"
                ? ParseAndroid(request, html)
                : ParseJava(request, html);

        static SourcePage ParseAndroid(SourceRequest request, string html)
        {
            var sections = new List<SourceMember>();
            var headings = Regex.Matches(
                html,
                @"<h3\b(?<attrs>[^>]*)>(?<title>.*?)</h3>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(match => new
                {
                    Match = match,
                    Attributes = ParseAttributes(match.Groups["attrs"].Value),
                    Title = HtmlText(match.Groups["title"].Value),
                })
                .Where(item => item.Attributes.TryGetValue("class", out var classes) &&
                    classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("api-name") &&
                    item.Attributes.ContainsKey("id"))
                .ToList();
            var sectionStarts = Regex.Matches(
                html,
                @"<h2\b[^>]*class=""[^""]*\bapi-section\b[^""]*""",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(match => match.Index)
                .ToList();

            for (var index = 0; index < headings.Count; index++)
            {
                var heading = headings[index];
                var nextHeading = index + 1 < headings.Count ? headings[index + 1].Match.Index : html.Length;
                var nextSection = sectionStarts.FirstOrDefault(position => position > heading.Match.Index);
                if (nextSection == 0)
                    nextSection = html.Length;
                var end = Math.Min(nextHeading, nextSection);
                var anchor = WebUtility.HtmlDecode(heading.Attributes["id"]);
                var fragment = html[heading.Match.Index..end];
                var arguments = Descriptor.FromAnchor(anchor, request.JavaPath);
                var name = anchor.Split('(', 2)[0];
                var isField = !anchor.Contains('(', StringComparison.Ordinal);
                var constructorName = request.JavaPath.Split('/', '$').Last();
                var isConstructor = name.Equals(constructorName, StringComparison.Ordinal);
                var url = request.Url + "#" + anchor.Replace(" ", "%20", StringComparison.Ordinal);
                sections.Add(new SourceMember(
                    name,
                    isConstructor,
                    isField,
                    arguments,
                    ExtractAndroidDocs(fragment, request, heading.Title, url)));
            }

            return new SourcePage
            {
                TypeDocs = ExtractAndroidTypeDocs(html, request),
                Members = sections,
            };
        }

        static SourceDocs? ExtractAndroidTypeDocs(string html, SourceRequest request)
        {
            var contentStart = html.IndexOf("id=\"jd-content\"", StringComparison.OrdinalIgnoreCase);
            if (contentStart < 0)
                contentStart = html.IndexOf("<main", StringComparison.OrdinalIgnoreCase);
            var summaryStart = html.IndexOf("id=\"summary\"", Math.Max(0, contentStart), StringComparison.OrdinalIgnoreCase);
            if (contentStart < 0 || summaryStart < 0)
                return null;
            var fragment = html[contentStart..summaryStart];
            var finalRule = fragment.LastIndexOf("<hr", StringComparison.OrdinalIgnoreCase);
            if (finalRule >= 0)
                fragment = fragment[finalRule..];
            var paragraphs = ExtractParagraphs(fragment);
            if (paragraphs.Count == 0)
                return null;
            return new SourceDocs(
                FirstSentence(paragraphs[0]),
                paragraphs,
                new Dictionary<string, string>(StringComparer.Ordinal),
                "",
                new Dictionary<string, string>(StringComparer.Ordinal),
                request.Url,
                request.JavaPath.Replace('/', '.').Replace('$', '.'),
                request.Kind);
        }

        static SourceDocs? ExtractAndroidDocs(
            string fragment,
            SourceRequest request,
            string title,
            string url)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match row in Regex.Matches(
                fragment,
                @"<tr\b[^>]*>(?<row>.*?)</tr>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var cells = Regex.Matches(
                    row.Groups["row"].Value,
                    @"<td\b[^>]*>(?<cell>.*?)</td>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    .Select(cell => HtmlText(cell.Groups["cell"].Value))
                    .ToList();
                if (cells.Count >= 2 && Regex.IsMatch(cells[0], @"^[A-Za-z_]\w*$"))
                    parameters.TryAdd(cells[0], cells[1]);
            }

            var returns = ExtractAndroidTableValue(fragment, "Returns");
            var exceptions = ExtractAndroidExceptions(fragment);
            var prose = Regex.Replace(
                fragment,
                @"<(?:table|pre|devsite-code)\b[^>]*>.*?</(?:table|pre|devsite-code)>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var paragraphs = ExtractParagraphs(prose);
            if (paragraphs.Count == 0)
                return null;
            return new SourceDocs(
                FirstSentence(paragraphs[0]),
                paragraphs,
                parameters,
                returns,
                exceptions,
                url,
                $"{request.JavaPath.Replace('/', '.').Replace('$', '.')}.{title}",
                request.Kind);
        }

        static string ExtractAndroidTableValue(string fragment, string heading)
        {
            foreach (Match table in Regex.Matches(
                fragment,
                @"<table\b[^>]*>(?<table>.*?)</table>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var rows = Regex.Matches(
                    table.Groups["table"].Value,
                    @"<tr\b[^>]*>(?<row>.*?)</tr>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    .Select(row => new
                    {
                        Cells = Regex.Matches(
                            row.Groups["row"].Value,
                            @"<t[dh]\b[^>]*>(?<cell>.*?)</t[dh]>",
                            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                            .Select(cell => HtmlText(cell.Groups["cell"].Value))
                            .ToList(),
                        Headings = Regex.Matches(
                            row.Groups["row"].Value,
                            @"<th\b[^>]*>(?<cell>.*?)</th>",
                            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                            .Select(cell => HtmlText(cell.Groups["cell"].Value))
                            .ToList(),
                    })
                    .ToList();
                var headingRow = rows.FindIndex(row => row.Headings.Any(
                    cell => cell.Equals(heading, StringComparison.OrdinalIgnoreCase)));
                if (headingRow < 0)
                    continue;
                foreach (var row in rows.Skip(headingRow + 1))
                {
                    var cells = row.Cells;
                    if (cells.Count > 0)
                        return string.Join(" ", cells.Skip(cells.Count > 1 ? 1 : 0));
                }
            }
            return "";
        }

        static Dictionary<string, string> ExtractAndroidExceptions(string fragment)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match table in Regex.Matches(
                fragment,
                @"<table\b[^>]*>(?<table>.*?)</table>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (!HtmlText(table.Value).Contains("Throws", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (Match row in Regex.Matches(
                    table.Groups["table"].Value,
                    @"<tr\b[^>]*>(?<row>.*?)</tr>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    var cells = Regex.Matches(
                        row.Groups["row"].Value,
                        @"<td\b[^>]*>(?<cell>.*?)</td>",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                        .Select(cell => HtmlText(cell.Groups["cell"].Value))
                        .ToList();
                    if (cells.Count >= 2)
                        result.TryAdd(cells[0], cells[1]);
                }
            }
            return result;
        }

        static SourcePage ParseJava(SourceRequest request, string html)
        {
            var members = new List<SourceMember>();
            foreach (Match section in Regex.Matches(
                html,
                @"<section\b(?<attrs>[^>]*)>(?<body>.*?)</section>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var attributes = ParseAttributes(section.Groups["attrs"].Value);
                if (!attributes.TryGetValue("class", out var classes) ||
                    !classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("detail") ||
                    !attributes.TryGetValue("id", out var encodedAnchor))
                    continue;
                var anchor = WebUtility.HtmlDecode(encodedAnchor);
                var isField = !anchor.Contains('(', StringComparison.Ordinal);
                var arguments = isField ? null : Descriptor.FromAnchor(anchor, request.JavaPath);
                if (!isField && arguments is null)
                    continue;
                var body = section.Groups["body"].Value;
                var heading = Regex.Match(
                    body,
                    @"<h3\b[^>]*>(?<name>.*?)</h3>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var displayName = heading.Success ? HtmlText(heading.Groups["name"].Value) : anchor.Split('(', 2)[0];
                var anchorName = anchor.Split('(', 2)[0];
                var isConstructor = anchorName is "<init>" or "%3Cinit%3E" ||
                    displayName.Equals(request.JavaPath.Split('/', '$').Last(), StringComparison.Ordinal);
                var name = isConstructor ? displayName : anchorName;
                var url = request.Url + "#" + encodedAnchor;
                members.Add(new SourceMember(
                    name,
                    isConstructor,
                    isField,
                    arguments,
                    ExtractJavaDocs(body, request, displayName, url)));
            }
            return new SourcePage
            {
                TypeDocs = ExtractJavaTypeDocs(html, request),
                Members = members,
            };
        }

        static SourceDocs? ExtractJavaTypeDocs(string html, SourceRequest request)
        {
            var match = Regex.Match(
                html,
                @"<section\b[^>]*class=""[^""]*\bclass-description\b[^""]*""[^>]*>(?<body>.*?)</section>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
                return null;
            var paragraphs = ExtractBlocks(match.Groups["body"].Value);
            if (paragraphs.Count == 0)
                return null;
            return new SourceDocs(
                FirstSentence(paragraphs[0]),
                paragraphs,
                new Dictionary<string, string>(StringComparer.Ordinal),
                "",
                new Dictionary<string, string>(StringComparer.Ordinal),
                request.Url,
                request.JavaPath.Replace('/', '.').Replace('$', '.'),
                request.Kind);
        }

        static SourceDocs? ExtractJavaDocs(
            string body,
            SourceRequest request,
            string displayName,
            string url)
        {
            var paragraphs = ExtractBlocks(body);
            if (paragraphs.Count == 0)
                return null;
            var notes = Regex.Match(
                body,
                @"<dl\b[^>]*class=""[^""]*\bnotes\b[^""]*""[^>]*>(?<notes>.*?)</dl>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var noteBody = notes.Success ? notes.Groups["notes"].Value : "";
            var parameters = ExtractJavaParameters(noteBody);
            var returns = ExtractJavaNoteValue(noteBody, "Returns:");
            var exceptions = ExtractJavaExceptions(noteBody);
            return new SourceDocs(
                FirstSentence(paragraphs[0]),
                paragraphs,
                parameters,
                returns,
                exceptions,
                url,
                $"{request.JavaPath.Replace('/', '.').Replace('$', '.')}.{displayName}",
                request.Kind);
        }

        static Dictionary<string, string> ExtractJavaParameters(string notes)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var body = NoteSection(notes, "Parameters:");
            foreach (Match item in Regex.Matches(
                body,
                @"<dd\b[^>]*>\s*<code\b[^>]*>(?<name>.*?)</code>\s*-\s*(?<value>.*?)</dd>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var name = HtmlText(item.Groups["name"].Value);
                var value = HtmlText(item.Groups["value"].Value);
                if (name.Length > 0 && value.Length > 0)
                    result.TryAdd(name, value);
            }
            return result;
        }

        static string ExtractJavaNoteValue(string notes, string heading)
        {
            var body = NoteSection(notes, heading);
            var item = Regex.Match(
                body,
                @"<dd\b[^>]*>(?<value>.*?)</dd>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return item.Success ? HtmlText(item.Groups["value"].Value) : "";
        }

        static Dictionary<string, string> ExtractJavaExceptions(string notes)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var body = NoteSection(notes, "Throws:");
            foreach (Match item in Regex.Matches(
                body,
                @"<dd\b[^>]*>\s*<code\b[^>]*>(?<name>.*?)</code>\s*-\s*(?<value>.*?)</dd>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var name = HtmlText(item.Groups["name"].Value);
                var value = HtmlText(item.Groups["value"].Value);
                if (name.Length > 0 && value.Length > 0)
                    result.TryAdd(name, value);
            }
            return result;
        }

        static string NoteSection(string notes, string heading)
        {
            var match = Regex.Match(
                notes,
                $@"<dt\b[^>]*>\s*{Regex.Escape(heading)}\s*</dt>(?<body>.*?)(?=<dt\b|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["body"].Value : "";
        }

        static List<string> ExtractParagraphs(string html) =>
            Regex.Matches(
                html,
                @"<p\b[^>]*>(?<body>.*?)</p>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(match => CleanSourceParagraph(HtmlText(match.Groups["body"].Value)))
                .Where(value => IsMeaningfulChannel(value, "remarks"))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        static List<string> ExtractBlocks(string html) =>
            Regex.Matches(
                html,
                @"<div\b(?<attrs>[^>]*)>(?<body>.*?)</div>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Where(match =>
                {
                    var attributes = ParseAttributes(match.Groups["attrs"].Value);
                    if (!attributes.TryGetValue("class", out var classes))
                        return false;
                    var classTokens = classes.Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries);
                    return classTokens.Contains("block", StringComparer.Ordinal) &&
                        !classTokens.Contains("deprecation-block", StringComparer.Ordinal);
                })
                .Select(match => HtmlText(match.Groups["body"].Value))
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        static Dictionary<string, string> ParseAttributes(string attributes)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(
                attributes,
                @"(?<name>[:\w-]+)\s*=\s*(?<quote>[""'])(?<value>.*?)\k<quote>",
                RegexOptions.Singleline | RegexOptions.CultureInvariant))
            {
                result[match.Groups["name"].Value] = WebUtility.HtmlDecode(match.Groups["value"].Value);
            }
            return result;
        }

        static string HtmlText(string html)
        {
            var withoutIgnored = Regex.Replace(
                html,
                @"<(?:script|style|svg|pre|devsite-code)\b[^>]*>.*?</(?:script|style|svg|pre|devsite-code)>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var withBreaks = Regex.Replace(
                withoutIgnored,
                @"</?(?:p|div|li|tr|td|th|dd|dt|br|ul|ol|blockquote)\b[^>]*>",
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return CleanSourceText(StripHtmlTags(withBreaks));
        }

        static string StripHtmlTags(string html)
        {
            var text = new StringBuilder(html.Length);
            var inTag = false;
            var quote = '\0';
            foreach (var character in html)
            {
                if (!inTag)
                {
                    if (character == '<')
                    {
                        inTag = true;
                        quote = '\0';
                        text.Append(' ');
                    }
                    else
                    {
                        text.Append(character);
                    }
                    continue;
                }

                if (quote != '\0')
                {
                    if (character == quote)
                        quote = '\0';
                }
                else if (character is '"' or '\'')
                {
                    quote = character;
                }
                else if (character == '>')
                {
                    inTag = false;
                }
            }
            return text.ToString();
        }

        static string FirstSentence(string text)
        {
            var match = Regex.Match(text, @"^(.+?[.!?])(?:\s|$)", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : text;
        }
    }

    sealed record SourceMember(
        string Name,
        bool IsConstructor,
        bool IsField,
        List<string>? ArgumentDescriptors,
        SourceDocs? Docs)
    {
        public string Url => Docs?.SourceUrl ?? "";
    }

    sealed record SourceDocs(
        string Summary,
        List<string> Paragraphs,
        Dictionary<string, string> Parameters,
        string Returns,
        Dictionary<string, string> Exceptions,
        string SourceUrl,
        string SourceLabel,
        string SourceKind);

    static class Descriptor
    {
        static readonly Dictionary<string, string> Primitive = new(StringComparer.Ordinal)
        {
            ["boolean"] = "Z",
            ["byte"] = "B",
            ["char"] = "C",
            ["double"] = "D",
            ["float"] = "F",
            ["int"] = "I",
            ["long"] = "J",
            ["short"] = "S",
            ["void"] = "V",
        };

        static readonly HashSet<string> JavaLang = new(StringComparer.Ordinal)
        {
            "Boolean", "Byte", "CharSequence", "Character", "Class", "ClassLoader",
            "Double", "Enum", "Exception", "Float", "Integer", "Iterable", "Long",
            "Object", "Runnable", "Short", "String", "Throwable",
        };

        public static List<string>? ParseArguments(string descriptor)
        {
            if (descriptor.Length < 2 || descriptor[0] != '(')
                return null;
            var result = new List<string>();
            var index = 1;
            while (index < descriptor.Length && descriptor[index] != ')')
            {
                var start = index;
                while (index < descriptor.Length && descriptor[index] == '[')
                    index++;
                if (index >= descriptor.Length)
                    return null;
                if (descriptor[index] == 'L')
                {
                    var end = descriptor.IndexOf(';', index);
                    if (end < 0)
                        return null;
                    index = end + 1;
                }
                else if ("ZBCDFIJS".Contains(descriptor[index], StringComparison.Ordinal))
                {
                    index++;
                }
                else
                {
                    return null;
                }
                result.Add(descriptor[start..index]);
            }
            return index < descriptor.Length && descriptor[index] == ')' ? result : null;
        }

        public static List<string>? FromAnchor(string anchor, string currentPath)
        {
            anchor = Uri.UnescapeDataString(WebUtility.HtmlDecode(anchor));
            var open = anchor.IndexOf('(');
            if (open < 0 || !anchor.EndsWith(')'))
                return null;
            var body = anchor[(open + 1)..^1];
            var values = SplitTopLevel(body);
            var result = new List<string>();
            foreach (var value in values)
            {
                var descriptor = FromJavaType(value, currentPath);
                if (descriptor is null)
                    return null;
                result.Add(descriptor);
            }
            return result;
        }

        static string? FromJavaType(string javaType, string currentPath)
        {
            var value = NormalizeText(javaType);
            value = Regex.Replace(value, @"@\w+(?:\([^)]*\))?\s*", "");
            value = value.Replace("? extends ", "", StringComparison.Ordinal)
                .Replace("? super ", "", StringComparison.Ordinal)
                .Replace("?", "", StringComparison.Ordinal);
            value = Regex.Replace(value, @"<.*>", "").Trim();
            var dimensions = 0;
            if (value.EndsWith("...", StringComparison.Ordinal))
            {
                value = value[..^3].Trim();
                dimensions++;
            }
            while (value.EndsWith("[]", StringComparison.Ordinal))
            {
                value = value[..^2].Trim();
                dimensions++;
            }

            string descriptor;
            if (Primitive.TryGetValue(value, out var primitive))
            {
                descriptor = primitive;
            }
            else
            {
                if (!value.Contains('.', StringComparison.Ordinal))
                {
                    value = JavaLang.Contains(value)
                        ? "java.lang." + value
                        : currentPath[..currentPath.LastIndexOf('/')].Replace('/', '.') + "." + value;
                }
                var parts = value.Split('.');
                var classStart = Array.FindIndex(parts, part => part.Length > 0 && char.IsUpper(part[0]));
                if (classStart < 0)
                    return null;
                var package = string.Join("/", parts.Take(classStart));
                var className = string.Join("$", parts.Skip(classStart));
                descriptor = $"L{package}/{className};";
            }
            return new string('[', dimensions) + descriptor;
        }

        static List<string> SplitTopLevel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return [];
            var result = new List<string>();
            var start = 0;
            var depth = 0;
            for (var index = 0; index < value.Length; index++)
            {
                switch (value[index])
                {
                    case '<':
                    case '[':
                        depth++;
                        break;
                    case '>':
                    case ']':
                        depth = Math.Max(0, depth - 1);
                        break;
                    case ',' when depth == 0:
                        result.Add(value[start..index].Trim());
                        start = index + 1;
                        break;
                }
            }
            result.Add(value[start..].Trim());
            return result;
        }
    }

    sealed record MappingResult(
        SourceDocs? Docs,
        string? ErrorReason,
        string Detail,
        string SourceUrl)
    {
        public static MappingResult Success(SourceDocs docs) =>
            new(docs, null, "", docs.SourceUrl);
        public static MappingResult Skip(string reason, string detail, string sourceUrl = "") =>
            new(null, reason, detail, sourceUrl);
    }

    sealed record Replacement(string? Text, string? Reason, string Detail)
    {
        public static Replacement Use(string text) => new(text, null, "");
        public static Replacement Skip(string reason, string detail) => new(null, reason, detail);
    }

    sealed class ImportReport
    {
        public string Schema { get; init; } = "android-api-doc-importer-report/v1";
        public required string Mode { get; init; }
        public required bool Offline { get; init; }
        public required int MaxChanges { get; init; }
        public int FilesScanned { get; set; }
        public int FilesChanged { get; set; }
        public int SourcesFetched { get; set; }
        public int SourcesFromCache { get; set; }
        public int AppliedCount { get; private set; }
        public int WouldApplyCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int ErrorCount { get; private set; }
        public List<ReportEntry> Entries { get; set; } = [];

        public void SortAndCount()
        {
            Entries = Entries
                .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                .ThenBy(entry => entry.Member, StringComparer.Ordinal)
                .ThenBy(entry => entry.Target, StringComparer.Ordinal)
                .ThenBy(entry => entry.Status, StringComparer.Ordinal)
                .ToList();
            AppliedCount = Entries.Count(entry => entry.Status == "applied");
            WouldApplyCount = Entries.Count(entry => entry.Status == "would_apply");
            SkippedCount = Entries.Count(entry => entry.Status == "skipped");
            ErrorCount = Entries.Count(entry => entry.Status == "error");
        }

        public void MarkApplied(string path)
        {
            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (entry.Status == "would_apply" &&
                    entry.Path.Equals(path, StringComparison.Ordinal))
                {
                    Entries[index] = entry with { Status = "applied" };
                }
            }
        }

        public string ToHumanText()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Mode: {Mode}");
            builder.AppendLine(
                $"Files: scanned={FilesScanned}, changed={FilesChanged}; " +
                $"sources: network={SourcesFetched}, cache={SourcesFromCache}");
            builder.AppendLine(
                $"Results: applied={AppliedCount}, would-apply={WouldApplyCount}, " +
                $"skipped={SkippedCount}, errors={ErrorCount}");
            foreach (var group in Entries
                .Where(entry => entry.Status is "skipped" or "error")
                .GroupBy(entry => (entry.Status, entry.Reason))
                .OrderBy(group => group.Key.Status, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Reason, StringComparer.Ordinal))
            {
                builder.AppendLine($"  {group.Key.Status}: {group.Key.Reason} ({group.Count()})");
            }
            foreach (var entry in Entries.Where(entry => entry.Status is "skipped" or "error"))
            {
                builder.Append($"  {entry.Status}: {entry.Path}");
                if (entry.Member.Length > 0)
                    builder.Append($" [{entry.Member}]");
                if (entry.Target.Length > 0)
                    builder.Append($" {entry.Target}");
                builder.Append($" - {entry.Reason}");
                if (entry.Detail.Length > 0)
                    builder.Append($": {entry.Detail}");
                builder.AppendLine();
            }
            return builder.ToString();
        }
    }

    sealed record ReportEntry
    {
        public required string Status { get; init; }
        public required string Path { get; init; }
        public required string Member { get; init; }
        public required string Target { get; init; }
        public required string Reason { get; init; }
        public required string Detail { get; init; }
        public required string SourceUrl { get; init; }

        public static ReportEntry Changed(
            string status,
            string path,
            string member,
            string target,
            string sourceUrl) =>
            new()
            {
                Status = status,
                Path = path,
                Member = member,
                Target = target,
                Reason = "exact_structural_match",
                Detail = "",
                SourceUrl = sourceUrl,
            };

        public static ReportEntry Skipped(
            string path,
            string member,
            string target,
            string reason,
            string detail,
            string sourceUrl = "") =>
            new()
            {
                Status = "skipped",
                Path = path,
                Member = member,
                Target = target,
                Reason = reason,
                Detail = detail,
                SourceUrl = sourceUrl,
            };

        public static ReportEntry Error(
            string path,
            string member,
            string target,
            string reason,
            string detail,
            string sourceUrl = "") =>
            new()
            {
                Status = "error",
                Path = path,
                Member = member,
                Target = target,
                Reason = reason,
                Detail = detail,
                SourceUrl = sourceUrl,
            };
    }
}
