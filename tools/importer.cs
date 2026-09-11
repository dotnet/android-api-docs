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
            var interfaceMemberResolver = new InterfaceMemberResolver(docsRoot);

            var loadedFiles = new List<LoadedFile>();
            foreach (var path in files)
            {
                try
                {
                    var file = LoadedFile.Load(repositoryRoot, path);
                    if (!MatchesNamespace(file.Root, options.Namespace))
                        continue;
                    file.SelectOwners(options.Member, interfaceMemberResolver);
                    loadedFiles.Add(file);
                }
                catch (Exception error) when (error is XmlException or IOException or UnauthorizedAccessException)
                {
                    report.Entries.Add(ReportEntry.Error(
                        Relative(repositoryRoot, path), "", "", "malformed_xml", error.Message));
                }
            }

            var sourceRequests = loadedFiles
                .SelectMany(file => file.Owners.Select(owner => (File: file, Owner: owner)))
                .Where(item =>
                    item.Owner.Placeholders.Count > 0 ||
                    IsEnumSummaryRepairCandidate(item.Owner) ||
                    HasPotentialEnumListRepair(item.File, item.Owner) ||
                    HasEnumDiscardedMetadataCandidate(item.File, item.Owner) ||
                    HasDeprecatedValueRepairCandidate(item.File, item.Owner) ||
                    HasAugmentedRemarksPlaceholder(item.File, item.Owner) ||
                    HasTruncatedImporterSummary(item.File, item.Owner) ||
                    HasIncompleteCodeExampleRemarks(item.File, item.Owner) ||
                    HasMetadataOnlyRemarks(item.File, item.Owner))
                .Select(item => item.Owner.SourceRequest)
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
                    var importedSourceChannel = false;
                    var replacedRemarksPlaceholder = false;
                    var deferredRemarksPlaceholder = false;
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
                            deferredRemarksPlaceholder |= placeholder.Name is "remarks" or "para";
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
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
                        importedSourceChannel = true;
                        replacedRemarksPlaceholder |= placeholder.Name is "remarks" or "para";
                        remaining--;
                        report.Entries.Add(ReportEntry.Changed(
                            "would_apply",
                            file.RelativePath,
                            owner.Id,
                            placeholder.Target,
                            mapping.SourceUrl));
                    }

                    var enumSummaryRepair = IsEnumSummaryRepairCandidate(owner);
                    var enumListRepair = mapping.Docs is not null &&
                        HasImporterOwnedEnumListGap(owner, mapping.Docs);
                    var enumDiscardedMetadataRepair = mapping.Docs is not null &&
                        owner.IsEnumField &&
                        !RemoveEnumDiscardedMetadata(
                            text[file.DocsBlocks[owner.Order].Start..file.DocsBlocks[owner.Order].End],
                            mapping.Docs!).Equals(
                                text[file.DocsBlocks[owner.Order].Start..file.DocsBlocks[owner.Order].End],
                                StringComparison.Ordinal);
                    var augmentedRemarksRepair = HasAugmentedRemarksPlaceholder(file, owner);
                    var truncatedSummaryRepair = HasTruncatedImporterSummary(file, owner);
                    var codeExampleRepair = HasIncompleteCodeExampleRemarks(file, owner);
                    var metadataOnlyRemarksRepair = HasMetadataOnlyRemarks(file, owner);
                    var channelOnlyMetadataRepair = HasChannelOnlySourceMetadata(
                        file,
                        owner,
                        mapping.Docs!);
                    if (!ownerChanged &&
                        mapping.Docs is not null &&
                        (enumSummaryRepair ||
                         enumListRepair ||
                         enumDiscardedMetadataRepair ||
                         augmentedRemarksRepair ||
                         truncatedSummaryRepair ||
                         codeExampleRepair ||
                         metadataOnlyRemarksRepair ||
                         channelOnlyMetadataRepair))
                    {
                        var refreshed = truncatedSummaryRepair
                            ? ReplaceTruncatedSummary(text, file, owner, mapping.Docs)
                            : text;
                        if (!refreshed.Equals(text, StringComparison.Ordinal))
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                        if (codeExampleRepair)
                        {
                            refreshed = ReplaceIncompleteCodeExampleRemarks(refreshed, file, owner, mapping.Docs);
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                        }
                        if (enumSummaryRepair ||
                            enumListRepair ||
                            enumDiscardedMetadataRepair ||
                            augmentedRemarksRepair ||
                            metadataOnlyRemarksRepair ||
                            channelOnlyMetadataRepair)
                        {
                            refreshed = AddSourceDocumentationIfSafe(
                                refreshed,
                                file,
                                owner,
                                mapping.Docs,
                                allowEnumCreation: false,
                                addMetadataForChannelOnlyMember: channelOnlyMetadataRepair);
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                        }
                        if (!refreshed.Equals(text, StringComparison.Ordinal))
                        {
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                            var repairTarget = "summary";
                            if (remaining == 0)
                            {
                                RestoreOffsetsAfterSkippedRepair(file, owner, text);
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    repairTarget,
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
                                    repairTarget,
                                    mapping.SourceUrl));
                            }
                        }
                    }

                    if (!ownerChanged &&
                        mapping.Docs is not null &&
                        HasDeprecatedValueRepairCandidate(file, owner))
                    {
                        var refreshed = RepairDeprecatedValue(
                            text,
                            file,
                            owner,
                            mapping.Docs);
                        if (!refreshed.Equals(text, StringComparison.Ordinal))
                        {
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                            if (remaining == 0)
                            {
                                RestoreOffsetsAfterSkippedRepair(file, owner, text);
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    "value",
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
                                    "value",
                                    mapping.SourceUrl));
                            }
                        }
                    }

                    if (ownerChanged &&
                        mapping.Docs is not null &&
                        ShouldAddSourceDocumentation(
                            deferredRemarksPlaceholder,
                            replacedRemarksPlaceholder,
                            importedSourceChannel,
                            mapping.Docs))
                    {
                        text = AddSourceDocumentationIfSafe(
                            text,
                            file,
                            owner,
                            mapping.Docs,
                            addMetadataForChannelOnlyMember: importedSourceChannel);
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
            .Where(path => !IsNonApiDocumentationFile(path, docsRoot, comparer))
            .Where(path => Path.GetExtension(path).Equals(".xml", comparer))
            .ToList();
        if (options.Paths.Count > 0 && selected.Count == 0)
            throw new ArgumentException("No --path selections resolved to XML files under docs/xml.");
        return selected;
    }

    static bool IsNonApiDocumentationFile(
        string path,
        string docsRoot,
        StringComparison comparer)
    {
        var frameworksIndexPrefix =
            Path.Combine(docsRoot, "FrameworksIndex") + Path.DirectorySeparatorChar;
        return path.Equals(Path.Combine(docsRoot, "index.xml"), comparer) ||
            path.Equals(Path.Combine(docsRoot, "_filter.xml"), comparer) ||
            path.StartsWith(frameworksIndexPrefix, comparer);
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

        var registration = owner.MemberRegistration ??
            (owner.Member is null ? null : Registration.Member(owner.Member));
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
            if (fieldDocs is null)
                return MappingResult.Skip(
                    "source_documentation_empty",
                    "The exact source field had no usable prose.",
                    fields[0].Url);
            return MappingResult.Success(WithSemanticSummaryIfNecessary(fieldDocs));
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
        if (docs is null)
            return MappingResult.Skip(
                "source_documentation_empty",
                "The exact source member had no usable prose.",
                exact[0].Url);
        var sourceVerifiedMapping = SourceVerifiedMemberMappings.Resolve(owner.Id);
        if (sourceVerifiedMapping is { UseFirstMeaningfulSummary: true })
        {
            docs = WithFirstMeaningfulSummary(docs);
        }
        if (sourceVerifiedMapping is { FilterSynchronousGeocoderBoilerplate: true })
        {
            docs = WithoutSynchronousGeocoderBoilerplate(docs);
        }
        return MappingResult.Success(WithSemanticSummaryIfNecessary(docs));
    }

    static SourceDocs WithSemanticSummaryIfNecessary(SourceDocs docs) =>
        IsMeaningfulChannel(docs.Summary, "summary")
            ? docs
            : WithFirstMeaningfulSummary(docs);

    static SourceDocs WithFirstMeaningfulSummary(SourceDocs docs)
    {
        var summary = docs.Paragraphs
            .Where(paragraph => !paragraph.IsCode)
            .Select(paragraph => SourcePage.FirstSentence(paragraph.Text))
            .FirstOrDefault(paragraph => IsMeaningfulChannel(paragraph, "summary"));
        return summary is null ? docs : docs with { Summary = summary };
    }

    static string? FirstDocumentationParagraph(SourceDocs docs)
    {
        var paragraphs = docs.Paragraphs
            .Where(paragraph => !paragraph.IsCode)
            .Where(paragraph => IsMeaningfulChannel(paragraph.Text, "remarks"))
            .ToList();
        if (paragraphs.Count == 0)
            return null;

        var first = CleanSourceText(paragraphs[0].Text);
        if (!IsDeprecationParagraph(first))
            return first;

        var semantic = paragraphs
            .Skip(1)
            .Select(paragraph => CleanSourceText(paragraph.Text))
            .FirstOrDefault(paragraph => !IsDeprecationParagraph(paragraph));
        return semantic is null ? first : $"{EnsureSentenceEnding(first)} {semantic}";
    }

    static string EnsureSentenceEnding(string value) =>
        value.EndsWith('.') ||
        value.EndsWith('!') ||
        value.EndsWith('?')
            ? value
            : value + ".";

    static SourceDocs WithoutSynchronousGeocoderBoilerplate(SourceDocs docs) =>
        docs with
        {
            Paragraphs = docs.Paragraphs
                .Where(paragraph => !IsSynchronousGeocoderBoilerplate(paragraph.Text))
                .ToList(),
        };

    static bool IsSynchronousGeocoderBoilerplate(string text)
    {
        var normalized = NormalizeText(text);
        return
            (normalized.StartsWith(
                "This method was deprecated in API level 33.",
                StringComparison.Ordinal) &&
             normalized.Contains(
                 "instead to avoid blocking a thread waiting for results.",
                 StringComparison.Ordinal)) ||
            (normalized.StartsWith(
                "Warning: This API may hit the network, and may block for excessive amounts of time.",
                StringComparison.Ordinal) &&
             normalized.Contains(
                 "encouraged to use the asynchronous version of this API.",
                 StringComparison.Ordinal));
    }

    static bool ReportMappingFailure(
        ImportReport report,
        LoadedFile file,
        DocsOwner owner,
        MappingResult mapping)
    {
        if (mapping.ErrorReason is null)
            return false;

        var targets = owner.Placeholders
            .Select(placeholder => placeholder.Target)
            .ToHashSet(StringComparer.Ordinal);
        if (IsEnumSummaryRepairCandidate(owner) ||
            HasTruncatedImporterSummary(file, owner) ||
            HasPotentialEnumListRepair(file, owner))
            targets.Add("summary");
        if (HasDeprecatedValueRepairCandidate(file, owner))
            targets.Add("value");
        if (HasAugmentedRemarksPlaceholder(file, owner) ||
            HasIncompleteCodeExampleRemarks(file, owner) ||
            HasMetadataOnlyRemarks(file, owner) ||
            HasEnumDiscardedMetadataCandidate(file, owner))
            targets.Add("remarks");

        foreach (var target in targets.OrderBy(target => target, StringComparer.Ordinal))
        {
            report.Entries.Add(ReportEntry.Skipped(
                file.RelativePath,
                owner.Id,
                target,
                mapping.ErrorReason,
                mapping.Detail,
                mapping.SourceUrl));
        }
        return true;
    }

    static void RestoreOffsetsAfterSkippedRepair(
        LoadedFile file,
        DocsOwner owner,
        string unchangedText) =>
        file.UpdateBlockOffsets(owner.Order, unchangedText);

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
        if (placeholder.IsImporterMetadataRepair)
            return ChannelValueOrSkip(
                FirstDocumentationParagraph(docs),
                "remarks",
                "source_remarks_missing");

        if (isEnumField && placeholder.Name is "remarks" or "para")
            return Replacement.Skip(
                "enum_field_remarks_not_rendered",
                "Enum field remarks are not emitted by ECMA2Yaml; authoritative prose is imported into the summary.");

        return placeholder.Name switch
        {
            "summary" => ChannelValueOrSkip(
                isEnumField ? docs.Paragraphs.FirstOrDefault()?.Text : docs.Summary,
                "summary",
                "source_summary_missing"),
            "remarks" or "para" => ChannelValueOrSkip(
                FirstDocumentationParagraph(docs),
                "remarks",
                "source_remarks_missing"),
            "param" => docs.Parameters.TryGetValue(placeholder.Key, out var parameter)
                ? ChannelValueOrSkip(parameter, "param", "source_parameter_missing")
                : Replacement.Skip(
                    "source_parameter_missing",
                    $"The exact source member did not document parameter '{placeholder.Key}'."),
            "returns" => ChannelValueOrSkip(
                docs.Returns,
                placeholder.Name,
                "source_return_missing"),
            "value" => ValueReplacement(docs),
            "exception" => ExceptionReplacement(placeholder, docs),
            _ => Replacement.Skip(
                "unsupported_placeholder_target",
                $"Placeholder element <{placeholder.Name}> is not imported."),
        };
    }

    static string? DeprecationAwareValueText(SourceDocs docs)
    {
        var first = docs.Paragraphs.FirstOrDefault(paragraph =>
            !paragraph.IsCode && IsMeaningfulChannel(paragraph.Text, "remarks"));
        return first is not null && IsDeprecationParagraph(first.Text)
            ? FirstDocumentationParagraph(docs)
            : docs.Summary;
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

    static bool HasDeprecatedValueRepairCandidate(LoadedFile file, DocsOwner owner)
    {
        var registration = owner.MemberRegistration ??
            (owner.Member is null ? null : Registration.Member(owner.Member));
        if (owner.IsEnumField ||
            owner.SourceRequest is null ||
            registration is not { IsField: true } ||
            owner.Docs.Element("value") is not XElement value ||
            value.HasElements ||
            !Regex.IsMatch(
                NormalizeText(value.Value),
                @"^This (?:field|constant|member) (?:is|was) deprecated(?: in API level \d+)?\.$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;

        var memberUrl = owner.SourceRequest.Url + "#" + registration.Name;
        return owner.Docs.Element("remarks")?
            .Elements("para")
            .Any(paragraph =>
                TryGetImporterSourceReferenceUrl(
                    paragraph.ToString(SaveOptions.DisableFormatting),
                    out var sourceUrl) &&
                UrlsEqual(sourceUrl, memberUrl)) == true;
    }

    static string RepairDeprecatedValue(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var replacement = FirstDocumentationParagraph(docs);
        var first = docs.Paragraphs.FirstOrDefault(paragraph =>
            !paragraph.IsCode && IsMeaningfulChannel(paragraph.Text, "remarks"));
        if (replacement is null ||
            first is null ||
            !IsDeprecationParagraph(first.Text) ||
            NormalizeText(replacement).Equals(NormalizeText(first.Text), StringComparison.Ordinal))
        {
            return text;
        }

        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!ContainsSourceUrl(blockText, docs.SourceUrl))
            return text;
        if (!TryFindDirectTextElementContentSpan(
                blockText,
                "value",
                out var valueStart,
                out var valueEnd) ||
            !NormalizeText(blockText[valueStart..valueEnd]).Equals(
                NormalizeText(SourcePage.FirstSentence(first.Text)),
                StringComparison.Ordinal))
        {
            return text;
        }

        var updatedBlock = blockText[..valueStart] + XmlEscape(replacement) + blockText[valueEnd..];
        return text[..block.Start] + updatedBlock + text[block.End..];
    }

    static bool TryFindDirectTextElementContentSpan(
        string blockText,
        string elementName,
        out int contentStart,
        out int contentEnd)
        => TryFindTextElementContentSpan(
            blockText,
            elementName,
            1,
            _ => true,
            _ => true,
            out _,
            out contentStart,
            out contentEnd);

    static bool TryFindTextElementContentSpan(
        string blockText,
        string elementName,
        int parentDepth,
        Func<string, bool> openingTagMatches,
        Func<string, bool> contentMatches,
        out int elementStart,
        out int contentStart,
        out int contentEnd)
    {
        elementStart = 0;
        contentStart = 0;
        contentEnd = 0;
        var depth = 0;
        var targetDepth = -1;
        var candidateElementStart = -1;
        var candidateContentStart = -1;

        for (var index = 0; index < blockText.Length;)
        {
            var tagStart = blockText.IndexOf('<', index);
            if (tagStart < 0)
                return false;
            if (blockText.AsSpan(tagStart).StartsWith("<!--", StringComparison.Ordinal))
            {
                var commentEnd = blockText.IndexOf("-->", tagStart + 4, StringComparison.Ordinal);
                if (commentEnd < 0)
                    return false;
                index = commentEnd + 3;
                continue;
            }
            if (blockText.AsSpan(tagStart).StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                var cdataEnd = blockText.IndexOf("]]>", tagStart + 9, StringComparison.Ordinal);
                if (cdataEnd < 0)
                    return false;
                index = cdataEnd + 3;
                continue;
            }
            if (blockText.AsSpan(tagStart).StartsWith("<?", StringComparison.Ordinal))
            {
                var instructionEnd = blockText.IndexOf("?>", tagStart + 2, StringComparison.Ordinal);
                if (instructionEnd < 0)
                    return false;
                index = instructionEnd + 2;
                continue;
            }

            var tagEnd = FindXmlTagEnd(blockText, tagStart);
            if (tagEnd < 0)
                return false;
            var tag = blockText[(tagStart + 1)..tagEnd].TrimStart();
            if (tag.StartsWith('!'))
            {
                index = tagEnd + 1;
                continue;
            }

            var closing = tag.StartsWith('/');
            var nameStart = closing ? 1 : 0;
            while (nameStart < tag.Length && char.IsWhiteSpace(tag[nameStart]))
                nameStart++;
            var nameEnd = nameStart;
            while (nameEnd < tag.Length &&
                !char.IsWhiteSpace(tag[nameEnd]) &&
                tag[nameEnd] != '/')
            {
                nameEnd++;
            }
            if (nameStart == nameEnd)
                return false;
            var name = tag[nameStart..nameEnd];

            if (closing)
            {
                if (targetDepth == depth &&
                    name.Equals(elementName, StringComparison.Ordinal))
                {
                    var candidateContent = blockText[candidateContentStart..tagStart];
                    if (contentMatches(candidateContent))
                    {
                        elementStart = candidateElementStart;
                        contentStart = candidateContentStart;
                        contentEnd = tagStart;
                        return true;
                    }
                    targetDepth = -1;
                }
                depth--;
            }
            else if (!tag.EndsWith("/", StringComparison.Ordinal))
            {
                if (targetDepth < 0 &&
                    depth == parentDepth &&
                    name.Equals(elementName, StringComparison.Ordinal) &&
                    openingTagMatches(tag))
                {
                    candidateElementStart = tagStart;
                    candidateContentStart = tagEnd + 1;
                    targetDepth = depth + 1;
                }
                depth++;
            }
            index = tagEnd + 1;
        }
        return false;
    }

    static int FindXmlTagEnd(string text, int tagStart)
    {
        var quote = '\0';
        for (var index = tagStart + 1; index < text.Length; index++)
        {
            var character = text[index];
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
                return index;
            }
        }
        return -1;
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

    static Replacement ValueReplacement(SourceDocs docs)
    {
        var returns = ChannelValueOrSkip(
            docs.Returns,
            "value",
            "source_return_missing");
        return returns.Text is not null
            ? returns
            : IsTypeOnlyReturnChannel(docs.Returns)
                ? ChannelValueOrSkip(
                    DeprecationAwareValueText(docs),
                    "value",
                    "source_return_missing")
                : returns;
    }

    static bool IsTypeOnlyReturnChannel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = NormalizeText(CleanSourceText(value)).TrimEnd('.').Trim();
        return Regex.IsMatch(
            normalized,
            @"^(?:boolean|byte|char|double|float|int|long|short|void|[A-Z][\w$]*(?:<[^>]+>)?(?:\[\])?|[\w$]+(?:\.[\w$]+)+(?:<[^>]+>)?(?:\[\])?)$",
            RegexOptions.CultureInvariant);
    }

    static string RemoveLeadingJavaType(string value)
    {
        var cleaned = CleanSourceText(value);
        cleaned = Regex.Replace(
            cleaned,
            @"^(?:[\w.$]+(?:<[^>]+>)?(?:\[\])?)\s*:\s*(?=\S)",
            "",
            RegexOptions.CultureInvariant).Trim();
        return CleanSourceText(cleaned);
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
            normalized.Contains("ERROR(", StringComparison.Ordinal) ||
            Regex.IsMatch(
                normalized,
                @"^Last updated \d{4}-\d{2}-\d{2} UTC$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            Regex.IsMatch(
                normalized,
                @"^Constant Value:\s*\S+(?:\s+\(\S+\))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;
        if (Regex.IsMatch(
            normalized,
            @"^\[[A-Za-z][A-Za-z0-9_-]*\]$",
            RegexOptions.CultureInvariant))
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
        if (normalized.EndsWith(":", StringComparison.Ordinal) ||
            normalized.EndsWith("i.e.", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(
                normalized,
                @"\b(?:and|or)$",
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
            1 => ChannelValueOrSkip(
                matches[0],
                "exception",
                "source_exception_not_meaningful"),
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
        if (placeholder.IsImporterMetadataRepair)
        {
            var remarks = Regex.Matches(
                    blockText,
                    @"<remarks\b[^>]*>.*?</remarks>",
                    RegexOptions.Singleline | RegexOptions.CultureInvariant)
                .Cast<Match>()
                .FirstOrDefault(match =>
                {
                    var parsed = XElement.Parse(match.Value, LoadOptions.PreserveWhitespace);
                    return LoadedFile.IsImporterAugmentedRemarksPlaceholder(parsed);
                });
            if (remarks is null)
            {
                updated = text;
                error = "Could not locate the structurally identified importer metadata remarks repair.";
                return false;
            }

            var remarksElement = XElement.Parse(remarks.Value, LoadOptions.PreserveWhitespace);
            var directPlaceholder = remarksElement.Nodes()
                .OfType<XText>()
                .FirstOrDefault(node => node is not XCData &&
                    NormalizeText(node.Value) is "To be added" or "To be added.");
            var emptyParagraph = directPlaceholder is null
                ? remarksElement.Elements("para")
                    .SingleOrDefault(paragraph => !paragraph.HasElements &&
                        NormalizeText(paragraph.Value).Length == 0)
                : null;
            if (directPlaceholder is null && emptyParagraph is null)
            {
                updated = text;
                error = "Could not locate the structurally identified importer metadata remarks repair.";
                return false;
            }

            if (directPlaceholder is not null)
                directPlaceholder.ReplaceWith(new XElement("para", replacement));
            else
                emptyParagraph!.Value = replacement;
            var repairedRemarks = remarksElement.ToString(SaveOptions.DisableFormatting);
            if (remarks.Value.Contains("\r\n", StringComparison.Ordinal))
                repairedRemarks = repairedRemarks.Replace("\n", "\r\n", StringComparison.Ordinal);
            if (LoadedFile.IsImporterAugmentedRemarksPlaceholder(
                    XElement.Parse(repairedRemarks, LoadOptions.PreserveWhitespace)))
            {
                updated = text;
                error = "Importer metadata remarks repair remained a candidate after replacement.";
                return false;
            }
            var repairedBlock = blockText[..remarks.Index] + repairedRemarks +
                blockText[(remarks.Index + remarks.Length)..];
            updated = text[..block.Start] + repairedBlock + text[block.End..];
            error = "";
            return true;
        }

        if (!TryFindTextElementContentSpan(
                blockText,
                placeholder.Name,
                placeholder.Name == "para" ? 2 : 1,
                tag => MatchesPlaceholderTag(tag, placeholder),
                value => NormalizeText(value) is "To be added" or "To be added.",
                out var elementStart,
                out var localStart,
                out var localEnd))
        {
            updated = text;
            error = $"Could not locate the structurally identified {placeholder.Target} placeholder in its <Docs> block.";
            return false;
        }

        var escaped = XmlEscape(replacement);
        if (placeholder.Name == "remarks")
        {
            var newline = blockText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lineStart = blockText.LastIndexOf(newline, elementStart, StringComparison.Ordinal);
            lineStart = lineStart < 0 ? 0 : lineStart + newline.Length;
            var indent = blockText[lineStart..elementStart];
            if (string.IsNullOrWhiteSpace(indent))
            {
                escaped =
                    newline + indent + "  " + $"<para>{escaped}</para>" +
                    newline + indent;
            }
        }
        var replacementBlock = blockText[..localStart] + escaped + blockText[localEnd..];
        updated = text[..block.Start] + replacementBlock + text[block.End..];
        error = "";
        return true;
    }

    static bool MatchesPlaceholderTag(string tag, Placeholder placeholder) =>
        placeholder.Name switch
        {
            "param" => Regex.IsMatch(
                    tag,
                    $@"\bname\s*=\s*""{Regex.Escape(placeholder.Key)}""",
                    RegexOptions.CultureInvariant),
            "exception" => Regex.IsMatch(
                    tag,
                    $@"\bcref\s*=\s*""{Regex.Escape(placeholder.Key)}""",
                    RegexOptions.CultureInvariant),
            _ => true,
        };

    static string AddSourceDocumentationIfSafe(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs,
        bool allowEnumCreation = true,
        bool addMetadataForChannelOnlyMember = false)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];

        if (owner.IsEnumField)
        {
            var updatedBlock = AddEnumSummaryMetadata(
                blockText,
                file,
                owner,
                docs,
                allowEnumCreation);
            updatedBlock = RemoveEnumDiscardedMetadata(updatedBlock, docs);
            if (updatedBlock.Equals(blockText, StringComparison.Ordinal))
                return text;
            return text[..block.Start] + updatedBlock + text[block.End..];
        }

        var hasRemarksPlaceholder = owner.Placeholders.Any(
            placeholder => placeholder.Name is "remarks" or "para");
        if (docs.Paragraphs.Count == 0 && hasRemarksPlaceholder)
        {
            if (HasAugmentedRemarksPlaceholder(file, owner))
                blockText = RemoveImporterRemarksMetadata(blockText);
            if (!addMetadataForChannelOnlyMember)
                return text[..block.Start] + blockText + text[block.End..];
            blockText = RemoveStandaloneRemarksPlaceholder(blockText);
        }

        blockText = RemoveStaleSourceLinks(blockText, docs.SourceUrl, removeAll: false);
        blockText = RemoveDuplicateSourceLinks(blockText, docs.SourceUrl);
        blockText = RemoveAugmentedRemarksPlaceholder(blockText);
        var metadataOnly = HasMetadataOnlyRemarks(blockText);
        if (ContainsSourceUrl(blockText, docs.SourceUrl))
        {
            if (!metadataOnly)
                return text[..block.Start] + blockText + text[block.End..];
            if (docs.Paragraphs.Count == 0)
            {
                blockText = RemoveDuplicateSourceLinks(blockText, docs.SourceUrl);
                return text[..block.Start] + blockText + text[block.End..];
            }
            blockText = RemoveMatchingSourceLinks(blockText, docs.SourceUrl);
        }

        var remarks = owner.Docs.Element("remarks");
        var remarksText = remarks is null ? "" : NormalizeText(remarks.Value);
        var attributionOnly = remarks is null ||
            remarksText.Length == 0 ||
            remarksText.Equals("To be added.", StringComparison.Ordinal) ||
            metadataOnly ||
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
        if (attributionOnly &&
            !replacedRemarksPlaceholder &&
            !hasRemarksPlaceholder &&
            docs.Paragraphs.Count > 0)
        {
            foreach (var paragraph in docs.Paragraphs)
                additions.Add(RenderDocumentationParagraph(paragraph, paraIndent));
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

    static bool HasAugmentedRemarksPlaceholder(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return Regex.IsMatch(
            file.Text[block.Start..block.End],
            @"<remarks\b[^>]*>[ \t\r\n]*To be added\.?[ \t\r\n]*(?=<para\b)",
            RegexOptions.CultureInvariant);
    }

    static bool HasChannelOnlySourceMetadata(
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        if (docs.Paragraphs.Count > 0 ||
            !owner.Placeholders.Any(placeholder => placeholder.Name is "remarks" or "para"))
        {
            return false;
        }

        var block = file.DocsBlocks[owner.Order];
        if (ContainsSourceUrl(file.Text[block.Start..block.End], docs.SourceUrl))
            return false;

        return docs.Parameters.Any(parameter =>
            IsMeaningfulChannel(RemoveLeadingJavaType(parameter.Value), "param") &&
            owner.Docs.Elements("param").Any(element =>
                (string?)element.Attribute("name") == parameter.Key &&
                NormalizeText(element.Value) == RemoveLeadingJavaType(parameter.Value))) ||
            (IsMeaningfulChannel(RemoveLeadingJavaType(docs.Returns), "returns") &&
             owner.Docs.Element("returns") is XElement returns &&
             NormalizeText(returns.Value) == RemoveLeadingJavaType(docs.Returns)) ||
            (ReplacementFor(
                 new Placeholder(0, "value", "", "value"),
                 docs).Text is string value &&
             owner.Docs.Element("value") is XElement valueElement &&
             NormalizeText(valueElement.Value) == value) ||
            owner.Docs.Elements("exception").Any(element =>
            {
                var replacement = ReplacementFor(
                    new Placeholder(
                        0,
                        "exception",
                        (string?)element.Attribute("cref") ?? "",
                        "exception"),
                    docs);
                return replacement.Text is not null &&
                    NormalizeText(element.Value) == replacement.Text;
            });
    }

    static bool ShouldAddSourceDocumentation(
        bool deferredRemarksPlaceholder,
        bool replacedRemarksPlaceholder,
        bool importedSourceChannel,
        SourceDocs docs) =>
        !deferredRemarksPlaceholder ||
        replacedRemarksPlaceholder;

    static bool HasTruncatedImporterSummary(LoadedFile file, DocsOwner owner)
        // A plain-text summary has no durable importer provenance.
        => false;

    static bool HasIncompleteCodeExampleRemarks(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = file.Text[block.Start..block.End];
        return blockText.Contains("title=\"Reference documentation\"", StringComparison.Ordinal) &&
            (
                (!Regex.IsMatch(
                    blockText,
                    @"<code\b[^>]*\blang=""text/java""",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                 Regex.IsMatch(
                    blockText,
                    @"(?:Example code:\s*(?:\{|</para>)|Example code:.*?//|expression:\s*will be true|For example:\s*\.\.\.|For example,\s+to loop\b.*?:\s+[A-Z]|To loop over\b.*?:</para>)",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ||
                Regex.IsMatch(
                    blockText,
                    @"<code\b[^>]*\blang=""text/java""[^>]*>\s*(?:public|protected|private)\s+(?:(?:static|final|abstract|synchronized|native)\s+)*(?:[\w.$<>\[\]?]+\s+)?\w+\s*\([^<]*\)\s*(?:throws\s+[^<;]+)?;?\s*</code>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                Regex.IsMatch(
                    blockText,
                    @"(?:\{@code|CharSequence\.subsequence\(\))",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    static bool HasMetadataOnlyRemarks(LoadedFile file, DocsOwner owner)
    {
        if (owner.IsEnumField)
            return false;
        var block = file.DocsBlocks[owner.Order];
        return HasImporterSourceReference(file, owner) &&
            HasMetadataOnlyRemarks(file.Text[block.Start..block.End]);
    }

    static bool HasMetadataOnlyRemarks(string blockText)
    {
        var remarks = Regex.Match(
            blockText,
            @"<remarks\b[^>]*>(?<body>.*?)</remarks>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!remarks.Success)
            return false;

        var withoutMetadata = Regex.Replace(
            remarks.Groups["body"].Value,
            @"<para\b[^>]*>(?:(?!</para>).)*?</para>",
            match => IsImporterMetadataParagraph(match.Value) ? "" : match.Value,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return NormalizeText(Regex.Replace(
            withoutMetadata,
            @"<[^>]*>",
            " ",
            RegexOptions.Singleline | RegexOptions.CultureInvariant)).Length == 0;
    }

    static bool IsImporterMetadataParagraph(string paragraph) =>
        TryGetImporterSourceReferenceUrl(paragraph, out _) ||
        Regex.IsMatch(
            paragraph,
            @"^\s*<para>\s*<format type=""text/html""><a href=""https://(?:developer\.android\.com/reference|docs\.oracle\.com/en/java/javase/21/docs/api)/[^""]+"" title=""Reference documentation"">(?:Android|Java) reference\.</a></format>\s*</para>\s*$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant) ||
        Regex.IsMatch(
            paragraph,
            @"^\s*<para\b[^>]*>.*?https://developers\.google\.com/terms/site-policies.*?</para>\s*$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

    static string ReplaceIncompleteCodeExampleRemarks(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        var remarks = Regex.Match(
            blockText,
            @"<remarks\b(?<attrs>[^>]*)>.*?</remarks>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!remarks.Success)
            return text;

        var newline = file.Newline;
        var docsIndent = file.IndentAt(block.Start);
        var childIndent = docsIndent + "  ";
        var paraIndent = childIndent + "  ";
        var paragraphs = docs.Paragraphs
            .Select(paragraph => RenderDocumentationParagraph(paragraph, paraIndent))
            .ToList();
        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        paragraphs.Add(
            $"{paraIndent}<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceLabel} reference for <code>{XmlEscape(docs.SourceLabel)}</code>.</a></format></para>");
        if (docs.SourceKind == "android")
            paragraphs.Add($"{paraIndent}<para>{AndroidAttribution}</para>");

        var replacement =
            $"<remarks{remarks.Groups["attrs"].Value}>{newline}" +
            string.Join(newline, paragraphs) + newline +
            $"{childIndent}</remarks>";
        var updatedBlock = blockText[..remarks.Index] + replacement + blockText[(remarks.Index + remarks.Length)..];
        return text[..block.Start] + updatedBlock + text[block.End..];
    }

    static string RemoveAugmentedRemarksPlaceholder(string blockText) =>
        Regex.Replace(
            blockText,
            @"(?<open><remarks\b[^>]*>)[ \t\r\n]*To be added\.?(?<separator>[ \t\r\n]*)(?=<para\b)",
            match => match.Groups["open"].Value + match.Groups["separator"].Value,
            RegexOptions.CultureInvariant);

    static string RemoveStandaloneRemarksPlaceholder(string blockText) =>
        Regex.Replace(
            blockText,
            @"<remarks(?<attrs>\b[^>]*)>[ \t\r\n]*To be added\.?[ \t\r\n]*</remarks>",
            "<remarks${attrs} />",
            RegexOptions.CultureInvariant);

    static string ReplaceTruncatedSummary(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs,
        bool hasImporterProvenance = false)
    {
        if (!hasImporterProvenance)
            return text;

        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        var summary = Regex.Match(
            blockText,
            @"<summary\b[^>]*>(?<value>.*?)</summary>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!summary.Success)
        {
            return text;
        }

        var existingSummary = NormalizeText(summary.Groups["value"].Value);
        var sourceSummary = NormalizeText(docs.Summary);
        if (sourceSummary.Length <= existingSummary.Length ||
            !sourceSummary.StartsWith(existingSummary, StringComparison.Ordinal))
        {
            return text;
        }

        var valueStart = summary.Groups["value"].Index;
        var valueEnd = valueStart + summary.Groups["value"].Length;
        var updatedBlock = blockText[..valueStart] + XmlEscape(docs.Summary) + blockText[valueEnd..];
        return text[..block.Start] + updatedBlock + text[block.End..];
    }

    static bool HasTruncatedSummaryEnding(string summary) =>
        Regex.IsMatch(
            summary,
            @"\b(?:e\.g\.|i\.e\.|vs\.|etc\.|\.\.\.)[\)\]\}]?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static bool HasImporterSourceReference(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return file.Text[block.Start..block.End].Contains(
            "title=\"Reference documentation\"",
            StringComparison.Ordinal);
    }

    static string RemoveStaleSourceLinks(
        string blockText,
        string sourceUrl,
        bool removeAll)
    {
        var expectedMember = SourceAnchorMember(sourceUrl);
        return Regex.Replace(
            blockText,
            @"(?:^[ \t]*)?<para\b[^>]*>(?:(?!</para>).)*?title=""Reference documentation""(?:(?!</para>).)*?</para>\r?\n?",
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

    static string RemoveMatchingSourceLinks(string blockText, string sourceUrl) =>
        Regex.Replace(
            blockText,
            @"^[ \t]*<para\b[^>]*>(?:(?!</para>).)*?title=""Reference documentation""(?:(?!</para>).)*?</para>\r?\n?",
            match =>
            {
                var href = Regex.Match(
                    match.Value,
                    @"\bhref=""(?<url>[^""]+)""",
                    RegexOptions.CultureInvariant);
                return href.Success && UrlsEqual(
                    WebUtility.HtmlDecode(href.Groups["url"].Value),
                    sourceUrl)
                    ? ""
                    : match.Value;
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    static string RemoveDuplicateSourceLinks(string blockText, string sourceUrl)
    {
        var found = false;
        return Regex.Replace(
            blockText,
            @"^[ \t]*<para\b[^>]*>(?:(?!</para>).)*?title=""Reference documentation""(?:(?!</para>).)*?</para>\r?\n?",
            match =>
            {
                var href = Regex.Match(
                    match.Value,
                    @"\bhref=""(?<url>[^""]+)""",
                    RegexOptions.CultureInvariant);
                if (!href.Success || !UrlsEqual(
                    WebUtility.HtmlDecode(href.Groups["url"].Value),
                    sourceUrl))
                    return match.Value;
                if (!found)
                {
                    found = true;
                    return match.Value;
                }
                return "";
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    static string RemoveImporterRemarksMetadata(string blockText) =>
        Regex.Replace(
            blockText,
            @"[ \t\r\n]*<para\b[^>]*>(?:(?!</para>).)*?(?:title=""Reference documentation""|https://developers\.google\.com/terms/site-policies)(?:(?!</para>).)*?</para>",
            "",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

    static int CountImporterSourceReferences(string blockText, string sourceUrl) =>
        Regex.Matches(
            blockText,
            @"<para\b[^>]*>(?:(?!</para>).)*?title=""Reference documentation""(?:(?!</para>).)*?</para>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Count(match =>
                TryGetImporterSourceReferenceUrl(match.Value, out var importerSourceUrl) &&
                UrlsEqual(importerSourceUrl, sourceUrl));

    static bool TryGetImporterSourceReferenceUrl(string paragraph, out string sourceUrl)
    {
        var match = Regex.Match(
            paragraph,
            @"^\s*<para>\s*<format type=""text/html""><a href=""(?<url>[^""]+)"" title=""Reference documentation"">(?:Android|Java) reference for <code>[^<]+</code>\.</a></format>\s*</para>\s*$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        sourceUrl = match.Success ? WebUtility.HtmlDecode(match.Groups["url"].Value) : "";
        return match.Success;
    }

    static string NormalizeStaleNestedConstructorLinks(
        string text,
        DocsBlock block)
    {
        var blockText = text[block.Start..block.End];
        var normalizedBlock = Regex.Replace(
            blockText,
            @"^[ \t]*<para\b[^>]*>(?:(?!</para>).)*?\bhref=""[^""]*#[^""()]*\$[A-Za-z_]\w*\([^""]*""(?:(?!</para>).)*?title=""Reference documentation""(?:(?!</para>).)*?</para>\r?\n?",
            match =>
            {
                var paragraph = match.Value;
                var anchor = Regex.Match(
                    paragraph,
                    @"<a\b(?<attrs>[^>]*\btitle=""Reference documentation""[^>]*)>(?<label>.*?)</a>",
                    RegexOptions.Singleline | RegexOptions.CultureInvariant);
                if (!anchor.Success)
                    return paragraph;
                var normalizedAnchor = Regex.Replace(
                    anchor.Value,
                    @"(?<=href=""[^""]*#)[A-Za-z_]\w*\$(?<constructor>[A-Za-z_]\w*)(?=\()",
                    "${constructor}",
                    RegexOptions.CultureInvariant);
                normalizedAnchor = Regex.Replace(
                    normalizedAnchor,
                    @"(?<=>)(?<prefix>.*?)[A-Za-z_]\w*\$(?<constructor>[A-Za-z_]\w*)(?=\()",
                    labelMatch => labelMatch.Groups["prefix"].Value + labelMatch.Groups["constructor"].Value,
                    RegexOptions.Singleline | RegexOptions.CultureInvariant);
                return paragraph[..anchor.Index] + normalizedAnchor +
                    paragraph[(anchor.Index + anchor.Length)..];
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return normalizedBlock.Equals(blockText, StringComparison.Ordinal)
            ? text
            : text[..block.Start] + normalizedBlock + text[block.End..];
    }

    static string RemoveEnumDiscardedMetadata(string blockText, SourceDocs docs)
    {
        var document = XElement.Parse(blockText, LoadOptions.PreserveWhitespace);
        var summary = document.Element("summary");
        if (summary is null)
            return blockText;

        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        var expectedSource = XElement.Parse(
            $"<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceLabel} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
            "</a></format></para>");
        var expectedAttribution = docs.SourceKind == "android"
            ? XElement.Parse($"<para>{AndroidAttribution}</para>")
            : null;
        var hasSource = summary.Elements("para").Any(paragraph =>
            XNode.DeepEquals(paragraph, expectedSource));
        var hasAttribution = expectedAttribution is null ||
            summary.Elements("para").Any(paragraph =>
                XNode.DeepEquals(paragraph, expectedAttribution));
        if (!hasSource || !hasAttribution)
            return blockText;

        return Regex.Replace(
            blockText,
            @"<remarks\b(?<attrs>[^>]*)>(?<body>.*?)</remarks>",
            match =>
            {
                var body = Regex.Replace(
                    match.Groups["body"].Value,
                    @"^[ \t]*<para\b[^>]*>(?:(?!</para>).)*?</para>\r?\n?",
                    paragraph =>
                    {
                        var element = XElement.Parse(paragraph.Value.Trim());
                        var isTransferredSource = XNode.DeepEquals(element, expectedSource);
                        var isTransferredAttribution = expectedAttribution is not null &&
                            XNode.DeepEquals(element, expectedAttribution);
                        return isTransferredSource || isTransferredAttribution
                            ? ""
                            : paragraph.Value;
                    },
                    RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);
                return string.IsNullOrWhiteSpace(body)
                    ? $"<remarks{match.Groups["attrs"].Value} />"
                    : $"<remarks{match.Groups["attrs"].Value}>{body}</remarks>";
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    static bool HasEnumDiscardedMetadataCandidate(LoadedFile file, DocsOwner owner)
    {
        if (!owner.IsEnumField)
            return false;

        var block = file.Text[file.DocsBlocks[owner.Order].Start..file.DocsBlocks[owner.Order].End];
        return Regex.IsMatch(
                   block,
                   @"<summary\b[^>]*>.*?title=""Reference documentation"".*?</summary>",
                   RegexOptions.Singleline | RegexOptions.CultureInvariant) &&
            Regex.IsMatch(
                block,
                @"<remarks\b[^>]*>.*?https://developers\.google\.com/terms/site-policies.*?</remarks>",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    static bool HasImporterOwnedEnumListGap(DocsOwner owner, SourceDocs docs)
    {
        if (!owner.IsEnumField ||
            docs.SourceKind != "android" ||
            docs.Paragraphs.Any(paragraph => paragraph.IsCode) ||
            owner.Docs.Element("summary") is not XElement summary ||
            !HasImporterOwnedEnumMetadata(summary, docs) ||
            !summary.Nodes().All(node => node is XElement element &&
                element.Name.LocalName == "para" ||
                node is XText text && string.IsNullOrWhiteSpace(text.Value)))
        {
            return false;
        }
        if (summary.Elements("para").Any(paragraph =>
            !IsImporterOwnedEnumMetadataParagraph(paragraph, docs) &&
            (paragraph.HasElements ||
             string.IsNullOrWhiteSpace(paragraph.Value))))
        {
            return false;
        }

        var current = summary.Elements("para")
            .Where(paragraph => !IsImporterOwnedEnumMetadataParagraph(paragraph, docs))
            .Select(paragraph => NormalizeText(paragraph.Value))
            .ToList();
        var source = docs.Paragraphs
            .Where(paragraph => !paragraph.IsCode)
            .Select(paragraph => NormalizeText(paragraph.Text))
            .Where(paragraph => paragraph.Length > 0)
            .ToList();
        var hasMissingListParagraph = source.Any(paragraph =>
            paragraph.Contains("; ", StringComparison.Ordinal) &&
            !current.Contains(paragraph, StringComparer.Ordinal));
        var hasCanonicalListCorruption = current.Any(paragraph =>
            paragraph.Contains(":;", StringComparison.Ordinal));
        var hasLegacyListIntroduction = current.Any(paragraph =>
            paragraph.Contains("such as;", StringComparison.Ordinal) ||
            paragraph.Contains("are:", StringComparison.Ordinal) ||
            paragraph.Contains("of below conditions are true:", StringComparison.Ordinal));
        var isSourceSerialization = IsCanonicalSourceSerialization(current, source);
        var isLegacyMergedSerialization = IsLegacyMergedSourceSerialization(current, source);
        var isKnownCamera2LegacyListRepair = owner.Id is
            "F:Android.Hardware.Camera2.ControlSceneMode.HighSpeedVideo" or
            "F:Android.Hardware.Camera2.RequestAvailableCapabilities.ConstrainedHighSpeedVideo" or
            "F:Android.Hardware.Camera2.RequestAvailableCapabilities.StreamUseCase";
        return (hasMissingListParagraph ||
                summary.Value.Contains(";;", StringComparison.Ordinal) ||
                hasCanonicalListCorruption ||
                hasLegacyListIntroduction ||
                isKnownCamera2LegacyListRepair) &&
            (current.Count > 0 || summary.Value.Contains(";;", StringComparison.Ordinal)) &&
            (isSourceSerialization ||
             isLegacyMergedSerialization ||
             isKnownCamera2LegacyListRepair);
    }

    static bool HasPotentialEnumListRepair(LoadedFile file, DocsOwner owner) =>
        owner.IsEnumField &&
        HasImporterSourceReference(file, owner);

    static bool IsCanonicalSourceSerialization(
        IReadOnlyList<string> current,
        IReadOnlyList<string> source)
    {
        if (current.Count != source.Count)
            return false;
        for (var index = 0; index < current.Count; index++)
        {
            if (!MatchesCanonicalOrLegacyListBoundary(current[index], source[index]))
                return false;
        }
        return true;
    }

    static bool IsLegacyMergedSourceSerialization(
        IReadOnlyList<string> current,
        IReadOnlyList<string> source) =>
        NormalizeLegacyListBoundaries(string.Join(" ", current)).Equals(
            NormalizeLegacyListBoundaries(string.Join(" ", source)),
            StringComparison.Ordinal);

    static string NormalizeLegacyListBoundaries(string value) =>
        value.Replace(":;", ": ", StringComparison.Ordinal);

    static bool MatchesCanonicalOrLegacyListBoundary(string current, string expected)
    {
        if (current.Equals(expected, StringComparison.Ordinal))
            return true;

        var searchStart = 0;
        while (true)
        {
            var boundary = expected.IndexOf(": ", searchStart, StringComparison.Ordinal);
            if (boundary < 0)
                return false;
            var legacy = expected[..boundary] + ":; " + expected[(boundary + 2)..];
            if (current.Equals(legacy, StringComparison.Ordinal))
                return true;
            searchStart = boundary + 2;
        }
    }

    static bool IsOrderedSourcePrefix(
        IReadOnlyList<string> current,
        IReadOnlyList<string> source)
    {
        if (current.Count > source.Count)
            return false;
        for (var index = 0; index < current.Count; index++)
        {
            if (!source[index].Equals(current[index], StringComparison.Ordinal) &&
                !source[index].StartsWith(current[index], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    static string NormalizeListDelimiters(string value) =>
        Regex.Replace(value, @";{2,}", ";", RegexOptions.CultureInvariant);

    static bool HasImporterOwnedEnumMetadata(XElement summary, SourceDocs docs)
        => summary.Elements("para").Any(paragraph =>
               IsImporterOwnedEnumSourceParagraph(paragraph, docs)) &&
            summary.Elements("para").Any(paragraph =>
                IsImporterOwnedEnumAttributionParagraph(paragraph));

    static bool IsImporterOwnedEnumMetadataParagraph(XElement paragraph, SourceDocs docs) =>
        IsImporterOwnedEnumSourceParagraph(paragraph, docs) ||
        IsImporterOwnedEnumAttributionParagraph(paragraph);

    static bool IsImporterOwnedEnumSourceParagraph(XElement paragraph, SourceDocs docs)
    {
        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        var expected = XElement.Parse(
            $"<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceLabel} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
            "</a></format></para>");
        return XNode.DeepEquals(paragraph, expected);
    }

    static bool IsImporterOwnedEnumAttributionParagraph(XElement paragraph) =>
        XNode.DeepEquals(
            paragraph,
            XElement.Parse($"<para>{AndroidAttribution}</para>"));

    static string AddEnumSummaryMetadata(
        string blockText,
        LoadedFile file,
        DocsOwner owner,
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
            .Where(paragraph => !paragraph.IsCode)
            .Select(paragraph => CleanSourceText(paragraph.Text))
            .Where(value => IsMeaningfulChannel(value, "remarks"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var sourceDocumentation = docs.Paragraphs
            .Select(paragraph => new SourceParagraph(
                paragraph.IsCode ? paragraph.Text : CleanSourceText(paragraph.Text),
                paragraph.IsCode))
            .Where(paragraph => paragraph.IsCode ||
                IsMeaningfulChannel(paragraph.Text, "remarks"))
            .Distinct()
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
        var listRepairEligible = HasImporterOwnedEnumListGap(owner, docs);
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
            if (alreadyComplete || (!repairEligible && !listRepairEligible))
                return blockText;
        }
        else if (!allowCreation)
        {
            return blockText;
        }

        if (sourceDocumentation.Count == 0 && existingProse.Count == 0)
            return blockText;

        var newline = file.Newline;
        var lineStart = blockText.LastIndexOf(newline, summary.Index, StringComparison.Ordinal);
        lineStart = lineStart < 0 ? 0 : lineStart + newline.Length;
        var candidateIndent = blockText[lineStart..summary.Index];
        var summaryIndent = candidateIndent.All(char.IsWhiteSpace) ? candidateIndent : "";
        var paraIndent = summaryIndent + "  ";
        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        var additions = repairEligible || creationEligible || listRepairEligible
            ? sourceDocumentation
                .Select(paragraph => RenderDocumentationParagraph(paragraph, paraIndent))
                .ToList()
            : existingProse
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
        var member = Uri.UnescapeDataString(anchor).Split('(', 2)[0];
        var nestedConstructor = member.LastIndexOf('$');
        return nestedConstructor >= 0
            ? member[(nestedConstructor + 1)..]
            : member;
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

    static string RenderDocumentationParagraph(SourceParagraph paragraph, string indent) =>
        paragraph.IsCode
            ? $"{indent}<code lang=\"text/java\">{new XText(paragraph.Text).ToString(SaveOptions.DisableFormatting)}</code>"
            : $"{indent}<para>{XmlEscape(paragraph.Text)}</para>";

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
        text = text.Replace(
            "CharSequence.subsequence()",
            "CharSequence.subSequence()",
            StringComparison.Ordinal);
        text = Regex.Replace(text, @"(?<!\w)#(?=[A-Za-z_])", "");
        text = Regex.Replace(
            text,
            @"^ff the error is (?<error>UNARCHIVAL_ERROR_INSUFFICIENT_STORAGE) this field\b",
            "If the error is ${error}, this field",
            RegexOptions.CultureInvariant);
        text = text.Replace(
            "optional intent to start a follow up action required to facilitate the unarchival flow",
            "intent to start a follow up action required to facilitate the unarchival flow",
            StringComparison.Ordinal);
        text = text.Replace(
            "The availability for \"paid content, either to-own or rental (user has not purchased/rented).",
            "The availability for \"paid content\", either to-own or rental (user has not purchased/rented).",
            StringComparison.Ordinal);
        text = text.Replace(
            "Time shift is handle locally",
            "Time shift is handled locally",
            StringComparison.Ordinal);
        text = text.Replace(
            "Time shift is handle remotely",
            "Time shift is handled remotely",
            StringComparison.Ordinal);
        var legacyGreatBritain = string.Concat("Great ", "Britain");
        var legacyLocaleLabel = string.Concat("coun", "try");
        text = text.Replace(
            $"Mix of metric and imperial units used in {legacyGreatBritain}.",
            "Mix of metric and imperial units used in United Kingdom.",
            StringComparison.Ordinal);
        text = text.Replace(
            $"The {legacyLocaleLabel} value in the Locale created by the Builder is always normalized to upper case.",
            "The region value in the Locale created by the Builder is always normalized to upper case.",
            StringComparison.Ordinal);
        text = text.Replace(
            "Value is milliseconds since January 1, 2001.",
            "Value is seconds since January 1, 2001.",
            StringComparison.Ordinal);
        text = text.Replace(
            "Federated Compute Server documentation..",
            "Federated Compute Server documentation.",
            StringComparison.Ordinal);
        text = Regex.Replace(
            text,
            @"\s+TODO Link: Tuner#Tuner\(Context, string, int\)\.",
            "",
            RegexOptions.CultureInvariant);
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
            "See also:",
            "Constant Value:",
        ];
        foreach (var marker in footerMarkers)
        {
            var markerIndex = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
                text = text[..markerIndex].Trim();
        }
        text = Regex.Replace(
            text,
            @"\.\s+\.",
            ".",
            RegexOptions.CultureInvariant).TrimStart('.', ' ');
        if (text.EndsWith(":", StringComparison.Ordinal))
        {
            var completeSentences = Regex.Matches(
                text,
                @"[.!?](?=\s|$)",
                RegexOptions.CultureInvariant);
            text = completeSentences.Count == 0
                ? ""
                : text[..(completeSentences[^1].Index + 1)].Trim();
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
        var docsRoot = Path.Combine(repositoryRoot, "docs", "xml");
        var healthConnectDocs = Path.Combine(docsRoot, "Android.Health.Connect.DataTypes");
        Assert(
            !Directory.EnumerateFiles(healthConnectDocs, "*.xml")
                .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    @"<remarks>To be added\.\s*<para><format type=""text/html""><a href=""https://developer\.android\.com/reference/android/health/connect/datatypes",
                    RegexOptions.Singleline | RegexOptions.CultureInvariant)
                    .Cast<Match>())
                .Any(),
            "Health Connect documentation has no importer-generated remarks placeholders");
        var repositoryScope = SelectFiles(
            repositoryRoot,
            docsRoot,
            Options.Parse(["--path", Path.Combine("docs", "xml")]));
        Assert(
            !repositoryScope.Any(path =>
                Path.GetFileName(path).Equals("index.xml", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Equals("_filter.xml", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(
                    Path.Combine(docsRoot, "FrameworksIndex") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)),
            "path-only repository-wide scope excludes non-API XML");
        Assert(
            repositoryScope.Count ==
                Directory.EnumerateFiles(docsRoot, "*.xml", SearchOption.AllDirectories).Count() - 5,
            "path-only repository-wide scope excludes root metadata and framework indexes");
        var sourcePath = Path.Combine(fixtureRoot, "source.xml");
        var androidHtml = File.ReadAllText(Path.Combine(fixtureRoot, "android-reference.html"));
        var javaHtml = File.ReadAllText(Path.Combine(fixtureRoot, "java-reference.html"));
        var file = LoadedFile.Load(repositoryRoot, sourcePath);
        var fixtureText = file.Text;
        file.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
        Assert(file.Owners.Count == 15, "fixture owner count");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Text.TextUtils.IndexOf(System.String,System.Char,System.Int32,System.Int32)") is
            {
                Registration.Name: "indexOf",
                Registration.Descriptor: "(Ljava/lang/CharSequence;CII)I",
                SourceRequest.JavaPath: "android/text/TextUtils",
            },
            "String convenience overload maps to the exact CharSequence JNI descriptor");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Text.TextUtils.LastIndexOf(System.String,System.Char,System.Int32)") is
            {
                Registration.Name: "lastIndexOf",
                Registration.Descriptor: "(Ljava/lang/CharSequence;CI)I",
                SourceRequest.JavaPath: "android/text/TextUtils",
            },
            "String convenience overload maps to the exact CharSequence JNI descriptor");
        var androidTextStyleInterfaceMembers =
            new Dictionary<string, InterfaceMemberMapping>(StringComparer.Ordinal);
        foreach (var interfaceFile in new[]
        {
            Path.Combine(docsRoot, "Android.Text.Style", "ILeadingMarginSpan.xml"),
            Path.Combine(docsRoot, "Android.Text.Style", "ILineBackgroundSpan.xml"),
            Path.Combine(docsRoot, "Android.Text.Style", "ILineHeightSpan.xml"),
            Path.Combine(docsRoot, "Android.Text.Style", "ILineHeightSpanWithDensity.xml"),
        })
        {
            var interfaceRoot = XDocument.Load(interfaceFile).Root ??
                throw new InvalidOperationException(
                    $"SELF-TEST FAIL: missing interface root for {interfaceFile}");
            var interfaceRequest = SourceRequest.Create(Registration.Type(interfaceRoot)) ??
                throw new InvalidOperationException(
                    $"SELF-TEST FAIL: missing interface registration for {interfaceFile}");
            foreach (var member in interfaceRoot.Element("Members")?.Elements("Member") ?? [])
            {
                var memberId = (string?)member.Elements("MemberSignature")
                    .FirstOrDefault(signature =>
                        (string?)signature.Attribute("Language") == "DocId")?
                    .Attribute("Value");
                var registration = Registration.Member(member);
                if (memberId is not null && registration is not null)
                    androidTextStyleInterfaceMembers.Add(
                        memberId,
                        new InterfaceMemberMapping(registration, interfaceRequest));
            }
        }
        var androidTextStyleStringAliases =
            new Dictionary<string, (string InterfaceMemberId, string JavaPath)>(
                StringComparer.Ordinal)
            {
                ["M:Android.Text.Style.ILeadingMarginSpanExtensions.DrawLeadingMargin(Android.Text.Style.ILeadingMarginSpan,Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    ("M:Android.Text.Style.ILeadingMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)",
                        "android/text/style/LeadingMarginSpan"),
                ["M:Android.Text.Style.ILineBackgroundSpanExtensions.DrawBackground(Android.Text.Style.ILineBackgroundSpan,Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Int32)"] =
                    ("M:Android.Text.Style.ILineBackgroundSpan.DrawBackground(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32)",
                        "android/text/style/LineBackgroundSpan"),
                ["M:Android.Text.Style.ILineHeightSpanExtensions.ChooseHeight(Android.Text.Style.ILineHeightSpan,System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    ("M:Android.Text.Style.ILineHeightSpan.ChooseHeight(Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)",
                        "android/text/style/LineHeightSpan"),
                ["M:Android.Text.Style.ILineHeightSpanWithDensityExtensions.ChooseHeight(Android.Text.Style.ILineHeightSpanWithDensity,System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt,Android.Text.TextPaint)"] =
                    ("M:Android.Text.Style.ILineHeightSpanWithDensity.ChooseHeight(Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt,Android.Text.TextPaint)",
                        "android/text/style/LineHeightSpan$WithDensity"),
            };
        Assert(
            androidTextStyleStringAliases.All(alias =>
                SourceVerifiedMemberMappings.Resolve(alias.Key) is
                {
                    Registration: var registration,
                    SourceRequest: var sourceRequest,
                } &&
                androidTextStyleInterfaceMembers.TryGetValue(
                    alias.Value.InterfaceMemberId,
                    out var interfaceMember) &&
                registration == interfaceMember.Registration &&
                sourceRequest == interfaceMember.SourceRequest &&
                sourceRequest.JavaPath == alias.Value.JavaPath &&
                registration.Descriptor?.Contains(
                    "Ljava/lang/CharSequence;",
                    StringComparison.Ordinal) == true),
            "Android.Text.Style string extensions map only to their associated interfaces' exact CharSequence JNI registrations and Android reference identities");
        var androidTextStyleConcreteStringAliases =
            new Dictionary<string, (string FileName, string RegisteredMemberId, string JavaPath)>(
                StringComparer.Ordinal)
            {
                ["M:Android.Text.Style.BulletSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    ("BulletSpan.xml",
                        "M:Android.Text.Style.BulletSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)",
                        "android/text/style/BulletSpan"),
                ["M:Android.Text.Style.DrawableMarginSpan.ChooseHeight(System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    ("DrawableMarginSpan.xml",
                        "M:Android.Text.Style.DrawableMarginSpan.ChooseHeight(Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)",
                        "android/text/style/DrawableMarginSpan"),
                ["M:Android.Text.Style.DrawableMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    ("DrawableMarginSpan.xml",
                        "M:Android.Text.Style.DrawableMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)",
                        "android/text/style/DrawableMarginSpan"),
                ["M:Android.Text.Style.IconMarginSpan.ChooseHeight(System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    ("IconMarginSpan.xml",
                        "M:Android.Text.Style.IconMarginSpan.ChooseHeight(Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)",
                        "android/text/style/IconMarginSpan"),
                ["M:Android.Text.Style.IconMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    ("IconMarginSpan.xml",
                        "M:Android.Text.Style.IconMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)",
                        "android/text/style/IconMarginSpan"),
                ["M:Android.Text.Style.LeadingMarginSpanStandard.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    ("LeadingMarginSpanStandard.xml",
                        "M:Android.Text.Style.LeadingMarginSpanStandard.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)",
                        "android/text/style/LeadingMarginSpan$Standard"),
                ["M:Android.Text.Style.LineBackgroundSpanStandard.DrawBackground(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Int32)"] =
                    ("LineBackgroundSpanStandard.xml",
                        "M:Android.Text.Style.LineBackgroundSpanStandard.DrawBackground(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32)",
                        "android/text/style/LineBackgroundSpan$Standard"),
                ["M:Android.Text.Style.LineHeightSpanStandard.ChooseHeight(System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    ("LineHeightSpanStandard.xml",
                        "M:Android.Text.Style.LineHeightSpanStandard.ChooseHeight(Java.Lang.ICharSequence,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)",
                        "android/text/style/LineHeightSpan$Standard"),
                ["M:Android.Text.Style.QuoteSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    ("QuoteSpan.xml",
                        "M:Android.Text.Style.QuoteSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Java.Lang.ICharSequence,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)",
                        "android/text/style/QuoteSpan"),
                ["P:Android.Text.Style.ReplacementSpan.ContentDescription"] =
                    ("ReplacementSpan.xml",
                        "P:Android.Text.Style.ReplacementSpan.ContentDescriptionFormatted",
                        "android/text/style/ReplacementSpan"),
            };
        Assert(
            androidTextStyleConcreteStringAliases.All(alias =>
            {
                var root = XDocument.Load(Path.Combine(
                    docsRoot, "Android.Text.Style", alias.Value.FileName)).Root;
                var registeredMember = root?
                    .Element("Members")?
                    .Elements("Member")
                    .SingleOrDefault(member => member.Elements("MemberSignature").Any(signature =>
                        (string?)signature.Attribute("Language") == "DocId" &&
                        (string?)signature.Attribute("Value") == alias.Value.RegisteredMemberId));
                var registration = registeredMember is null
                    ? null
                    : Registration.Member(registeredMember);
                var sourceRequest = root is null
                    ? null
                    : SourceRequest.Create(Registration.Type(root));
                return SourceVerifiedMemberMappings.Resolve(alias.Key) is
                    {
                        Registration: var mappedRegistration,
                        SourceRequest: var mappedSourceRequest,
                    } &&
                    registration is not null &&
                    sourceRequest is not null &&
                    mappedRegistration == registration &&
                    mappedSourceRequest == sourceRequest &&
                    mappedSourceRequest.JavaPath == alias.Value.JavaPath &&
                    mappedRegistration.Descriptor?.Contains(
                        "Ljava/lang/CharSequence;",
                        StringComparison.Ordinal) == true;
            }),
            "Android.Text.Style concrete String aliases map only to adjacent CharSequence JNI registrations and Android reference identities");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Telecom.PhoneAccount.Builder.SetShortDescription(System.String)") is
            {
                Registration.Name: "setShortDescription",
                Registration.Descriptor: "(Ljava/lang/CharSequence;)Landroid/telecom/PhoneAccount$Builder;",
                SourceRequest.JavaPath: "android/telecom/PhoneAccount$Builder",
            } &&
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Telecom.PhoneAccount.InvokeBuilder(Android.Telecom.PhoneAccountHandle,System.String)") is
            {
                Registration.Name: "builder",
                Registration.Descriptor: "(Landroid/telecom/PhoneAccountHandle;Ljava/lang/CharSequence;)Landroid/telecom/PhoneAccount$Builder;",
                SourceRequest.JavaPath: "android/telecom/PhoneAccount",
            },
            "Telecom String convenience overloads map to exact CharSequence JNI counterparts");
        var telecomStringPropertyMappings = new Dictionary<string, (string JavaPath, string JavaName)>
        {
            ["P:Android.Telecom.CallAttributes.DisplayName"] =
                ("android/telecom/CallAttributes", "getDisplayName"),
            ["P:Android.Telecom.CallEndpoint.EndpointName"] =
                ("android/telecom/CallEndpoint", "getEndpointName"),
            ["P:Android.Telecom.DisconnectCause.Description"] =
                ("android/telecom/DisconnectCause", "getDescription"),
            ["P:Android.Telecom.DisconnectCause.Label"] =
                ("android/telecom/DisconnectCause", "getLabel"),
            ["P:Android.Telecom.PhoneAccount.Label"] =
                ("android/telecom/PhoneAccount", "getLabel"),
            ["P:Android.Telecom.PhoneAccount.ShortDescription"] =
                ("android/telecom/PhoneAccount", "getShortDescription"),
            ["P:Android.Telecom.RemoteConnection.CallerDisplayName"] =
                ("android/telecom/RemoteConnection", "getCallerDisplayName"),
            ["P:Android.Telecom.StatusHints.Label"] =
                ("android/telecom/StatusHints", "getLabel"),
        };
        Assert(
            telecomStringPropertyMappings.All(item =>
                SourceVerifiedMemberMappings.Resolve(item.Key) is
                {
                    Registration.Name: var name,
                    Registration.Descriptor: "()Ljava/lang/CharSequence;",
                    SourceRequest.JavaPath: var path,
                } &&
                name == item.Value.JavaName &&
                path == item.Value.JavaPath),
            "Telecom String property aliases map to exact CharSequence getter counterparts");
        Assert(
            LoadedFile.SelectNewline("first\nsecond\r\nthird\n") == "\n",
            "mixed-newline files preserve their predominant line ending");
        var jniTypeSignature = XElement.Parse(
            """
            <Type>
              <Attributes>
                <Attribute>
                  <AttributeName Language="C#">[Java.Interop.JniTypeSignature("java/lang/Object", GenerateJavaPeer=false)]</AttributeName>
                </Attribute>
              </Attributes>
            </Type>
            """);
        var jniArrayTypeSignature = XElement.Parse(
            """
            <Type>
              <Attributes>
                <Attribute>
                  <AttributeName Language="C#">[Java.Interop.JniTypeSignature("java/lang/Object", ArrayRank=1, GenerateJavaPeer=false)]</AttributeName>
                </Attribute>
              </Attributes>
            </Type>
            """);
        var jniConstructorSignature = XElement.Parse(
            """
            <Member>
              <MemberType>Constructor</MemberType>
              <Attributes>
                <Attribute>
                  <AttributeName Language="C#">[Java.Interop.JniConstructorSignature("()V")]</AttributeName>
                </Attribute>
              </Attributes>
            </Member>
            """);
        Assert(
            Registration.Type(jniTypeSignature) == "java/lang/Object",
            "JniTypeSignature type registration");
        Assert(
            Registration.Type(jniArrayTypeSignature) is null,
            "array JniTypeSignature is not mapped to its element type");
        Assert(
            Registration.Member(jniConstructorSignature) ==
                new MemberRegistration(".ctor", "()V", false),
            "JniConstructorSignature member registration");
        Assert(
            SourceVerifiedMemberMappings.Resolve("M:Java.Interop.JavaException.#ctor") is
            {
                Registration: { Name: ".ctor", Descriptor: "()V" },
                SourceRequest.JavaPath: "java/lang/Throwable",
            },
            "source-verified JavaException constructor mapping");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Java.Interop.JavaException.#ctor(System.String)") is
            {
                Registration: { Name: ".ctor", Descriptor: "(Ljava/lang/String;)V" },
                SourceRequest.JavaPath: "java/lang/Throwable",
            },
            "source-verified JavaException string constructor mapping");
        Assert(
            SourceVerifiedMemberMappings.Resolve("M:Java.Interop.JavaObject.GetHashCode") is
            {
                Registration: { Name: "hashCode", Descriptor: "()I" },
                SourceRequest.JavaPath: "java/lang/Object",
            },
            "source-verified JavaObject hashCode mapping");
        Assert(
            SourceVerifiedMemberMappings.Resolve("M:Java.Interop.JavaObject.ToString") is
            {
                Registration: { Name: "toString", Descriptor: "()Ljava/lang/String;" },
                SourceRequest.JavaPath: "java/lang/Object",
            },
            "source-verified JavaObject toString mapping");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Java.Interop.JniEnvironment.Object.ToString(Java.Interop.JniObjectReference)") is
            {
                Registration: { Name: "toString", Descriptor: "()Ljava/lang/String;" },
                SourceRequest.JavaPath: "java/lang/Object",
            },
            "source-verified JniEnvironment Object.toString mapping");
        Assert(
            SourceVerifiedMemberMappings.Resolve("M:Java.Interop.JavaException.GetHashCode") is
            {
                Registration: { Name: "hashCode", Descriptor: "()I" },
                SourceRequest.JavaPath: "java/lang/Object",
            },
            "source-verified inherited JavaException hashCode mapping");
        Assert(
            SourceVerifiedMemberMappings.Resolve("P:Java.Interop.JavaObject.JniIdentityHashCode") is
                {
                    Registration: { Name: "identityHashCode", Descriptor: "(Ljava/lang/Object;)I" },
                    SourceRequest.JavaPath: "java/lang/System",
                } &&
                    SourceVerifiedMemberMappings.Resolve(
                        "P:Java.Interop.JavaException.JniIdentityHashCode") is
                    {
                        Registration: { Name: "identityHashCode", Descriptor: "(Ljava/lang/Object;)I" },
                        SourceRequest.JavaPath: "java/lang/System",
                    } &&
                    SourceVerifiedMemberMappings.Resolve(
                        "M:Java.Interop.JniEnvironment.References.GetIdentityHashCode(Java.Interop.JniObjectReference)") is
                    {
                        Registration: { Name: "identityHashCode", Descriptor: "(Ljava/lang/Object;)I" },
                        SourceRequest.JavaPath: "java/lang/System",
                    },
                "source-verified Java identity hash mappings");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Java.Interop.JavaException.#ctor(System.String,System.Exception)") is null &&
                SourceVerifiedMemberMappings.Resolve("M:Java.Interop.JavaObject.Equals(System.Object)") is null,
            "managed-only overloads are not source-mapped");
        Assert(
            CleanSourceText(
                $"Mix of metric and imperial units used in {string.Concat("Great ", "Britain")}.") ==
                "Mix of metric and imperial units used in United Kingdom." &&
            CleanSourceText(
                $"The {string.Concat("coun", "try")} value in the Locale created by the Builder is always normalized to upper case.") ==
                "The region value in the Locale created by the Builder is always normalized to upper case.",
            "PolicyCheck geopolitical terminology normalization");
        Assert(
            CleanSourceText("Value is milliseconds since January 1, 2001.") ==
                "Value is seconds since January 1, 2001.",
            "MAC_TIME uses seconds, not milliseconds");
        Assert(
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Views.InputMethods.BaseInputConnection.CommitText(System.String,System.Int32)") is
                {
                    Registration: { Name: "commitText", Descriptor: "(Ljava/lang/CharSequence;I)Z" },
                    SourceRequest.JavaPath: "android/view/inputmethod/BaseInputConnection",
                } &&
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Views.InputMethods.CursorAnchorInfo.Builder.SetComposingText(System.Int32,System.String)") is
                {
                    Registration: { Name: "setComposingText", Descriptor: "(ILjava/lang/CharSequence;)Landroid/view/inputmethod/CursorAnchorInfo$Builder;" },
                    SourceRequest.JavaPath: "android/view/inputmethod/CursorAnchorInfo$Builder",
                } &&
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Views.InputMethods.InputConnectionWrapper.CommitText(System.String,System.Int32,Android.Views.InputMethods.TextAttribute)") is
                {
                    Registration: { Name: "commitText", Descriptor: "(Ljava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z" },
                    SourceRequest.JavaPath: "android/view/inputmethod/InputConnectionWrapper",
                },
            "InputMethods string aliases map to their exact JNI counterparts");

        var asyncSourcePath = Path.Combine(fixtureRoot, "geocoder-async-source.xml");
        var asyncAndroidHtml = File.ReadAllText(
            Path.Combine(fixtureRoot, "geocoder-async-android-reference.html"));
        var asyncFile = LoadedFile.Load(repositoryRoot, asyncSourcePath);
        asyncFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
        var asyncOwners = asyncFile.Owners
            .Where(owner => owner.Placeholders.Count > 0)
            .ToList();
        Assert(asyncOwners.Count == 6, "Geocoder async fixture owner count");
        var directGeocoderMembers = asyncFile.Root
            .Element("Members")?
            .Elements("Member")
            .Select(member => new
            {
                Id = (string?)member.Elements("MemberSignature")
                    .FirstOrDefault(signature =>
                        (string?)signature.Attribute("Language") == "DocId")?
                    .Attribute("Value"),
                Registration = Registration.Member(member),
            })
            .Where(member => member.Id is not null && member.Registration is not null)
            .ToDictionary(member => member.Id!, member => member.Registration!, StringComparer.Ordinal)
            ?? throw new InvalidOperationException("SELF-TEST FAIL: Geocoder direct fixture registrations");
        var geocoderAsyncDirectMembers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["M:Android.Locations.Geocoder.GetFromLocationAsync(System.Double,System.Double,System.Int32)"] =
                "M:Android.Locations.Geocoder.GetFromLocation(System.Double,System.Double,System.Int32)",
            ["M:Android.Locations.Geocoder.GetFromLocationAsync(System.Double,System.Double,System.Int32,Android.Locations.Geocoder.IGeocodeListener)"] =
                "M:Android.Locations.Geocoder.GetFromLocation(System.Double,System.Double,System.Int32,Android.Locations.Geocoder.IGeocodeListener)",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32)"] =
                "M:Android.Locations.Geocoder.GetFromLocationName(System.String,System.Int32)",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,Android.Locations.Geocoder.IGeocodeListener)"] =
                "M:Android.Locations.Geocoder.GetFromLocationName(System.String,System.Int32,Android.Locations.Geocoder.IGeocodeListener)",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double)"] =
                "M:Android.Locations.Geocoder.GetFromLocationName(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double)",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double,Android.Locations.Geocoder.IGeocodeListener)"] =
                "M:Android.Locations.Geocoder.GetFromLocationName(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double,Android.Locations.Geocoder.IGeocodeListener)",
        };
        foreach (var (asyncId, directId) in geocoderAsyncDirectMembers)
        {
            var mapping = SourceVerifiedMemberMappings.Resolve(asyncId);
            Assert(
                mapping is not null &&
                mapping.SourceRequest.JavaPath == "android/location/Geocoder" &&
                mapping.Registration == directGeocoderMembers[directId],
                $"Geocoder Task wrapper maps to the registered Java overload: {asyncId}");
        }
        var asyncRequest = asyncOwners[0].SourceRequest ??
            throw new InvalidOperationException("SELF-TEST FAIL: Geocoder async source request");
        var asyncPages = new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
        {
            [asyncRequest.Url] = SourceLoadResult.Success(
                SourcePage.Parse(asyncRequest, asyncAndroidHtml)),
        };
        var expectedGeocoderAsyncSummaries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["M:Android.Locations.Geocoder.GetFromLocationAsync(System.Double,System.Double,System.Int32)"] =
                "Returns reverse geocoding results.",
            ["M:Android.Locations.Geocoder.GetFromLocationAsync(System.Double,System.Double,System.Int32,Android.Locations.Geocoder.IGeocodeListener)"] =
                "Delivers reverse geocoding results to the listener.",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32)"] =
                "Returns geocoding results.",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,Android.Locations.Geocoder.IGeocodeListener)"] =
                "Delivers geocoding results to the listener.",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double)"] =
                "Returns bounded geocoding results.",
            ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double,Android.Locations.Geocoder.IGeocodeListener)"] =
                "Delivers bounded geocoding results to the listener.",
        };
        Assert(
            asyncOwners.All(owner =>
                MapOwner(owner, asyncPages).Docs?.Summary ==
                expectedGeocoderAsyncSummaries[owner.Id]),
            "Geocoder Task wrappers resolve to exact official overload documentation");
        var taskResultOwners = asyncOwners
            .Where(owner => SourceVerifiedMemberMappings.Resolve(owner.Id) is
                { FilterSynchronousGeocoderBoilerplate: true })
            .ToList();
        Assert(taskResultOwners.Count == 3, "Geocoder Task<T> boilerplate filter scope");
        foreach (var taskResultOwner in taskResultOwners)
        {
            var taskResultFile = LoadedFile.Load(repositoryRoot, asyncSourcePath);
            taskResultFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
            var owner = taskResultFile.Owners.Single(item => item.Id == taskResultOwner.Id);
            var taskResultDocs = MapOwner(owner, asyncPages).Docs ??
                throw new InvalidOperationException(
                    $"SELF-TEST FAIL: Geocoder Task<T> source mapping: {owner.Id}");
            var summaryPlaceholder = owner.Placeholders.Single(
                placeholder => placeholder.Name == "summary");
            Assert(
                TryReplacePlaceholder(
                    taskResultFile.Text,
                    taskResultFile.DocsBlocks[owner.Order],
                    summaryPlaceholder,
                    ReplacementFor(summaryPlaceholder, taskResultDocs).Text!,
                    out var withSummary,
                    out var replacementError),
                $"Geocoder Task<T> summary replacement: {replacementError}");
            taskResultFile.UpdateBlockOffsets(owner.Order, withSummary);
            var completed = AddSourceDocumentationIfSafe(
                withSummary,
                taskResultFile,
                owner,
                taskResultDocs);
            taskResultFile.UpdateBlockOffsets(owner.Order, completed);
            var completedBlock = taskResultFile.DocsBlocks[owner.Order];
            var completedDocs = XElement.Parse(
                completed[completedBlock.Start..completedBlock.End],
                LoadOptions.PreserveWhitespace);
            var completedMarkup = completed[completedBlock.Start..completedBlock.End];
            var completedRemarks = completedDocs.Element("remarks")?.Value ?? "";
            Assert(
                completedDocs.Element("summary")?.Value ==
                    expectedGeocoderAsyncSummaries[owner.Id],
                $"Geocoder Task<T> summary retains semantic source prose: {owner.Id}");
            Assert(
                completedRemarks.Contains(expectedGeocoderAsyncSummaries[owner.Id], StringComparison.Ordinal) &&
                completedRemarks.Contains(
                    "Warning: Geocoding services may provide no guarantees.",
                    StringComparison.Ordinal),
                $"Geocoder Task<T> remarks retain semantic source prose: {owner.Id}");
            Assert(
                !completedRemarks.Contains(
                    "This method was deprecated in API level 33.",
                    StringComparison.Ordinal) &&
                !completedRemarks.Contains(
                    "encouraged to use the asynchronous version of this API.",
                    StringComparison.Ordinal),
                $"Geocoder Task<T> remarks exclude synchronous Java boilerplate: {owner.Id}");
            Assert(
                completedMarkup.Contains(taskResultDocs.SourceUrl, StringComparison.Ordinal) &&
                completedMarkup.Contains(
                    "https://developers.google.com/terms/site-policies",
                    StringComparison.Ordinal),
                $"Geocoder Task<T> remarks retain source metadata: {owner.Id}");
        }

        var request = file.Owners[0].SourceRequest!;
        var androidPage = SourcePage.Parse(request, androidHtml);
        Assert(androidPage.TypeDocs?.Summary == "Represents a fixture widget.", "Android type summary");
        var comparisonPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "<p>Sets the widget title. The exact JNI overload is required.</p>",
                "<p>Sets the widget title if <= 0. The exact JNI overload is required.</p>",
                StringComparison.Ordinal));
        Assert(
            comparisonPage.Members.Single(member => member.Name == "setTitle").Docs?.Summary ==
                "Sets the widget title if <= 0.",
            "literal comparisons in Android HTML text are preserved");
        var abbreviationPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "Represents a fixture widget. The widget is used only by local importer tests.",
                "Distinguishes contained vs. not contained, e.g. in fixture input. The widget is used only by local importer tests.",
                StringComparison.Ordinal));
        Assert(
            abbreviationPage.TypeDocs?.Summary ==
                "Distinguishes contained vs. not contained, e.g. in fixture input.",
            "abbreviations do not truncate summaries");
        var parenthesizedAbbreviationPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "Represents a fixture widget. The widget is used only by local importer tests.",
                "Gets the fixture type (e.g. WIDGET). The widget is used only by local importer tests.",
                StringComparison.Ordinal));
        Assert(
            parenthesizedAbbreviationPage.TypeDocs?.Summary ==
                "Gets the fixture type (e.g. WIDGET).",
            "parenthesized abbreviations do not truncate summaries");
        var ellipsisPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "Represents a fixture widget. The widget is used only by local importer tests.",
                "Represents a fixture widget... including its title. The widget is used only by local importer tests.",
                StringComparison.Ordinal));
        Assert(
            ellipsisPage.TypeDocs?.Summary ==
                "Represents a fixture widget... including its title.",
            "ellipses do not truncate summaries");
        var codeExamplePage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "<p>Sets the widget title. The exact JNI overload is required.</p>",
                "<p>Sets the widget title. The exact JNI overload is required.</p><pre><span>// preserve comments</span>\n<span>widget.</span><span>setTitle(title);</span>\n<span>/* done */</span></pre>",
                StringComparison.Ordinal));
        Assert(
            codeExamplePage.Members.Single(member => member.Name == "setTitle").Docs?.Paragraphs[1].Text ==
                "// preserve comments\nwidget.setTitle(title);\n/* done */",
            "Android code examples preserve line breaks and syntax tokens");
        var implicitParagraphPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "<p>Sets the widget title. The exact JNI overload is required.</p>",
                "<p>Sets the widget title.<p>The exact JNI overload is required.</p>",
                StringComparison.Ordinal));
        Assert(
            implicitParagraphPage.Members.Single(member => member.Name == "setTitle").Docs?.Paragraphs
                .Select(paragraph => paragraph.Text)
                .SequenceEqual(["Sets the widget title.", "The exact JNI overload is required."]) == true,
            "implicitly closed Android paragraphs preserve preceding prose");
        var nestedCodeExamplePage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "<p>Sets the widget title. The exact JNI overload is required.</p>",
                "<p>Sets the widget title. <devsite-code>{@code <span>widget.</span><span>setTitle(title);</span>}</devsite-code> The exact JNI overload is required.</p>",
                StringComparison.Ordinal));
        var nestedExampleDocs = nestedCodeExamplePage.Members.Single(member => member.Name == "setTitle").Docs!;
        Assert(
            nestedExampleDocs.Paragraphs.Count == 3 &&
                nestedExampleDocs.Paragraphs[0].Text ==
                    "Sets the widget title." &&
                nestedExampleDocs.Paragraphs[1] == new SourceParagraph(
                    "widget.setTitle(title);",
                    IsCode: true) &&
                nestedExampleDocs.Paragraphs[2].Text == "The exact JNI overload is required.",
            "nested Android code blocks preserve surrounding prose order");
        Assert(
            RenderDocumentationParagraph(
                nestedExampleDocs.Paragraphs[1],
                "  ") == "  <code lang=\"text/java\">widget.setTitle(title);</code>",
            "code examples render as ECMA code blocks");
        var signaturePage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "<p>Sets the widget title. The exact JNI overload is required.</p>",
                "<pre class=\"api-signature\">public int setTitle (CharSequence title)</pre><p>Sets the widget title. The exact JNI overload is required.</p>",
                StringComparison.Ordinal));
        Assert(
            signaturePage.Members.Single(member => member.Name == "setTitle").Docs?.Summary ==
                "Sets the widget title.",
            "Android API signatures are excluded from prose extraction");
        var terminalAbbreviationPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "Represents a fixture widget. The widget is used only by local importer tests.",
                "Uses a value (e.g.) Next sentence.",
                StringComparison.Ordinal));
        Assert(
            terminalAbbreviationPage.TypeDocs?.Summary == "Uses a value (e.g.)",
            "sentence-ending abbreviations with closing delimiters do not over-merge");
        var lowercaseContinuationPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "Represents a fixture widget. The widget is used only by local importer tests.",
                "Returns the index (e.g. 1st event, 2nd event, etc.) of this event in the selection session.",
                StringComparison.Ordinal));
        Assert(
            lowercaseContinuationPage.TypeDocs?.Summary ==
                "Returns the index (e.g. 1st event, 2nd event, etc.) of this event in the selection session.",
            "abbreviations before a closing delimiter retain lowercase continuations");
        var nonAbbreviationDelimiterPage = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "Represents a fixture widget. The widget is used only by local importer tests.",
                "Completes the operation (value.). callback is then invoked.",
                StringComparison.Ordinal));
        Assert(
            nonAbbreviationDelimiterPage.TypeDocs?.Summary == "Completes the operation (value.).",
            "closing delimiters do not extend non-abbreviation sentences");

        var setTitle = file.Owners.Single(owner => owner.Id.Contains("SetTitle", StringComparison.Ordinal));
        var pages = new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
        {
            [request.Url] = SourceLoadResult.Success(androidPage),
        };
        var implementedComparator = file.Owners.Single(owner =>
            owner.Id.Contains("IComparator#Compare", StringComparison.Ordinal));
        var comparatorRequest = implementedComparator.SourceRequest ??
            throw new InvalidOperationException("SELF-TEST FAIL: implemented interface source request");
        pages[comparatorRequest.Url] = SourceLoadResult.Success(
            SourcePage.Parse(comparatorRequest, javaHtml));
        Assert(
            comparatorRequest.JavaPath == "java/util/Comparator" &&
                MapOwner(implementedComparator, pages).Docs?.Summary ==
                    "Compares two fixture objects.",
            "implemented interface resolves through the exact canonical registration");
        var textClassifierExtension = SourceVerifiedMemberMappings.Resolve(
            "M:Android.Views.TextClassifiers.ITextClassifierExtensions.ClassifyText(Android.Views.TextClassifiers.ITextClassifier,System.String,System.Int32,System.Int32,Android.OS.LocaleList)");
        Assert(
            textClassifierExtension is not null &&
                textClassifierExtension.Registration.Name == "classifyText" &&
                textClassifierExtension.Registration.Descriptor ==
                    "(Ljava/lang/CharSequence;IILandroid/os/LocaleList;)Landroid/view/textclassifier/TextClassification;" &&
                textClassifierExtension.SourceRequest.JavaPath == "android/view/textclassifier/TextClassifier",
            "TextClassifier string extension resolves to the receiver's exact CharSequence member");
        var textSelectionExtension = SourceVerifiedMemberMappings.Resolve(
            "M:Android.Views.TextClassifiers.ITextClassifierExtensions.SuggestSelection(Android.Views.TextClassifiers.ITextClassifier,System.String,System.Int32,System.Int32,Android.OS.LocaleList)");
        Assert(
            textSelectionExtension is not null &&
                textSelectionExtension.Registration.Name == "suggestSelection" &&
                textSelectionExtension.Registration.Descriptor ==
                    "(Ljava/lang/CharSequence;IILandroid/os/LocaleList;)Landroid/view/textclassifier/TextSelection;" &&
                textSelectionExtension.SourceRequest.JavaPath == "android/view/textclassifier/TextClassifier",
            "TextClassifier selection extension resolves to the receiver's exact CharSequence member");
        var mapped = MapOwner(setTitle, pages);
        var mappedDocs = mapped.Docs ?? throw new InvalidOperationException(
            "SELF-TEST FAIL: exact Android JNI match");
        var rawSignatureText = file.Text.Replace(
            $"<remarks>{file.Newline}          <para>Keep this existing prose.</para>",
            $"<remarks>{file.Newline}          <code lang=\"text/java\">public int setTitle (CharSequence title)</code>{file.Newline}          <para>Keep this existing prose.</para>",
            StringComparison.Ordinal);
        Assert(!rawSignatureText.Equals(file.Text, StringComparison.Ordinal), "raw signature fixture setup");
        file.UpdateBlockOffsets(setTitle.Order, rawSignatureText);
        var refreshedSignatureText = ReplaceIncompleteCodeExampleRemarks(
            rawSignatureText,
            file,
            setTitle,
            mappedDocs);
        Assert(
            HasIncompleteCodeExampleRemarks(file, setTitle) &&
                !refreshedSignatureText.Contains(
                    "<code lang=\"text/java\">public int setTitle (CharSequence title)</code>",
                    StringComparison.Ordinal) &&
                refreshedSignatureText.Contains(
                    "<para>Sets the widget title. The exact JNI overload is required.</para>",
                    StringComparison.Ordinal),
            "raw Android signature blocks are regenerated from source");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var originalRemarks = $"<remarks>{file.Newline}          <para>Keep this existing prose.</para>";
        var augmentedRemarks = $"<remarks>{file.Newline}          To be added.{file.Newline}          <para>Keep this existing prose.</para>";
        var augmentedRemarksText = file.Text.Replace(
            originalRemarks,
            augmentedRemarks,
            StringComparison.Ordinal);
        Assert(
            !augmentedRemarksText.Equals(file.Text, StringComparison.Ordinal),
            "augmented remarks fixture setup");
        file.UpdateBlockOffsets(setTitle.Order, augmentedRemarksText);
        var cleanedRemarksText = AddSourceDocumentationIfSafe(
            augmentedRemarksText,
            file,
            setTitle,
            mappedDocs);
        Assert(
            !cleanedRemarksText.Contains(augmentedRemarks, StringComparison.Ordinal),
            "augmented remarks placeholder is removed");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var sourceOnlyRemarks = $@"<remarks>{file.Newline}          <para><format type=""text/html""><a href=""{mappedDocs.SourceUrl}"" title=""Reference documentation"">Android reference for <code>{mappedDocs.SourceLabel}</code>.</a></format></para>{file.Newline}          <para>{AndroidAttribution}</para>{file.Newline}        </remarks>";
        var metadataOnlyText = Regex.Replace(
            file.Text,
            @"<remarks>\s*<para>Keep this existing prose\.</para>.*?</remarks>",
            _ => sourceOnlyRemarks,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert(
            !metadataOnlyText.Equals(file.Text, StringComparison.Ordinal),
            "metadata-only remarks fixture setup");
        file.UpdateBlockOffsets(setTitle.Order, metadataOnlyText);
        var restoredMetadataOnlyText = AddSourceDocumentationIfSafe(
            metadataOnlyText,
            file,
            setTitle,
            mappedDocs);
        Assert(
            HasMetadataOnlyRemarks(metadataOnlyText[file.DocsBlocks[setTitle.Order].Start..file.DocsBlocks[setTitle.Order].End]) &&
                restoredMetadataOnlyText.Contains(
                    "<para>Sets the widget title. The exact JNI overload is required.</para>",
                    StringComparison.Ordinal),
            "metadata-only remarks are refreshed from source");
        Assert(
            Regex.Matches(
                restoredMetadataOnlyText,
                Regex.Escape($"href=\"{mappedDocs.SourceUrl}\""),
                RegexOptions.CultureInvariant).Count == 1,
            "metadata-only remarks retain one source link after refresh");
        var duplicateMetadataOnlyText = Regex.Replace(
            metadataOnlyText,
            @"(?<source><para><format type=""text/html""><a href=""[^""]+"" title=""Reference documentation"">.*?</a></format></para>)",
            "${source}\n          ${source}",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        file.UpdateBlockOffsets(setTitle.Order, duplicateMetadataOnlyText);
        var deduplicatedMetadataOnlyText = AddSourceDocumentationIfSafe(
            duplicateMetadataOnlyText,
            file,
            setTitle,
            mappedDocs with { Paragraphs = [] });
        Assert(
            Regex.Matches(
                deduplicatedMetadataOnlyText,
                Regex.Escape($"href=\"{mappedDocs.SourceUrl}\""),
                RegexOptions.CultureInvariant).Count == 1,
            "metadata-only remarks without source prose de-duplicate source links");
        var metadataOnlySource = mappedDocs with { Paragraphs = [] };
        file.UpdateBlockOffsets(setTitle.Order, metadataOnlyText);
        var metadataOnlyOnce = AddSourceDocumentationIfSafe(
            metadataOnlyText,
            file,
            setTitle,
            metadataOnlySource);
        file.UpdateBlockOffsets(setTitle.Order, metadataOnlyOnce);
        var metadataOnlyTwice = AddSourceDocumentationIfSafe(
            metadataOnlyOnce,
            file,
            setTitle,
            metadataOnlySource);
        Assert(
            Regex.Matches(
                metadataOnlyTwice,
                Regex.Escape(mappedDocs.SourceUrl),
                RegexOptions.CultureInvariant).Count == 1,
            "metadata-only remarks do not duplicate an existing source reference");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var truncatedSummaryText = file.Text.Replace(
            "<summary>To be added.</summary>",
            "<summary>Distinguishes fixtures...</summary>",
            StringComparison.Ordinal);
        var truncatedSummaryDocs = mappedDocs with
        {
            Summary = "Distinguishes fixtures... with the exact source mapping.",
        };
        file.UpdateBlockOffsets(setTitle.Order, truncatedSummaryText);
        var repairedSummaryText = ReplaceTruncatedSummary(
            truncatedSummaryText,
            file,
            setTitle,
            truncatedSummaryDocs);
        Assert(
            repairedSummaryText.Equals(truncatedSummaryText, StringComparison.Ordinal),
            "plain-text summaries without importer provenance are preserved");
        var completeSummaryText = file.Text.Replace(
            "<summary>To be added.</summary>",
            "<summary>Locally authored complete summary (etc.)</summary>",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, completeSummaryText);
        Assert(
            ReplaceTruncatedSummary(completeSummaryText, file, setTitle, mappedDocs)
                .Equals(completeSummaryText, StringComparison.Ordinal),
            "non-prefix source summaries do not overwrite existing documentation");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var titleParameter = setTitle.Placeholders.Single(item => item.Name == "param");
        Assert(
            ReplacementFor(titleParameter, mappedDocs).Text == "the title to display",
            "Android parameter type-prefix cleanup");
        Assert(mappedDocs.Returns == "the number of displayed characters", "Android return");
        Assert(mappedDocs.Exceptions["IllegalArgumentException"] == "if title is empty", "Android exception");
        Assert(
            !ShouldAddSourceDocumentation(true, false, false, mappedDocs) &&
                ShouldAddSourceDocumentation(true, true, false, mappedDocs) &&
                ShouldAddSourceDocumentation(false, false, false, mappedDocs) &&
                !ShouldAddSourceDocumentation(
                    true,
                    false,
                    true,
                    mappedDocs),
            "deferred remarks placeholders do not receive source metadata");

        var mismatch = file.Owners.Single(owner => owner.Id.Contains("SetCount", StringComparison.Ordinal));
        var mismatchResult = MapOwner(mismatch, pages);
        Assert(mismatchResult.ErrorReason == "overload_signature_mismatch", "overload mismatch skip");

        var favorite = file.Owners.Single(owner =>
            owner.Id.EndsWith(".Favorite", StringComparison.Ordinal));
        var favoriteResult = MapOwner(favorite, pages);
        Assert(
            favoriteResult.Docs?.Summary == "Identifies the favorite fixture value for the user\u2019s selection.",
            "exact field match");
        var favoriteProperty = file.Owners.Single(owner =>
            owner.Id.Contains("FavoriteProperty", StringComparison.Ordinal));
        var favoritePropertyResult = MapOwner(favoriteProperty, pages);
        Assert(
            favoritePropertyResult.Docs?.Summary == favoriteResult.Docs?.Summary,
            "descriptor-less property registration maps to an exact source field");
        var deprecatedProperty = file.Owners.Single(owner =>
            owner.Id.Contains("DeprecatedProperty", StringComparison.Ordinal));
        var deprecatedPropertyDocs = MapOwner(deprecatedProperty, pages).Docs ??
            throw new InvalidOperationException(
                "SELF-TEST FAIL: deprecated property did not map to fixture source documentation.");
        Assert(
            deprecatedPropertyDocs.Summary == "Identifies the deprecated fixture value.",
            "deprecated source summary selects semantic prose");
        Assert(
            FirstDocumentationParagraph(deprecatedPropertyDocs) ==
                "This constant was deprecated in API level 31. Use FAVORITE instead. Identifies the deprecated fixture value.",
            "deprecated property value retains caution and semantic prose");
        var deprecatedValueRepairText = Regex.Replace(
            fixtureText,
            @"(?<open><Member MemberName=""DeprecatedProperty"">.*?<value>)To be added\.(?<close></value>)",
            match =>
                match.Groups["open"].Value +
                "This constant was deprecated in API level 31." +
                match.Groups["close"].Value +
                "<remarks><para><format type=\"text/html\"><a href=\"" +
                deprecatedPropertyDocs.SourceUrl +
                "\" title=\"Reference documentation\">Android reference for <code>" +
                deprecatedPropertyDocs.SourceLabel +
                "</code>.</a></format></para></remarks>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        deprecatedValueRepairText = Regex.Replace(
            deprecatedValueRepairText,
            @"(?<open><Member MemberName=""DeprecatedProperty"">.*?<summary>)To be added\.(?<close></summary>)",
            match =>
                match.Groups["open"].Value +
                "<![CDATA[Example <value>This constant was deprecated in API level 31.</value>]]>" +
                match.Groups["close"].Value,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var deprecatedValueRepairPath = Path.Combine(
            Path.GetTempPath(),
            $"android-api-doc-importer-deprecated-value-{Environment.ProcessId}.xml");
        File.WriteAllText(
            deprecatedValueRepairPath,
            deprecatedValueRepairText,
            new UTF8Encoding(false));
        try
        {
            var deprecatedValueRepairFile = LoadedFile.Load(
                repositoryRoot,
                deprecatedValueRepairPath);
            deprecatedValueRepairFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
            var deprecatedValueRepairOwner = deprecatedValueRepairFile.Owners.Single(owner =>
                owner.Id.Contains("DeprecatedProperty", StringComparison.Ordinal));
            Assert(
                HasDeprecatedValueRepairCandidate(
                    deprecatedValueRepairFile,
                    deprecatedValueRepairOwner),
                "deprecated property value with an exact member source URL is repairable");
            var repairedDeprecatedValueText = RepairDeprecatedValue(
                deprecatedValueRepairFile.Text,
                deprecatedValueRepairFile,
                deprecatedValueRepairOwner,
                deprecatedPropertyDocs);
            Assert(
                repairedDeprecatedValueText.Contains(
                    "<summary><![CDATA[Example <value>This constant was deprecated in API level 31.</value>]]></summary>",
                    StringComparison.Ordinal) &&
                repairedDeprecatedValueText.Contains(
                    "<value>This constant was deprecated in API level 31. Use FAVORITE instead. Identifies the deprecated fixture value.</value>",
                    StringComparison.Ordinal),
                "deprecated property value repair targets the direct value element");
            foreach (var instruction in new[]
            {
                "<?example compare > <value>This constant was deprecated in API level 31.</value> ?>",
                "<?review Don't change this?>",
            })
            {
                var instructionFixture = Regex.Replace(
                    deprecatedValueRepairText,
                    @"(?<summary></summary>)(?<value>\s*<value>This constant was deprecated in API level 31\.</value>)",
                    match => match.Groups["summary"].Value + instruction + match.Groups["value"].Value,
                    RegexOptions.Singleline | RegexOptions.CultureInvariant);
                File.WriteAllText(
                    deprecatedValueRepairPath,
                    instructionFixture,
                    new UTF8Encoding(false));
                var instructionFile = LoadedFile.Load(repositoryRoot, deprecatedValueRepairPath);
                instructionFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
                var instructionOwner = instructionFile.Owners.Single(owner =>
                    owner.Id.Contains("DeprecatedProperty", StringComparison.Ordinal));
                var instructionRepaired = RepairDeprecatedValue(
                    instructionFile.Text,
                    instructionFile,
                    instructionOwner,
                    deprecatedPropertyDocs);
                Assert(
                    instructionRepaired.Contains(instruction, StringComparison.Ordinal) &&
                    instructionRepaired.Contains(
                        "<value>This constant was deprecated in API level 31. Use FAVORITE instead. Identifies the deprecated fixture value.</value>",
                        StringComparison.Ordinal),
                    "processing instructions do not hide or replace a direct deprecated value");
            }
            var repairFailureReport = new ImportReport
            {
                Mode = "dry-run",
                Offline = true,
                MaxChanges = 1,
            };
            Assert(
                ReportMappingFailure(
                    repairFailureReport,
                    deprecatedValueRepairFile,
                    deprecatedValueRepairOwner,
                    MappingResult.Skip(
                        "offline_cache_miss",
                        "No cached official page exists for the fixture.",
                        deprecatedValueRepairOwner.SourceRequest!.Url)) &&
                repairFailureReport.Entries.Any(entry =>
                    entry.Target == "value" &&
                    entry.Reason == "offline_cache_miss"),
                "repair-only deprecated value source failures are reported");
        }
        finally
        {
            File.Delete(deprecatedValueRepairPath);
        }
        var tableOnly = file.Owners.Single(owner =>
            owner.Id.EndsWith(".TableOnly(System.Int32)", StringComparison.Ordinal));
        var tableOnlyResult = MapOwner(tableOnly, pages);
        Assert(
            tableOnlyResult.Docs is not null,
            "channel-only Android documentation maps to the exact member");
        Assert(
            tableOnlyResult.Docs!.Summary.Length == 0 &&
                tableOnlyResult.Docs.Returns == "Value is either 0 or FIRST; SECOND" &&
                ReplacementFor(
                    tableOnly.Placeholders.Single(placeholder => placeholder.Target == "param:value"),
                    tableOnlyResult.Docs).Text == "the fixture value",
            "channel-only Android documentation is imported without a guessed summary");
        var structuredList = file.Owners.Single(owner =>
            owner.Id.EndsWith(".StructuredList", StringComparison.Ordinal));
        var structuredListResult = MapOwner(structuredList, pages);
        var structuredListProse = string.Join("|", structuredListResult.Docs?.Paragraphs ?? []);
        Assert(
            structuredListProse.Contains(
                "First case. Nested detail; Second case.",
                StringComparison.Ordinal) &&
                !structuredListProse.Contains(
                    "Ignore this item.",
                    StringComparison.Ordinal),
            $"Android prose lists retain visible item separators and exclude nolist content: {structuredListProse}");
        var postList = file.Owners.Single(owner =>
            owner.Id.EndsWith(".PostList", StringComparison.Ordinal));
        var postListResult = MapOwner(postList, pages);
        var postListProse = string.Join("|", postListResult.Docs?.Paragraphs ?? []);
        Assert(
            postListProse.IndexOf(
                "following cases: First case. Nested detail; Second case.",
                StringComparison.Ordinal) <
                postListProse.IndexOf(
                    "Requires the visible permission.",
                    StringComparison.Ordinal) &&
                postListProse.Contains("Requires the visible permission.", StringComparison.Ordinal) &&
                !postListProse.Contains("Constant Value:", StringComparison.Ordinal),
            "malformed nested paragraphs preserve ordered post-list permission prose without metadata");
        var systemList = file.Owners.Single(owner =>
            owner.Id.EndsWith(".SystemList", StringComparison.Ordinal));
        var systemListResult = MapOwner(systemList, pages);
        var systemListProse = string.Join("|", systemListResult.Docs?.Paragraphs ?? []);
        Assert(
            systemListProse.Contains(
                "following cases: First eligible case < max; Second eligible case.",
                StringComparison.Ordinal) &&
                systemListProse.Contains(
                    "Requires the required permission.",
                    StringComparison.Ordinal),
            $"malformed system list preserves items and permission tail: {systemListProse}");
        var listField = file.Owners.Single(owner =>
            owner.Id.EndsWith(".ListField", StringComparison.Ordinal));
        var listFieldResult = MapOwner(listField, pages);
        Assert(
            listFieldResult.Docs?.Summary == "Retains this exact sentence.",
            "list introduction retains only complete leading source sentences");
        var emptyConstructor = file.Owners.Single(owner =>
            owner.Id.EndsWith(".#ctor", StringComparison.Ordinal));
        var emptyConstructorResult = MapOwner(emptyConstructor, pages);
        var emptyConstructorReport = new ImportReport
        {
            Mode = "dry-run",
            Offline = true,
            MaxChanges = 1,
        };
        Assert(
            ReportMappingFailure(
                emptyConstructorReport,
                file,
                emptyConstructor,
                emptyConstructorResult) &&
                emptyConstructorReport.Entries.Count == 1 &&
                emptyConstructorReport.Entries[0].Reason == "source_documentation_empty" &&
                emptyConstructorReport.Entries[0].SourceUrl.EndsWith(
                    "#Widget()",
                    StringComparison.Ordinal),
            "empty source documentation report retains the exact Android member URL");
        var typeOnly = ReplacementFor(
            new Placeholder(0, "returns", "", "returns"),
            favoriteResult.Docs! with { Returns = "String" });
        Assert(typeOnly.Reason == "source_channel_not_meaningful", "type-only return skip");
        var valueSummaryFallback = ReplacementFor(
            new Placeholder(0, "value", "", "value"),
            favoriteResult.Docs! with { Returns = "" });
        Assert(
            valueSummaryFallback.Reason == "source_return_missing",
            "property value does not fall back when the source return channel is missing");
        var valueTypeOnlyFallback = ReplacementFor(
            new Placeholder(0, "value", "", "value"),
            favoriteResult.Docs! with { Returns = "Widget" });
        Assert(
            valueTypeOnlyFallback.Text == favoriteResult.Docs.Summary,
            "property value falls back to exact source summary when return text is only a type");
        var valueNullabilityOnly = ReplacementFor(
            new Placeholder(0, "value", "", "value"),
            favoriteResult.Docs! with { Returns = "This value cannot be null." });
        Assert(
            valueNullabilityOnly.Reason == "source_channel_not_meaningful",
            "property value does not fall back when return text is only a nullability marker");
        var parsedTypeOnlyValueDocs = SourcePage.Parse(
            request,
            androidHtml.Replace(
                "the number of displayed characters",
                "Widget",
                StringComparison.Ordinal))
            .Members.Single(member => member.Name == "setTitle").Docs!;
        Assert(
            parsedTypeOnlyValueDocs.Returns == "Widget" &&
            ReplacementFor(
                new Placeholder(0, "value", "", "value"),
                parsedTypeOnlyValueDocs).Text == parsedTypeOnlyValueDocs.Summary,
            "property value uses the exact source summary only for a parsed type-only return channel");
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
        var annotationOnly = ChannelValueOrSkip("[icu]", "summary", "source_summary_missing");
        Assert(
            annotationOnly.Reason == "source_channel_not_meaningful",
            "standalone source annotation skip");
        Assert(
            CleanSourceText(@"the user\u2019s \u201cvalue\u201d") == "the user\u2019s \u201cvalue\u201d",
            "literal Unicode escape decoding");
        Assert(
            CleanSourceText("CharSequence.subsequence()") == "CharSequence.subSequence()",
            "canonical CharSequence method casing");
        Assert(
            CleanSourceText(
                "ff the error is UNARCHIVAL_ERROR_INSUFFICIENT_STORAGE this field should be set.") ==
                    "If the error is UNARCHIVAL_ERROR_INSUFFICIENT_STORAGE, this field should be set.",
            "known Android parameter typo cleanup");
        Assert(
            CleanSourceText(
                "optional intent to start a follow up action required to facilitate the unarchival flow. This value cannot be null.") ==
                    "intent to start a follow up action required to facilitate the unarchival flow. This value cannot be null.",
            "contradictory optional unarchival intent cleanup");
        Assert(
            CleanSourceText(
                "The availability for \"paid content, either to-own or rental (user has not purchased/rented).") ==
                    "The availability for \"paid content\", either to-own or rental (user has not purchased/rented).",
            "unbalanced Android content quote cleanup");
        Assert(
            CleanSourceText(
                "Time shift is handle locally. TODO Link: Tuner#Tuner(Context, string, int).") ==
                    "Time shift is handled locally.",
            "Android time-shift and TODO metadata cleanup");
        Assert(
            CleanSourceText(
                "Federated Compute Server documentation.. This value cannot be null.") ==
                    "Federated Compute Server documentation. This value cannot be null.",
            "Android federated compute parameter punctuation cleanup");
        Assert(
            RemoveLeadingJavaType(
                "long: ff the error is UNARCHIVAL_ERROR_INSUFFICIENT_STORAGE this field should be set.") ==
                    "If the error is UNARCHIVAL_ERROR_INSUFFICIENT_STORAGE, this field should be set.",
            "typed Android parameter typo cleanup");
        Assert(
            androidPage.Members.Single(member => member.Name == "Widget").Docs is null,
            "boilerplate-only member documentation skip");
        Assert(
            androidPage.Members.Single(member => member.Name == "Widget").Url.EndsWith(
                "#Widget()",
                StringComparison.Ordinal),
            "empty Android member documentation retains its exact source URL");
        Assert(
            !favoriteResult.Docs.Paragraphs.Any(
                paragraph => paragraph.Text.Contains("Content and code samples", StringComparison.Ordinal) ||
                    paragraph.Text.StartsWith("Last updated ", StringComparison.Ordinal)),
            "Android footer paragraphs filtered");

        var javaRequest = new SourceRequest(
            "java/lang/String",
            JavaReference + "java.base/java/lang/String.html",
            "java");
        var javaPage = SourcePage.Parse(javaRequest, javaHtml);
        Assert(
            javaPage.TypeDocs?.Summary == "Represents a sequence of characters." &&
                !javaPage.TypeDocs.Paragraphs.Any(
                    paragraph => paragraph.Text.Contains("Deprecated", StringComparison.Ordinal)),
            "Java deprecated type block excluded");
        var length = javaPage.Members.Single(member => member.Name == "length");
        Assert(length.ArgumentDescriptors?.Count == 0, "Java no-argument descriptor");
        Assert(length.Docs?.Returns == "the length of this string", "Java return extraction");
        Assert(
            Descriptor.FromAnchor(
                "set(android.hardware.camera2.CaptureRequest.Key<T>,T)",
                "android/hardware/camera2/CaptureRequest$Builder")?.SequenceEqual(
                    [
                        "Landroid/hardware/camera2/CaptureRequest$Key;",
                        "Ljava/lang/Object;",
                    ],
                    StringComparer.Ordinal) == true,
            "generic Java type variables erase to Object descriptors");
        Assert(
            length.Docs?.Summary == "Returns the length of this string." &&
                !length.Docs.Paragraphs.Any(
                    paragraph => paragraph.Text.Contains("Deprecated", StringComparison.Ordinal)),
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
        var cdataSummaryFixture = fixtureText.Replace(
            "<param name=\"title\">To be added.</param>",
            "<param name=\"title\"><![CDATA[Example XML: <summary>To be added.</summary>]]></param>",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, cdataSummaryFixture);
        Assert(
            TryReplacePlaceholder(
                cdataSummaryFixture,
                file.DocsBlocks[setTitle.Order],
                summary,
                mappedDocs.Summary,
                out var cdataSummaryReplaced,
                out _) &&
            cdataSummaryReplaced.Contains(
                "<param name=\"title\"><![CDATA[Example XML: <summary>To be added.</summary>]]></param>",
                StringComparison.Ordinal) &&
            cdataSummaryReplaced.Contains(
                "<summary>Sets the widget title.</summary>",
                StringComparison.Ordinal),
            "summary replacement targets the structural placeholder outside CDATA");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
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
        const string nestedBuilderAnchor =
            "https://developer.android.com/reference/android/example/Widget.Builder#Widget$Builder()";
        const string builderAnchor =
            "https://developer.android.com/reference/android/example/Widget.Builder#Builder()";
        Assert(
            !RemoveStaleSourceLinks(
                $"<para><format type=\"text/html\"><a href=\"{nestedBuilderAnchor}\" " +
                "title=\"Reference documentation\">Stale builder reference.</a></format></para>\n",
                builderAnchor,
                removeAll: false).Contains(nestedBuilderAnchor, StringComparison.Ordinal),
            "stale nested builder constructor link was removed");
        var nestedConstructorText =
            $"<Docs><remarks>{Environment.NewLine}<para><format type=\"text/html\"><a href=\"{nestedBuilderAnchor}\" " +
            $"title=\"Reference documentation\">Stale <code>Widget.Builder.Widget$Builder()</code> reference.</a></format></para>{Environment.NewLine}</remarks></Docs>";
        var normalizedNestedConstructorText = NormalizeStaleNestedConstructorLinks(
            nestedConstructorText,
            new DocsBlock(0, 0, nestedConstructorText.Length));
        Assert(
            !normalizedNestedConstructorText.Contains("$Builder(", StringComparison.Ordinal) &&
                normalizedNestedConstructorText.Contains(builderAnchor, StringComparison.Ordinal) &&
                normalizedNestedConstructorText.Contains(
                    "<code>Widget.Builder.Builder()</code>",
                    StringComparison.Ordinal),
            "independent nested constructor normalization preserves the reference paragraph");
        var nestedConstructorWithUnrelatedDollar = nestedConstructorText.Replace(
            " reference.</a>",
            " and <code>unrelated$code</code> reference.</a>",
            StringComparison.Ordinal);
        Assert(
            NormalizeStaleNestedConstructorLinks(
                nestedConstructorWithUnrelatedDollar,
                new DocsBlock(0, 0, nestedConstructorWithUnrelatedDollar.Length))
                .Contains("<code>unrelated$code</code>", StringComparison.Ordinal),
            "nested constructor normalization preserves unrelated dollar text");
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
            favoriteResult.Docs.Paragraphs[0].Text,
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

        const string metadataRepairText =
            "<Docs><remarks>To be added.<para><format type=\"text/html\">" +
            "<a href=\"https://developer.android.com/reference/android/example/Widget#favorite\" " +
            "title=\"Reference documentation\">Android reference.</a></format></para>" +
            "<para>Portions of this page are modifications based on work created and shared by the " +
            "<format type=\"text/html\"><a href=\"https://developers.google.com/terms/site-policies\">" +
            "Android Open Source Project</a></format> and used according to terms described in the " +
            "<format type=\"text/html\"><a href=\"https://creativecommons.org/licenses/by/2.5/\">" +
            "Creative Commons 2.5 Attribution License.</a></format></para></remarks></Docs>";
        var metadataRepairRemarks = XDocument.Parse(metadataRepairText).Root!.Element("remarks")!;
        var metadataRepair = Placeholder.Create(metadataRepairRemarks, 0);
        var metadataReplacement = ReplacementFor(metadataRepair, favoriteResult.Docs!).Text!;
        Assert(metadataRepair.IsImporterMetadataRepair, "importer metadata remarks repair detection");
        Assert(TryReplacePlaceholder(
            metadataRepairText,
            new DocsBlock(0, 0, metadataRepairText.Length),
            metadataRepair,
            metadataReplacement,
            out var repairedMetadataText,
            out _), "importer metadata remarks placeholder is replaced");
        Assert(
            !repairedMetadataText.Contains("To be added.", StringComparison.Ordinal),
            "importer metadata remarks placeholder is removed");
        var repairedMetadataProse = NormalizeText(
            XDocument.Parse(repairedMetadataText).Root!.Element("remarks")!
                .Elements("para").First().Value);
        Assert(
            repairedMetadataProse == NormalizeText(metadataReplacement),
            "importer metadata remarks placeholder retains source prose");
        Assert(
            repairedMetadataText.Contains("Reference documentation", StringComparison.Ordinal),
            "importer metadata remarks preserves reference metadata");
        var cleanedMissingRemarksText = RemoveImporterRemarksMetadata(metadataRepairText);
        var cleanedMissingRemarks = XDocument.Parse(cleanedMissingRemarksText)
            .Root!.Element("remarks")!;
        Assert(
            NormalizeText(cleanedMissingRemarks.Value).Equals(
                "To be added.",
                StringComparison.Ordinal) &&
                !cleanedMissingRemarks.Descendants("a").Any(),
            "missing source remarks retain their placeholder without importer metadata");
        Assert(
            RemoveStandaloneRemarksPlaceholder("<Docs><remarks>To be added.</remarks></Docs>") ==
                "<Docs><remarks /></Docs>",
            "channel-only source imports clear a standalone remarks placeholder before metadata insertion");

        var duplicateMetadataReference =
            $"<para><format type=\"text/html\"><a href=\"{favoriteResult.Docs!.SourceUrl}\" " +
            $"title=\"Reference documentation\">Android reference for <code>{favoriteResult.Docs.SourceLabel}</code>.</a></format></para>";
        const string userReference =
            "<para><format type=\"text/html\"><a href=\"https://example.invalid/reference\" title=\"Reference documentation\">User-authored reference.</a></format></para>";
        var duplicateMetadataRepairText =
            $"<Docs><remarks>\n{duplicateMetadataReference}\n{userReference}\n{duplicateMetadataReference}\n" +
            $"<para>{AndroidAttribution}</para>\n</remarks></Docs>";
        Assert(
            TryGetImporterSourceReferenceUrl(duplicateMetadataReference, out var duplicateSourceUrl) &&
                UrlsEqual(duplicateSourceUrl, favoriteResult.Docs.SourceUrl),
            "metadata-only fixture uses importer reference syntax");
        Assert(
            CountImporterSourceReferences(
                duplicateMetadataRepairText,
                favoriteResult.Docs.SourceUrl) == 2,
            "metadata-only fixture contains duplicate importer references");
        Assert(
            !HasMetadataOnlyRemarks(duplicateMetadataRepairText) &&
            HasMetadataOnlyRemarks(duplicateMetadataRepairText.Replace(
                userReference,
                "",
                StringComparison.Ordinal)),
            "metadata-only repair preserves user-authored reference content");
        var deduplicatedMetadataRepairText = RemoveDuplicateSourceLinks(
            duplicateMetadataRepairText,
            favoriteResult.Docs.SourceUrl);
        Assert(
            CountImporterSourceReferences(
                deduplicatedMetadataRepairText,
                favoriteResult.Docs.SourceUrl) == 1 &&
            deduplicatedMetadataRepairText.Contains(userReference, StringComparison.Ordinal) &&
            deduplicatedMetadataRepairText.Contains(AndroidAttribution, StringComparison.Ordinal) &&
            RemoveDuplicateSourceLinks(
                deduplicatedMetadataRepairText,
                favoriteResult.Docs.SourceUrl).Equals(
                    deduplicatedMetadataRepairText,
                    StringComparison.Ordinal),
            "metadata-only repairs deduplicate importer references without removing user content");
        const string emptyMetadataRepairText =
            "<Docs>\n  <remarks>\n    <para></para>\n    \n" +
            "    <para><format type=\"text/html\"><a " +
            "href=\"https://developer.android.com/reference/android/example/Widget#favorite\" " +
            "title=\"Reference documentation\">Android reference.</a></format></para>\n" +
            "    <para>Portions of this page are modifications based on work created and shared by the " +
            "<format type=\"text/html\"><a href=\"https://developers.google.com/terms/site-policies\">" +
            "Android Open Source Project</a></format> and used according to terms described in the " +
            "<format type=\"text/html\"><a href=\"https://creativecommons.org/licenses/by/2.5/\">" +
            "Creative Commons 2.5 Attribution License.</a></format></para>\n" +
            "  </remarks>\n</Docs>";
        var emptyMetadataRepairRemarks =
            XDocument.Parse(emptyMetadataRepairText).Root!.Element("remarks")!;
        var emptyMetadataRepairVariants = new[]
        {
            emptyMetadataRepairText,
            emptyMetadataRepairText.Replace("\n", "\r\n", StringComparison.Ordinal),
            emptyMetadataRepairText.Replace("<para></para>", "<para />", StringComparison.Ordinal),
            emptyMetadataRepairText.Replace("<para></para>", "<para> \t </para>", StringComparison.Ordinal),
            emptyMetadataRepairText.Replace("<para></para>", "<para data-source=\"importer\">&#x20;</para>", StringComparison.Ordinal),
            emptyMetadataRepairText.Replace("<para></para>", "<!-- retained --><para></para>", StringComparison.Ordinal),
            emptyMetadataRepairText.Replace(
                "<remarks>\n    <para></para>\n    \n",
                "<remarks><para></para>",
                StringComparison.Ordinal),
        };
        Assert(
            LoadedFile.IsImporterAugmentedRemarksPlaceholder(emptyMetadataRepairRemarks) &&
                TryReplacePlaceholder(
                    emptyMetadataRepairText,
                    new DocsBlock(0, 0, emptyMetadataRepairText.Length),
                    Placeholder.Create(emptyMetadataRepairRemarks, 0),
                    metadataReplacement,
                    out var repairedEmptyMetadataText,
                    out _) &&
                !repairedEmptyMetadataText.Contains("<para></para>", StringComparison.Ordinal) &&
                NormalizeText(XDocument.Parse(repairedEmptyMetadataText).Root!.Element("remarks")!
                    .Elements("para").First().Value) == NormalizeText(metadataReplacement) &&
                repairedEmptyMetadataText.Contains("Reference documentation", StringComparison.Ordinal) &&
                repairedEmptyMetadataText.Contains(
                    "https://developers.google.com/terms/site-policies",
                    StringComparison.Ordinal),
            "importer empty metadata paragraph repair retains prose and metadata");
        foreach (var emptyMetadataRepairVariant in emptyMetadataRepairVariants)
        {
            var variantRemarks = XDocument.Parse(emptyMetadataRepairVariant).Root!.Element("remarks")!;
            var variantPlaceholder = Placeholder.Create(variantRemarks, 0);
            Assert(
                LoadedFile.IsImporterAugmentedRemarksPlaceholder(variantRemarks) &&
                    TryReplacePlaceholder(
                        emptyMetadataRepairVariant,
                        new DocsBlock(0, 0, emptyMetadataRepairVariant.Length),
                        variantPlaceholder,
                        metadataReplacement,
                        out var repairedVariantText,
                        out _) &&
                    XDocument.Parse(repairedVariantText).Root is not null &&
                    !LoadedFile.IsImporterAugmentedRemarksPlaceholder(
                        XDocument.Parse(repairedVariantText).Root!.Element("remarks")!) &&
                    NormalizeText(XDocument.Parse(repairedVariantText).Root!.Element("remarks")!
                        .Elements("para").First().Value) == NormalizeText(metadataReplacement),
                "importer empty metadata paragraph repair supports LF, CRLF, self-closing, whitespace, and inline layouts");
        }
        var firstRemarksStart = emptyMetadataRepairText.IndexOf("<remarks>", StringComparison.Ordinal);
        var firstRemarksEnd = emptyMetadataRepairText.IndexOf("</remarks>", StringComparison.Ordinal) +
            "</remarks>".Length;
        var candidateRemarksText = emptyMetadataRepairText[firstRemarksStart..firstRemarksEnd];
        var twoCandidateMetadataText = $"<Docs>{candidateRemarksText}{candidateRemarksText}</Docs>";
        var twoCandidateRemarks = XDocument.Parse(twoCandidateMetadataText).Root!.Element("remarks")!;
        Assert(
            TryReplacePlaceholder(
                twoCandidateMetadataText,
                new DocsBlock(0, 0, twoCandidateMetadataText.Length),
                Placeholder.Create(twoCandidateRemarks, 0),
                metadataReplacement,
                out var repairedTwoCandidateText,
                out _) &&
            XDocument.Parse(repairedTwoCandidateText).Root!.Elements("remarks")
                .Count(LoadedFile.IsImporterAugmentedRemarksPlaceholder) == 1,
            "importer metadata repair validates only its targeted remarks candidate");
        var directPlaceholderWithTwoEmptyParagraphs = emptyMetadataRepairText.Replace(
            "<para></para>",
            "To be added.<para></para><para></para>",
            StringComparison.Ordinal);
        var directPlaceholderWithTwoEmptyRemarks =
            XDocument.Parse(directPlaceholderWithTwoEmptyParagraphs).Root!.Element("remarks")!;
        Assert(
            LoadedFile.IsImporterAugmentedRemarksPlaceholder(directPlaceholderWithTwoEmptyRemarks) &&
                TryReplacePlaceholder(
                    directPlaceholderWithTwoEmptyParagraphs,
                    new DocsBlock(0, 0, directPlaceholderWithTwoEmptyParagraphs.Length),
                    Placeholder.Create(directPlaceholderWithTwoEmptyRemarks, 0),
                    metadataReplacement,
                    out var repairedDirectPlaceholderText,
                    out _) &&
                repairedDirectPlaceholderText.Contains(metadataReplacement, StringComparison.Ordinal),
            "direct metadata placeholder ignores coexisting empty paragraphs");

        var emptyReturn = androidPage.Members.Single(member => member.Name == "emptyReturn");
        Assert(emptyReturn.Docs?.Returns.Length == 0, "empty return description preserved");
        var networkScan = androidPage.Members.Single(member => member.Name == "requestNetworkScan");
        Assert(
            networkScan.Docs?.Returns ==
                "a scan handle that can be used to stop the network scan",
            "exact Android Returns heading selected");
        Assert(
            !favoriteResult.Docs!.Paragraphs[0].Text.Contains(")&quot;&gt;", StringComparison.Ordinal) &&
                !favoriteResult.Docs.Paragraphs[0].Text.Contains(")\"&gt;", StringComparison.Ordinal) &&
                favoriteResult.Docs.Paragraphs[0].Text.Contains("consume(List)", StringComparison.Ordinal),
            "quoted generic link stripped without corrupt fragments");
        var codeSample = androidPage.Members.Single(member => member.Name == "CODE_SAMPLE");
        Assert(
            codeSample.Docs?.Summary == "Documents a sample-capable feature." &&
                codeSample.Docs.Paragraphs.Any(paragraph =>
                    !paragraph.IsCode &&
                    paragraph.Text.Contains(
                    "Post-sample guidance.",
                    StringComparison.Ordinal)) &&
                codeSample.Docs.Paragraphs.Any(paragraph =>
                    !paragraph.IsCode &&
                    paragraph.Text.Equals(
                        "Requires the special permission.",
                        StringComparison.Ordinal)) &&
                codeSample.Docs.Paragraphs.Any(paragraph =>
                    paragraph.IsCode &&
                    paragraph.Text.Contains("example()", StringComparison.Ordinal)),
            "code blocks and surrounding prose are preserved in source order");
        var inlineSample = androidPage.Members.Single(member => member.Name == "INLINE_SAMPLE");
        Assert(
            inlineSample.Docs?.Summary == "Combines |s and marks FOO.",
            "inline markup preserves adjacent punctuation");
        Assert(
            SourcePage.HtmlText("<p>Use <code>for (;;) { process(); }</code> for a processing loop.</p>") ==
                "Use for (;;) { process(); } for a processing loop.",
            "inline Java code semicolons are preserved");
        Assert(
            SourcePage.HtmlText(
                "<p>Supported loops:</p><ul><li><code>for (;;) { process(); }</code></li></ul><p>continue.</p>") ==
                "Supported loops: for (;;) { process(); } continue.",
            "inline Java code semicolons are preserved in list prose");
        Assert(
            SourcePage.HtmlText(
                "<p>Types:</p><ul><li><code>List&lt;String&gt;</code></li><li><code>Set&lt;Integer&gt;</code></li></ul><p>continue.</p>") ==
                "Types: List<String>; Set<Integer> continue.",
            "inline generic code is preserved in list prose");
        Assert(
            SourcePage.HtmlText(
                "<p>Use:</p><ul><li><code>__INLINE_CODE_1__</code></li><li><code>for (;;) { process(); }</code></li></ul><p>continue.</p>") ==
                "Use: __INLINE_CODE_1__; for (;;) { process(); } continue.",
            "inline code markers cannot collide with source text");
        var inlineCodePeriodText = SourcePage.HtmlText(
            "<p>Versions:</p><ul><li><code>Version 1.</code></li><li>Other</li></ul><p>continue.</p>");
        Assert(
            inlineCodePeriodText == "Versions: Version 1.; Other continue.",
            $"terminal punctuation in inline code is preserved: {inlineCodePeriodText}");
        Assert(
            SourcePage.HtmlText(
                "<p>Versions:</p><ul><li><code><span>Version 1.</span></code></li><li>Other</li></ul><p>continue.</p>") ==
                "Versions: Version 1.; Other continue.",
            "nested markup in inline code is emitted as text");
        Assert(
            SourcePage.HtmlText(
                "<p><strong>Types:</strong></p><ul><li>First</li><li>Second</li></ul><p>continue.</p>") ==
                "Types: First; Second continue.",
            "formatted list introductions retain colon punctuation");
        var bridgedListParagraphs = SourcePage.ExtractParagraphs(
            "<p>Types:</p><ul><li>List&lt;String&gt;</li><li><pre>for (;;) { process(); }</pre></li></ul><p>Continue.</p>");
        Assert(
            bridgedListParagraphs.Count == 2 &&
                bridgedListParagraphs[0].Text == "Types: List<String>; for (;;) { process(); }" &&
                bridgedListParagraphs[1].Text == "Continue.",
            "list bridge retains payload and following prose as paragraphs");
        var multiBridgeParagraphs = SourcePage.ExtractParagraphs(
            "<p>First:</p><ul><li>One</li><li>Two<ul><li>Nested</li></ul></li></ul><p>Middle.</p><p>Second:</p><ol><li>Three</li><li>Four</li></ol><p>Last.</p>");
        Assert(
            multiBridgeParagraphs.Count == 4 &&
                multiBridgeParagraphs[0].Text == "First: One; Two; Nested" &&
                multiBridgeParagraphs[1].Text == "Middle." &&
                multiBridgeParagraphs[2].Text == "Second: Three; Four" &&
                multiBridgeParagraphs[3].Text == "Last.",
            "multiple list bridges retain following paragraphs and nested list content");

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
            HasImporterOwnedEnumMetadata(
                enumDocs.Element("summary")!,
                enumMapped.Docs!),
            "canonical enum source metadata is recognized");
        Assert(
            !enumDocs.Element("remarks")!.Descendants("a").Any() &&
                NormalizeText(enumDocs.Element("remarks")!.Value)
                    .Equals("To be added.", StringComparison.Ordinal),
            "enum discarded remarks retain only placeholder");
        var enumFileWithCode = LoadedFile.Load(
            repositoryRoot,
            Path.Combine(fixtureRoot, "enum-source.xml"));
        enumFileWithCode.SelectOwners(null);
        var enumFavoriteWithCode = enumFileWithCode.Owners.Single(
            owner => (string?)owner.Member?.Attribute("MemberName") == "Favorite");
        var enumDocsWithCode = enumMapped.Docs! with
        {
            Paragraphs =
            [
                enumMapped.Docs.Paragraphs[0],
                new SourceParagraph(
                    "<service android:foregroundServiceType=\"foo\">\n" +
                    "  <property android:value=\"foo\" />\n" +
                    "</service>",
                    true),
                .. enumMapped.Docs.Paragraphs.Skip(1),
            ],
        };
        var enumCodeSummary = enumFavoriteWithCode.Placeholders.Single(item => item.Name == "summary");
        Assert(
            TryReplacePlaceholder(
                enumFileWithCode.Text,
                enumFileWithCode.DocsBlocks[enumFavoriteWithCode.Order],
                enumCodeSummary,
                ReplacementFor(enumCodeSummary, enumDocsWithCode, true).Text!,
                out var enumCodeText,
                out _),
            "enum summary replacement with code");
        enumFileWithCode.UpdateBlockOffsets(enumFavoriteWithCode.Order, enumCodeText);
        enumCodeText = AddSourceDocumentationIfSafe(
            enumCodeText,
            enumFileWithCode,
            enumFavoriteWithCode,
            enumDocsWithCode);
        var enumCodeSummaryElement = XDocument.Parse(enumCodeText)
            .Root!.Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "Favorite")
            .Element("Docs")!.Element("summary")!;
        Assert(
            enumCodeSummaryElement.Descendants("code").Any(code =>
                (string?)code.Attribute("lang") == "text/java" &&
                code.Value.Equals(
                    "<service android:foregroundServiceType=\"foo\">\n" +
                    "  <property android:value=\"foo\" />\n" +
                    "</service>",
                    StringComparison.Ordinal)),
            "enum summaries preserve source code examples and line breaks");
        var enumMetadataSourceUrl = XmlAttributeEscape(enumMapped.Docs!.SourceUrl);
        var enumSourceLabel = enumMapped.Docs.SourceKind == "android" ? "Android" : "Java";
        var enumExpectedSource =
            $"<para><format type=\"text/html\"><a href=\"{enumMetadataSourceUrl}\" " +
            $"title=\"Reference documentation\">{enumSourceLabel} reference for <code>{XmlEscape(enumMapped.Docs.SourceLabel)}</code>.</a></format></para>";
        var enumMetadataWithoutTransfer =
            "<Docs><summary><para>Keep semantic prose.</para></summary><remarks>\n" +
            $"  {enumExpectedSource}\n" +
            $"  <para>{AndroidAttribution}</para>\n" +
            "</remarks></Docs>";
        Assert(
            RemoveEnumDiscardedMetadata(enumMetadataWithoutTransfer, enumMapped.Docs!) ==
                enumMetadataWithoutTransfer,
            "enum remarks metadata is preserved until summary transfer is confirmed");
        var enumMetadataWithTransfer = enumMetadataWithoutTransfer.Replace(
            "<summary><para>Keep semantic prose.</para></summary>",
            "<summary>" +
            enumExpectedSource +
            $"<para>{AndroidAttribution}</para>" +
            "</summary>",
            StringComparison.Ordinal);
        var prunedEnumMetadata = RemoveEnumDiscardedMetadata(
            enumMetadataWithTransfer,
            enumMapped.Docs!);
        Assert(
            prunedEnumMetadata.Contains("<remarks />", StringComparison.Ordinal) &&
                !XElement.Parse(prunedEnumMetadata)
                    .Element("remarks")!
                    .Descendants("a")
                    .Any(),
            "enum remarks metadata self-closes after verified summary transfer");
        var favoriteRemarksClose = enumText.IndexOf("</remarks>", StringComparison.Ordinal);
        var enumTextWithDuplicateMetadata =
            enumText[..favoriteRemarksClose] +
            "\n" +
            $"          {enumExpectedSource}\n" +
            $"          <para>{AndroidAttribution}</para>\n" +
            "          <para><format type=\"text/html\"><a href=\"https://example.invalid/unrelated\" title=\"Reference documentation\">Keep unrelated content.</a></format></para>\n" +
            enumText[favoriteRemarksClose..];
        enumFile.UpdateBlockOffsets(enumFavorite.Order, enumTextWithDuplicateMetadata);
        var prunedEnumText = AddSourceDocumentationIfSafe(
            enumTextWithDuplicateMetadata,
            enumFile,
            enumFavorite,
            enumMapped.Docs!);
        var prunedEnumRemarks = XDocument.Parse(prunedEnumText)
            .Root!
            .Element("Members")!
            .Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "Favorite")
            .Element("Docs")!
            .Element("remarks")!;
        Assert(
            !prunedEnumRemarks.Descendants("a").Any(link =>
                UrlsEqual(
                    WebUtility.HtmlDecode((string?)link.Attribute("href") ?? ""),
                    enumMapped.Docs!.SourceUrl) ||
                ((string?)link.Attribute("href"))?.Equals(
                    "https://developers.google.com/terms/site-policies",
                    StringComparison.Ordinal) == true) &&
                prunedEnumRemarks.Descendants("a").Any(link =>
                    ((string?)link.Attribute("href"))?.Equals(
                        "https://example.invalid/unrelated",
                        StringComparison.Ordinal) == true) &&
                NormalizeText(prunedEnumRemarks.Value).Contains(
                    "To be added.",
                    StringComparison.Ordinal) &&
                NormalizeText(prunedEnumRemarks.Value).Contains(
                    "Keep unrelated content.",
                    StringComparison.Ordinal),
            "already-complete enum summary prunes only duplicate remarks metadata");
        var enumMetadataWithUnrelatedPolicyLink = enumMetadataWithoutTransfer.Replace(
            "<summary><para>Keep semantic prose.</para></summary>",
            "<summary>" +
            enumExpectedSource +
            "<para><format type=\"text/html\"><a href=\"https://developers.google.com/terms/site-policies\">Unrelated policy link.</a></format></para>" +
            "</summary>",
            StringComparison.Ordinal);
        Assert(
            RemoveEnumDiscardedMetadata(
                enumMetadataWithUnrelatedPolicyLink,
                enumMapped.Docs!) == enumMetadataWithUnrelatedPolicyLink,
            "enum remarks attribution is preserved until the exact attribution paragraph transfers");

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
        var firstOwner = file.Owners[0];
        var laterOwner = file.Owners[1];
        var expectedLaterBlock = emptyRemarksText[
            file.DocsBlocks[laterOwner.Order].Start..file.DocsBlocks[laterOwner.Order].End];
        var speculativeCappedRepairText = emptyRemarksText.Replace(
            "<Docs>",
            "<Docs> ",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(firstOwner.Order, speculativeCappedRepairText);
        RestoreOffsetsAfterSkippedRepair(file, firstOwner, emptyRemarksText);
        Assert(
            emptyRemarksText[
                file.DocsBlocks[laterOwner.Order].Start..file.DocsBlocks[laterOwner.Order].End] ==
                expectedLaterBlock,
            "capped repair restores later documentation block offsets");

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"android-api-doc-importer-self-test-{Environment.ProcessId}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var channelOnlyText = fixtureText.Replace(
                "<param name=\"title\">To be added.</param>",
                $"<param name=\"title\">{RemoveLeadingJavaType(mappedDocs.Parameters["title"])}</param>",
                StringComparison.Ordinal);
            channelOnlyText = Regex.Replace(
                channelOnlyText,
                @"<remarks>.*?</remarks>",
                "<remarks>To be added.</remarks>",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            var channelOnlyPath = Path.Combine(tempDirectory, "channel-only.xml");
            File.WriteAllText(channelOnlyPath, channelOnlyText, new UTF8Encoding(false));
            var channelOnlyFile = LoadedFile.Load(repositoryRoot, channelOnlyPath);
            channelOnlyFile.SelectOwners(null);
            var channelOnlyOwner = channelOnlyFile.Owners.Single(owner =>
                owner.Id.Contains("SetTitle", StringComparison.Ordinal));
            var channelOnlyDocs = mappedDocs with { Paragraphs = [] };
            Assert(
                HasChannelOnlySourceMetadata(channelOnlyFile, channelOnlyOwner, channelOnlyDocs),
                "channel-only source import is eligible for metadata repair");
            var channelOnlyRepaired = AddSourceDocumentationIfSafe(
                channelOnlyFile.Text,
                channelOnlyFile,
                channelOnlyOwner,
                channelOnlyDocs,
                addMetadataForChannelOnlyMember: true);
            Assert(
                channelOnlyRepaired.Contains(mappedDocs.SourceUrl, StringComparison.Ordinal),
                "channel-only source import adds metadata");
            var channelOnlyRemarks = XDocument.Parse(channelOnlyRepaired)
                .Root!.Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "SetTitle")
                .Element("Docs")!.Element("remarks")!;
            Assert(
                !NormalizeText(channelOnlyRemarks.Value).Contains("To be added.", StringComparison.Ordinal),
                "channel-only source import clears the remarks placeholder");
            var valueAndExceptionDocs = mappedDocs with
            {
                Paragraphs = [],
                Parameters = [],
                Returns = "int: the stored value",
                Exceptions = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["IllegalArgumentException"] = "if the title is invalid",
                },
            };
            var valueAndExceptionText = channelOnlyText
                .Replace(
                    "<returns>To be added.</returns>",
                    "<returns>To be added.</returns><value>the stored value</value>",
                    StringComparison.Ordinal)
                .Replace(
                    "<exception cref=\"T:Java.Lang.IllegalArgumentException\">To be added.</exception>",
                    "<exception cref=\"T:Java.Lang.IllegalArgumentException\">if the title is invalid</exception>",
                    StringComparison.Ordinal);
            var valueAndExceptionPath = Path.Combine(tempDirectory, "value-exception-only.xml");
            File.WriteAllText(valueAndExceptionPath, valueAndExceptionText, new UTF8Encoding(false));
            var valueAndExceptionFile = LoadedFile.Load(repositoryRoot, valueAndExceptionPath);
            valueAndExceptionFile.SelectOwners(null);
            var valueAndExceptionOwner = valueAndExceptionFile.Owners.Single(owner =>
                owner.Id.Contains("SetTitle", StringComparison.Ordinal));
            Assert(
                valueAndExceptionOwner.Docs.Element("value")?.Value == "the stored value",
                "value-only fixture contains the exact source value");
            Assert(
                valueAndExceptionOwner.Docs.Element("exception")?.Value == "if the title is invalid",
                "exception-only fixture contains the exact source exception");
            Assert(
                ReplacementFor(
                    new Placeholder(0, "value", "", "value"),
                    valueAndExceptionDocs).Text == "the stored value" &&
                ReplacementFor(
                    new Placeholder(
                        0,
                        "exception",
                        "T:Java.Lang.IllegalArgumentException",
                        "exception"),
                    valueAndExceptionDocs).Text == "if the title is invalid",
                "value and exception-only fixture matches exact source channels");
            Assert(
                valueAndExceptionDocs.Paragraphs.Count == 0,
                "value and exception-only fixture has no source paragraphs");
            Assert(
                valueAndExceptionOwner.Placeholders.Any(placeholder =>
                    placeholder.Name is "remarks" or "para"),
                "value and exception-only fixture retains its remarks placeholder");
            Assert(
                !ContainsSourceUrl(
                    valueAndExceptionFile.Text[
                        valueAndExceptionFile.DocsBlocks[valueAndExceptionOwner.Order].Start..
                        valueAndExceptionFile.DocsBlocks[valueAndExceptionOwner.Order].End],
                    valueAndExceptionDocs.SourceUrl),
                "value and exception-only fixture has no source metadata");
            Assert(
                HasChannelOnlySourceMetadata(
                    valueAndExceptionFile,
                    valueAndExceptionOwner,
                    valueAndExceptionDocs),
                "value and exception-only source imports are eligible for metadata repair");

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

            const string augmentedRepairFixture =
                "<Type Name=\"Widget\" FullName=\"Android.Example.Widget\">\n" +
                "  <Attributes><Attribute><AttributeName Language=\"C#\">[Android.Runtime.Register(\"android/example/Widget\", DoNotGenerateAcw=true)]</AttributeName></Attribute></Attributes>\n" +
                "  <Docs><summary>Existing documentation.</summary><remarks>To be added.<para>Imported metadata.</para></remarks></Docs>\n" +
                "</Type>";
            var augmentedRepairPath = Path.Combine(tempDirectory, "augmented-repair.xml");
            File.WriteAllText(augmentedRepairPath, augmentedRepairFixture, new UTF8Encoding(false));
            var augmentedRepairFile = LoadedFile.Load(repositoryRoot, augmentedRepairPath);
            augmentedRepairFile.SelectOwners(null);
            var augmentedRepairOwner = augmentedRepairFile.Owners.Single();
            var augmentedRepairReport = new ImportReport
            {
                Mode = "dry-run",
                Offline = true,
                MaxChanges = 1,
            };
            var augmentedRepairMapping = MapOwner(
                augmentedRepairOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [augmentedRepairOwner.SourceRequest!.Url] = SourceLoadResult.Failure(
                        "offline_cache_miss",
                        "No cached official page exists for the fixture."),
                });
            Assert(
                augmentedRepairOwner.Placeholders.Count == 0 &&
                    HasAugmentedRemarksPlaceholder(augmentedRepairFile, augmentedRepairOwner) &&
                    ReportMappingFailure(
                        augmentedRepairReport,
                        augmentedRepairFile,
                        augmentedRepairOwner,
                        augmentedRepairMapping) &&
                    augmentedRepairReport.Entries.Count == 1 &&
                    augmentedRepairReport.Entries[0].Target == "remarks" &&
                    augmentedRepairReport.Entries[0].Reason == "offline_cache_miss",
                "repair-only mapping failure reported for remarks");

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

        Console.WriteLine("SELF-TEST PASS: Android/Java exact matching, ICU text and Health Connect importer regressions, paragraph and code preservation, metadata-only and placeholder repairs, source-channel validation, XML parsing, and atomic writes.");
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
                Newline = SelectNewline(text),
                HasUtf8Bom = hasBom,
                Root = root,
                DocsBlocks = FindDocsBlocks(text),
            };
        }

        public static string SelectNewline(string text) =>
            Regex.Matches(text, "\r\n", RegexOptions.CultureInvariant).Count >
                Regex.Matches(text, "(?<!\r)\n", RegexOptions.CultureInvariant).Count
                ? "\r\n"
                : "\n";

        public void SelectOwners(
            string? memberFilter,
            InterfaceMemberResolver? interfaceMemberResolver = null)
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
                    .Where(element =>
                        (!element.HasElements && IsPlaceholder(element.Value)) ||
                        IsImporterAugmentedRemarksPlaceholder(element))
                    .Select((element, index) => Placeholder.Create(element, index))
                    .ToList();
                var memberField = member is null ? null : Registration.JniField(member);
                var memberRegistration = member is null ? null : Registration.Member(member);
                var interfaceMember = memberRegistration is null && member is not null
                    ? interfaceMemberResolver?.Resolve(member)
                    : null;
                memberRegistration ??= interfaceMember?.Registration;
                var sourceVerifiedMember = memberRegistration is null && member is not null
                    ? SourceVerifiedMemberMappings.Resolve(id)
                    : null;
                memberRegistration ??= sourceVerifiedMember?.Registration;
                var sourceOverride = member is null
                    ? null
                    : SourceVerifiedMemberMappings.ResolveSourceOverride(id);
                var request = member is null
                    ? typeRequest
                    : sourceOverride ??
                        SourceRequest.Create(memberField?.Owner) ??
                        interfaceMember?.SourceRequest ??
                        sourceVerifiedMember?.SourceRequest ??
                        typeRequest;
                Owners.Add(new DocsOwner(
                    order,
                    id,
                    docs,
                    member,
                    memberRegistration,
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

        public static bool IsImporterAugmentedRemarksPlaceholder(XElement element)
        {
            if (element.Name.LocalName != "remarks")
            {
                return false;
            }

            var paragraphs = element.Elements().ToList();
            var hasDirectPlaceholder = IsPlaceholder(
                string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value)));
            var emptyParagraphs = paragraphs
                .Where(paragraph => !paragraph.HasElements && NormalizeText(paragraph.Value).Length == 0)
                .ToList();
            if (!hasDirectPlaceholder && emptyParagraphs.Count != 1)
                return false;

            var metadataParagraphs = paragraphs.Except(emptyParagraphs).ToList();
            return metadataParagraphs.Count > 0 &&
                metadataParagraphs.All(paragraph =>
                    paragraph.Name.LocalName == "para" &&
                    (paragraph.Descendants("a").Any(link =>
                        (string?)link.Attribute("title") == "Reference documentation" &&
                        (((string?)link.Attribute("href"))?.StartsWith(
                            "https://developer.android.com/reference/",
                            StringComparison.Ordinal) == true ||
                        ((string?)link.Attribute("href"))?.StartsWith(
                            "https://docs.oracle.com/en/java/javase/21/docs/api/",
                            StringComparison.Ordinal) == true)) ||
                    (paragraph.Value.StartsWith(
                        "Portions of this page are modifications based on work created and shared by",
                        StringComparison.Ordinal) &&
                        paragraph.Descendants("a").Any(link =>
                            (string?)link.Attribute("href") ==
                                "https://developers.google.com/terms/site-policies"))));
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
        MemberRegistration? MemberRegistration,
        SourceRequest? SourceRequest,
        List<Placeholder> Placeholders,
        bool IsEnumField);

    sealed record Placeholder(
        int Order,
        string Name,
        string Key,
        string Target,
        bool IsImporterMetadataRepair = false)
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
            return new Placeholder(
                order,
                name,
                key,
                target,
                LoadedFile.IsImporterAugmentedRemarksPlaceholder(element));
        }
    }

    sealed record MemberRegistration(string Name, string? Descriptor, bool IsField);
    sealed record JniFieldRegistration(string Owner, string Name);

    static class Registration
    {
        static readonly Regex TypeRegex = new(
            @"Register\(""(?<name>[^""]+)""",
            RegexOptions.CultureInvariant);
        static readonly Regex JniTypeRegex = new(
            @"JniTypeSignature\s*\(\s*""(?<name>[^""]+)""(?<options>[^)]*)\)",
            RegexOptions.CultureInvariant);
        static readonly Regex ArrayRankRegex = new(
            @"\bArrayRank\s*=\s*(?<rank>\d+)",
            RegexOptions.CultureInvariant);
        static readonly Regex MemberRegex = new(
            @"Register\(""(?<name>[^""]+)""\s*,\s*""(?<descriptor>[^""]*)""",
            RegexOptions.CultureInvariant);
        static readonly Regex JniConstructorRegex = new(
            @"JniConstructorSignature\s*\(\s*""(?<descriptor>[^""]*)""",
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
                match = JniTypeRegex.Match(attribute.Value);
                if (!match.Success)
                    continue;
                var arrayRank = ArrayRankRegex.Match(match.Groups["options"].Value);
                if (!arrayRank.Success || arrayRank.Groups["rank"].Value == "0")
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
                match = JniConstructorRegex.Match(attribute.Value);
                if (match.Success)
                    return new MemberRegistration(
                        ".ctor",
                        match.Groups["descriptor"].Value,
                        false);
            }
            var memberType = member.Element("MemberType")?.Value;
            if (memberType is "Field" or "Property")
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

    sealed record InterfaceMemberMapping(
        MemberRegistration Registration,
        SourceRequest SourceRequest,
        bool UseFirstMeaningfulSummary = false,
        bool FilterSynchronousGeocoderBoilerplate = false);

    static class SourceVerifiedMemberMappings
    {
        static readonly IReadOnlyDictionary<string, SourceRequest> SourceOverrides =
            new Dictionary<string, SourceRequest>(StringComparer.Ordinal)
            {
                ["F:Java.Util.Regex.RegexOptions.UnicodeCharacterClass"] =
                    SourceRequest.CreateAndroid("java/util/regex/Pattern") ??
                        throw new InvalidOperationException(
                            "Could not create the Android Pattern source request."),
            };

        static readonly IReadOnlyDictionary<string, InterfaceMemberMapping> Mappings =
            new Dictionary<string, InterfaceMemberMapping>(StringComparer.Ordinal)
            {
                ["M:Java.Interop.JavaException.#ctor"] =
                    Mapping("java/lang/Throwable", ".ctor", "()V"),
                ["M:Java.Interop.JavaException.#ctor(System.String)"] =
                    Mapping("java/lang/Throwable", ".ctor", "(Ljava/lang/String;)V"),
                ["M:Java.Interop.JavaException.GetHashCode"] =
                    Mapping("java/lang/Object", "hashCode", "()I"),
                ["P:Java.Interop.JavaException.JniIdentityHashCode"] =
                    Mapping("java/lang/System", "identityHashCode", "(Ljava/lang/Object;)I"),
                ["M:Java.Interop.JavaObject.GetHashCode"] =
                    Mapping("java/lang/Object", "hashCode", "()I"),
                ["P:Java.Interop.JavaObject.JniIdentityHashCode"] =
                    Mapping("java/lang/System", "identityHashCode", "(Ljava/lang/Object;)I"),
                ["M:Java.Interop.JavaObject.ToString"] =
                    Mapping("java/lang/Object", "toString", "()Ljava/lang/String;"),
                ["M:Java.Interop.JniEnvironment.Object.ToString(Java.Interop.JniObjectReference)"] =
                    Mapping("java/lang/Object", "toString", "()Ljava/lang/String;"),
                ["M:Java.Interop.JniEnvironment.References.GetIdentityHashCode(Java.Interop.JniObjectReference)"] =
                    Mapping("java/lang/System", "identityHashCode", "(Ljava/lang/Object;)I"),
                ["M:Android.Locations.Geocoder.GetFromLocationAsync(System.Double,System.Double,System.Int32)"] =
                    Mapping("android/location/Geocoder", "getFromLocation", "(DDI)Ljava/util/List;",
                        useFirstMeaningfulSummary: true,
                        filterSynchronousGeocoderBoilerplate: true),
                ["M:Android.Locations.Geocoder.GetFromLocationAsync(System.Double,System.Double,System.Int32,Android.Locations.Geocoder.IGeocodeListener)"] =
                    Mapping("android/location/Geocoder", "getFromLocation", "(DDILandroid/location/Geocoder$GeocodeListener;)V"),
                ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32)"] =
                    Mapping("android/location/Geocoder", "getFromLocationName", "(Ljava/lang/String;I)Ljava/util/List;",
                        useFirstMeaningfulSummary: true,
                        filterSynchronousGeocoderBoilerplate: true),
                ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,Android.Locations.Geocoder.IGeocodeListener)"] =
                    Mapping("android/location/Geocoder", "getFromLocationName", "(Ljava/lang/String;ILandroid/location/Geocoder$GeocodeListener;)V"),
                ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double)"] =
                    Mapping("android/location/Geocoder", "getFromLocationName", "(Ljava/lang/String;IDDDD)Ljava/util/List;",
                        useFirstMeaningfulSummary: true,
                        filterSynchronousGeocoderBoilerplate: true),
                ["M:Android.Locations.Geocoder.GetFromLocationNameAsync(System.String,System.Int32,System.Double,System.Double,System.Double,System.Double,Android.Locations.Geocoder.IGeocodeListener)"] =
                    Mapping("android/location/Geocoder", "getFromLocationName", "(Ljava/lang/String;IDDDDLandroid/location/Geocoder$GeocodeListener;)V"),
                ["M:Android.Text.TextUtils.IndexOf(System.String,System.Char)"] =
                    Mapping("android/text/TextUtils", "indexOf", "(Ljava/lang/CharSequence;C)I"),
                ["M:Android.Text.TextUtils.IndexOf(System.String,System.String)"] =
                    Mapping("android/text/TextUtils", "indexOf", "(Ljava/lang/CharSequence;Ljava/lang/CharSequence;)I"),
                ["M:Android.Text.TextUtils.IndexOf(System.String,System.Char,System.Int32)"] =
                    Mapping("android/text/TextUtils", "indexOf", "(Ljava/lang/CharSequence;CI)I"),
                ["M:Android.Text.TextUtils.IndexOf(System.String,System.String,System.Int32)"] =
                    Mapping("android/text/TextUtils", "indexOf", "(Ljava/lang/CharSequence;Ljava/lang/CharSequence;I)I"),
                ["M:Android.Text.TextUtils.IndexOf(System.String,System.Char,System.Int32,System.Int32)"] =
                    Mapping("android/text/TextUtils", "indexOf", "(Ljava/lang/CharSequence;CII)I"),
                ["M:Android.Text.TextUtils.IndexOf(System.String,System.String,System.Int32,System.Int32)"] =
                    Mapping("android/text/TextUtils", "indexOf", "(Ljava/lang/CharSequence;Ljava/lang/CharSequence;II)I"),
                ["M:Android.Text.TextUtils.LastIndexOf(System.String,System.Char)"] =
                    Mapping("android/text/TextUtils", "lastIndexOf", "(Ljava/lang/CharSequence;C)I"),
                ["M:Android.Text.TextUtils.LastIndexOf(System.String,System.Char,System.Int32)"] =
                    Mapping("android/text/TextUtils", "lastIndexOf", "(Ljava/lang/CharSequence;CI)I"),
                ["M:Android.Text.TextUtils.LastIndexOf(System.String,System.Char,System.Int32,System.Int32)"] =
                    Mapping("android/text/TextUtils", "lastIndexOf", "(Ljava/lang/CharSequence;CII)I"),
                ["M:Android.Text.Style.BulletSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    Mapping("android/text/style/BulletSpan", "drawLeadingMargin", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;IIZLandroid/text/Layout;)V"),
                ["M:Android.Text.Style.DrawableMarginSpan.ChooseHeight(System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    Mapping("android/text/style/DrawableMarginSpan", "chooseHeight", "(Ljava/lang/CharSequence;IIIILandroid/graphics/Paint$FontMetricsInt;)V"),
                ["M:Android.Text.Style.DrawableMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    Mapping("android/text/style/DrawableMarginSpan", "drawLeadingMargin", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;IIZLandroid/text/Layout;)V"),
                ["M:Android.Text.Style.IconMarginSpan.ChooseHeight(System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    Mapping("android/text/style/IconMarginSpan", "chooseHeight", "(Ljava/lang/CharSequence;IIIILandroid/graphics/Paint$FontMetricsInt;)V"),
                ["M:Android.Text.Style.IconMarginSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    Mapping("android/text/style/IconMarginSpan", "drawLeadingMargin", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;IIZLandroid/text/Layout;)V"),
                ["M:Android.Text.Style.LeadingMarginSpanStandard.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    Mapping("android/text/style/LeadingMarginSpan$Standard", "drawLeadingMargin", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;IIZLandroid/text/Layout;)V"),
                ["M:Android.Text.Style.LineBackgroundSpanStandard.DrawBackground(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Int32)"] =
                    Mapping("android/text/style/LineBackgroundSpan$Standard", "drawBackground", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;III)V"),
                ["M:Android.Text.Style.LineHeightSpanStandard.ChooseHeight(System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    Mapping("android/text/style/LineHeightSpan$Standard", "chooseHeight", "(Ljava/lang/CharSequence;IIIILandroid/graphics/Paint$FontMetricsInt;)V"),
                ["M:Android.Text.Style.QuoteSpan.DrawLeadingMargin(Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    Mapping("android/text/style/QuoteSpan", "drawLeadingMargin", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;IIZLandroid/text/Layout;)V"),
                ["P:Android.Text.Style.ReplacementSpan.ContentDescription"] =
                    Mapping("android/text/style/ReplacementSpan", "getContentDescription", "()Ljava/lang/CharSequence;"),
                ["M:Android.Text.Style.ILeadingMarginSpanExtensions.DrawLeadingMargin(Android.Text.Style.ILeadingMarginSpan,Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Boolean,Android.Text.Layout)"] =
                    Mapping("android/text/style/LeadingMarginSpan", "drawLeadingMargin", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;IIZLandroid/text/Layout;)V"),
                ["M:Android.Text.Style.ILineBackgroundSpanExtensions.DrawBackground(Android.Text.Style.ILineBackgroundSpan,Android.Graphics.Canvas,Android.Graphics.Paint,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.String,System.Int32,System.Int32,System.Int32)"] =
                    Mapping("android/text/style/LineBackgroundSpan", "drawBackground", "(Landroid/graphics/Canvas;Landroid/graphics/Paint;IIIIILjava/lang/CharSequence;III)V"),
                ["M:Android.Text.Style.ILineHeightSpanExtensions.ChooseHeight(Android.Text.Style.ILineHeightSpan,System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt)"] =
                    Mapping("android/text/style/LineHeightSpan", "chooseHeight", "(Ljava/lang/CharSequence;IIIILandroid/graphics/Paint$FontMetricsInt;)V"),
                ["M:Android.Text.Style.ILineHeightSpanWithDensityExtensions.ChooseHeight(Android.Text.Style.ILineHeightSpanWithDensity,System.String,System.Int32,System.Int32,System.Int32,System.Int32,Android.Graphics.Paint.FontMetricsInt,Android.Text.TextPaint)"] =
                    Mapping("android/text/style/LineHeightSpan$WithDensity", "chooseHeight", "(Ljava/lang/CharSequence;IIIILandroid/graphics/Paint$FontMetricsInt;Landroid/text/TextPaint;)V"),
                ["M:Android.Telecom.PhoneAccount.Builder.SetShortDescription(System.String)"] =
                    Mapping("android/telecom/PhoneAccount$Builder", "setShortDescription", "(Ljava/lang/CharSequence;)Landroid/telecom/PhoneAccount$Builder;"),
                ["M:Android.Telecom.PhoneAccount.InvokeBuilder(Android.Telecom.PhoneAccountHandle,System.String)"] =
                    Mapping("android/telecom/PhoneAccount", "builder", "(Landroid/telecom/PhoneAccountHandle;Ljava/lang/CharSequence;)Landroid/telecom/PhoneAccount$Builder;"),
                ["P:Android.Telecom.CallAttributes.DisplayName"] =
                    Mapping("android/telecom/CallAttributes", "getDisplayName", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.CallEndpoint.EndpointName"] =
                    Mapping("android/telecom/CallEndpoint", "getEndpointName", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.DisconnectCause.Description"] =
                    Mapping("android/telecom/DisconnectCause", "getDescription", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.DisconnectCause.Label"] =
                    Mapping("android/telecom/DisconnectCause", "getLabel", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.PhoneAccount.Label"] =
                    Mapping("android/telecom/PhoneAccount", "getLabel", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.PhoneAccount.ShortDescription"] =
                    Mapping("android/telecom/PhoneAccount", "getShortDescription", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.RemoteConnection.CallerDisplayName"] =
                    Mapping("android/telecom/RemoteConnection", "getCallerDisplayName", "()Ljava/lang/CharSequence;"),
                ["P:Android.Telecom.StatusHints.Label"] =
                    Mapping("android/telecom/StatusHints", "getLabel", "()Ljava/lang/CharSequence;"),
                ["M:Android.Views.InputMethods.BaseInputConnection.CommitText(System.String,System.Int32)"] =
                    Mapping("android/view/inputmethod/BaseInputConnection", "commitText", "(Ljava/lang/CharSequence;I)Z"),
                ["M:Android.Views.InputMethods.BaseInputConnection.ReplaceText(System.Int32,System.Int32,System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/BaseInputConnection", "replaceText", "(IILjava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
                ["M:Android.Views.InputMethods.BaseInputConnection.SetComposingText(System.String,System.Int32)"] =
                    Mapping("android/view/inputmethod/BaseInputConnection", "setComposingText", "(Ljava/lang/CharSequence;I)Z"),
                ["M:Android.Views.InputMethods.CursorAnchorInfo.Builder.SetComposingText(System.Int32,System.String)"] =
                    Mapping("android/view/inputmethod/CursorAnchorInfo$Builder", "setComposingText", "(ILjava/lang/CharSequence;)Landroid/view/inputmethod/CursorAnchorInfo$Builder;"),
                ["M:Android.Views.InputMethods.InputConnectionWrapper.CommitText(System.String,System.Int32)"] =
                    Mapping("android/view/inputmethod/InputConnectionWrapper", "commitText", "(Ljava/lang/CharSequence;I)Z"),
                ["M:Android.Views.InputMethods.InputConnectionWrapper.CommitText(System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/InputConnectionWrapper", "commitText", "(Ljava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
                ["M:Android.Views.InputMethods.InputConnectionWrapper.ReplaceText(System.Int32,System.Int32,System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/InputConnectionWrapper", "replaceText", "(IILjava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
                ["M:Android.Views.InputMethods.InputConnectionWrapper.SetComposingText(System.String,System.Int32)"] =
                    Mapping("android/view/inputmethod/InputConnectionWrapper", "setComposingText", "(Ljava/lang/CharSequence;I)Z"),
                ["M:Android.Views.InputMethods.InputConnectionWrapper.SetComposingText(System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/InputConnectionWrapper", "setComposingText", "(Ljava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
                ["M:Android.Views.InputMethods.InputMethodSubtype.InputMethodSubtypeBuilder.SetLayoutLabelNonLocalized(System.String)"] =
                    Mapping("android/view/inputmethod/InputMethodSubtype$InputMethodSubtypeBuilder", "setLayoutLabelNonLocalized", "(Ljava/lang/CharSequence;)Landroid/view/inputmethod/InputMethodSubtype$InputMethodSubtypeBuilder;"),
                ["M:Android.Views.InputMethods.InputMethodSubtype.InputMethodSubtypeBuilder.SetSubtypeNameOverride(System.String)"] =
                    Mapping("android/view/inputmethod/InputMethodSubtype$InputMethodSubtypeBuilder", "setSubtypeNameOverride", "(Ljava/lang/CharSequence;)Landroid/view/inputmethod/InputMethodSubtype$InputMethodSubtypeBuilder;"),
                ["M:Android.Views.TextClassifiers.ITextClassifierExtensions.ClassifyText(Android.Views.TextClassifiers.ITextClassifier,System.String,System.Int32,System.Int32,Android.OS.LocaleList)"] =
                    Mapping("android/view/textclassifier/TextClassifier", "classifyText", "(Ljava/lang/CharSequence;IILandroid/os/LocaleList;)Landroid/view/textclassifier/TextClassification;"),
                ["M:Android.Views.TextClassifiers.ITextClassifierExtensions.SuggestSelection(Android.Views.TextClassifiers.ITextClassifier,System.String,System.Int32,System.Int32,Android.OS.LocaleList)"] =
                    Mapping("android/view/textclassifier/TextClassifier", "suggestSelection", "(Ljava/lang/CharSequence;IILandroid/os/LocaleList;)Landroid/view/textclassifier/TextSelection;"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.CommitText(Android.Views.InputMethods.IInputConnection,System.String,System.Int32)"] =
                    Mapping("android/view/inputmethod/InputConnection", "commitText", "(Ljava/lang/CharSequence;I)Z"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.CommitText(Android.Views.InputMethods.IInputConnection,System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/InputConnection", "commitText", "(Ljava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.GetSelectedText(Android.Views.InputMethods.IInputConnection,Android.Views.InputMethods.GetTextFlags)"] =
                    Mapping("android/view/inputmethod/InputConnection", "getSelectedText", "(I)Ljava/lang/CharSequence;"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.GetTextAfterCursor(Android.Views.InputMethods.IInputConnection,System.Int32,Android.Views.InputMethods.GetTextFlags)"] =
                    Mapping("android/view/inputmethod/InputConnection", "getTextAfterCursor", "(II)Ljava/lang/CharSequence;"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.GetTextBeforeCursor(Android.Views.InputMethods.IInputConnection,System.Int32,Android.Views.InputMethods.GetTextFlags)"] =
                    Mapping("android/view/inputmethod/InputConnection", "getTextBeforeCursor", "(II)Ljava/lang/CharSequence;"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.ReplaceText(Android.Views.InputMethods.IInputConnection,System.Int32,System.Int32,System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/InputConnection", "replaceText", "(IILjava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.SetComposingText(Android.Views.InputMethods.IInputConnection,System.String,System.Int32)"] =
                    Mapping("android/view/inputmethod/InputConnection", "setComposingText", "(Ljava/lang/CharSequence;I)Z"),
                ["M:Android.Views.InputMethods.IInputConnectionExtensions.SetComposingText(Android.Views.InputMethods.IInputConnection,System.String,System.Int32,Android.Views.InputMethods.TextAttribute)"] =
                    Mapping("android/view/inputmethod/InputConnection", "setComposingText", "(Ljava/lang/CharSequence;ILandroid/view/inputmethod/TextAttribute;)Z"),
            };

        public static InterfaceMemberMapping? Resolve(string memberId) =>
            Mappings.GetValueOrDefault(memberId);

        public static SourceRequest? ResolveSourceOverride(string memberId) =>
            SourceOverrides.GetValueOrDefault(memberId);

        static InterfaceMemberMapping Mapping(
            string javaPath,
            string name,
            string descriptor,
            bool useFirstMeaningfulSummary = false,
            bool filterSynchronousGeocoderBoilerplate = false) =>
            new(
                new MemberRegistration(name, descriptor, false),
                SourceRequest.Create(javaPath) ??
                    throw new InvalidOperationException(
                        $"Unsupported source-verified Java path '{javaPath}'."),
                useFirstMeaningfulSummary,
                filterSynchronousGeocoderBoilerplate);
    }

    sealed class InterfaceMemberResolver
    {
        readonly string docsRoot;
        readonly Dictionary<string, InterfaceMemberMapping?> cache =
            new(StringComparer.Ordinal);

        public InterfaceMemberResolver(string docsRoot) => this.docsRoot = docsRoot;

        public InterfaceMemberMapping? Resolve(XElement member)
        {
            foreach (var reference in member
                .Element("Implements")?
                .Elements("InterfaceMember")
                .Select(item => item.Value)
                .Where(value => value.Length > 0) ?? [])
            {
                if (!cache.TryGetValue(reference, out var mapping))
                {
                    mapping = ResolveReference(reference);
                    cache[reference] = mapping;
                }
                if (mapping is not null)
                    return mapping;
            }
            return null;
        }

        InterfaceMemberMapping? ResolveReference(string reference)
        {
            var signatureEnd = reference.IndexOf('(');
            var memberReference = signatureEnd >= 0
                ? reference[..signatureEnd]
                : reference;
            var separator = memberReference.LastIndexOf('.');
            if (reference.Length < 3 || separator <= 2)
                return null;

            var typeName = memberReference[2..separator];
            var typeSeparator = typeName.LastIndexOf('.');
            if (typeSeparator <= 0)
                return null;

            var path = Path.Combine(
                docsRoot,
                typeName[..typeSeparator],
                typeName[(typeSeparator + 1)..] + ".xml");
            if (!File.Exists(path))
                return null;

            try
            {
                var document = XDocument.Load(path);
                var root = document.Root;
                if (root is null || !string.Equals(
                    (string?)root.Attribute("FullName"),
                    typeName,
                    StringComparison.Ordinal))
                    return null;

                var interfaceMember = root
                    .Element("Members")?
                    .Elements("Member")
                    .SingleOrDefault(item => item.Elements("MemberSignature").Any(signature =>
                        (string?)signature.Attribute("Language") == "DocId" &&
                        (string?)signature.Attribute("Value") == reference));
                if (interfaceMember is null)
                    return null;

                var registration = Registration.Member(interfaceMember);
                var request = SourceRequest.Create(Registration.Type(root));
                return registration is null || request is null
                    ? null
                    : new InterfaceMemberMapping(registration, request);
            }
            catch (Exception error) when (error is XmlException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
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

        public static SourceRequest? CreateAndroid(string? javaPath)
        {
            if (string.IsNullOrWhiteSpace(javaPath))
                return null;
            var urlPath = javaPath.Replace('$', '.');
            return new SourceRequest(javaPath, AndroidReference + urlPath, "android");
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
                    ExtractAndroidDocs(fragment, request, heading.Title, url),
                    url));
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
                FirstSentence(paragraphs[0].Text),
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
                    .Select(cell => HtmlTableCellText(cell.Groups["cell"].Value))
                    .ToList();
                if (cells.Count >= 2 && Regex.IsMatch(cells[0], @"^[A-Za-z_]\w*$"))
                    parameters.TryAdd(cells[0], cells[1]);
            }

            var returns = ExtractAndroidTableValue(fragment, "Returns");
            var exceptions = ExtractAndroidExceptions(fragment);
            var prose = Regex.Replace(
                fragment,
                @"<table\b[^>]*>.*?</table>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            prose = Regex.Replace(
                prose,
                @"<pre\b(?=[^>]*\bclass=[""'][^""']*\bapi-signature\b[^""']*[""'])[^>]*>.*?</pre>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var paragraphs = ExtractParagraphs(prose);
            if (paragraphs.Count == 0 &&
                parameters.Count == 0 &&
                returns.Length == 0 &&
                exceptions.Count == 0)
                return null;
            return new SourceDocs(
                paragraphs.Count > 0 ? FirstSentence(paragraphs[0].Text) : "",
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
                            .Select(cell => HtmlTableCellText(cell.Groups["cell"].Value))
                            .ToList(),
                        Headings = Regex.Matches(
                            row.Groups["row"].Value,
                            @"<th\b[^>]*>(?<cell>.*?)</th>",
                            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                            .Select(cell => HtmlTableCellText(cell.Groups["cell"].Value))
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
                @"<section\b(?=[^>]*\bclass=""[^""]*\bdetail\b[^""]*"")(?<attrs>[^>]*)>(?<body>.*?)</section>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var attributes = ParseAttributes(section.Groups["attrs"].Value);
                if (!attributes.TryGetValue("class", out var classes) ||
                    !classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("detail") ||
                    !attributes.TryGetValue("id", out var encodedAnchor))
                    continue;
                var anchor = WebUtility.HtmlDecode(encodedAnchor);
                var isField = !anchor.Contains('(', StringComparison.Ordinal);
                var body = section.Groups["body"].Value;
                var heading = Regex.Match(
                    body,
                    @"<h3\b(?<attrs>[^>]*)>(?<name>.*?)</h3>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var detailAnchor = anchor;
                if (heading.Success)
                {
                    var headingAttributes = ParseAttributes(heading.Groups["attrs"].Value);
                    if (headingAttributes.TryGetValue("id", out var headingAnchor))
                        detailAnchor = headingAnchor;
                }
                var arguments = isField
                    ? null
                    : Descriptor.FromAnchor(detailAnchor, request.JavaPath);
                if (!isField && arguments is null)
                    continue;
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
                    ExtractJavaDocs(body, request, displayName, url),
                    url));
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
                FirstSentence(paragraphs[0].Text),
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
            var notes = Regex.Match(
                body,
                @"<dl\b[^>]*class=""[^""]*\bnotes\b[^""]*""[^>]*>(?<notes>.*?)</dl>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var noteBody = notes.Success ? notes.Groups["notes"].Value : "";
            var parameters = ExtractJavaParameters(noteBody);
            var returns = ExtractJavaNoteValue(noteBody, "Returns:");
            var exceptions = ExtractJavaExceptions(noteBody);
            if (paragraphs.Count == 0 &&
                parameters.Count == 0 &&
                returns.Length == 0 &&
                exceptions.Count == 0)
                return null;
            return new SourceDocs(
                paragraphs.Count > 0 ? FirstSentence(paragraphs[0].Text) : "",
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

        internal static List<SourceParagraph> ExtractParagraphs(string html)
        {
            html = NormalizeHtmlLists(html);
            html = NormalizeNestedListParagraphs(html);
            html = Regex.Replace(
                html,
                @"</p>\s*\.\s*<br>\s*(?=Requires\b)",
                "<br>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var paragraphs = new List<(int Position, SourceParagraph Paragraph)>();
            var codeRanges = new List<(int Start, int End, SourceParagraph Paragraph)>();
            var stack = new Stack<(string Tag, int TagStart, int ContentStart)>();

            void CompleteElement(
                (string Tag, int TagStart, int ContentStart) open,
                int contentEnd,
                int elementEnd)
            {
                var isCode = open.Tag.Equals("pre", StringComparison.OrdinalIgnoreCase) ||
                    open.Tag.Equals("devsite-code", StringComparison.OrdinalIgnoreCase);
                if (isCode)
                {
                    var value = HtmlCodeText(html[open.ContentStart..contentEnd]);
                    if (value.Length > 0)
                        codeRanges.Add((open.TagStart, elementEnd, new SourceParagraph(value, IsCode: true)));
                    return;
                }

                var nestedCode = codeRanges
                    .Where(code => code.Start >= open.ContentStart && code.End <= contentEnd)
                    .OrderBy(code => code.Start)
                    .ToList();
                var textStart = open.ContentStart;
                foreach (var code in nestedCode)
                {
                    if (code.Start < textStart)
                        continue;
                    AddSourceTextParagraph(html[textStart..code.Start], textStart, paragraphs);
                    paragraphs.Add((code.Start, code.Paragraph));
                    textStart = code.End;
                }
                if (textStart <= contentEnd)
                    AddSourceTextParagraph(html[textStart..contentEnd], textStart, paragraphs);
            }

            foreach (Match tag in Regex.Matches(
                html,
                @"<(?<close>/)?(?<tag>p|pre|devsite-code|ul|ol)\b[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var name = tag.Groups["tag"].Value;
                if (name.Equals("ul", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ol", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!tag.Groups["close"].Success)
                {
                    // HTML permits omitted </p>; a nested paragraph starts a new block.
                    if (name.Equals("p", StringComparison.OrdinalIgnoreCase) &&
                        stack.Count > 0 &&
                        stack.Peek().Tag.Equals("p", StringComparison.OrdinalIgnoreCase))
                    {
                        CompleteElement(stack.Pop(), tag.Index, tag.Index);
                    }
                    stack.Push((name, tag.Index, tag.Index + tag.Length));
                    continue;
                }

                if (stack.Count == 0)
                    continue;
                var open = stack.Pop();
                if (!open.Tag.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                CompleteElement(open, tag.Index, tag.Index + tag.Length);
            }
            foreach (var code in codeRanges.Where(code =>
                !paragraphs.Any(paragraph => paragraph.Position == code.Start &&
                    paragraph.Paragraph == code.Paragraph)))
            {
                paragraphs.Add((code.Start, code.Paragraph));
            }
            return paragraphs
                .OrderBy(paragraph => paragraph.Position)
                .Select(paragraph => paragraph.Paragraph)
                .Distinct()
                .ToList();
        }

        static string NormalizeNestedListParagraphs(string html) =>
            Regex.Replace(
                html,
                @"(?<open><(?:ul|ol)\b[^>]*>)(?<body>.*?)(?<close></(?:ul|ol)>|$)",
                match =>
                    match.Groups["open"].Value +
                    Regex.Replace(
                        match.Groups["body"].Value,
                        @"</?p\b[^>]*>",
                        "<br>",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) +
                    match.Groups["close"].Value,
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static void AddSourceTextParagraph(
            string html,
            int position,
            List<(int Position, SourceParagraph Paragraph)> paragraphs)
        {
            var value = CleanSourceParagraph(HtmlText(html));
            if (IsMeaningfulChannel(value, "remarks"))
                paragraphs.Add((position, new SourceParagraph(value, IsCode: false)));
        }

        static List<SourceParagraph> ExtractBlocks(string html) =>
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
                .Select(match => new SourceParagraph(HtmlText(match.Groups["body"].Value), IsCode: false))
                .Where(paragraph => paragraph.Text.Length > 0)
                .Distinct()
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

        internal static string HtmlText(string html, bool includeCode = false)
            => HtmlTextCore(NormalizeHtmlLists(html), includeCode);

        static string NormalizeHtmlLists(string html)
        {
            html = Regex.Replace(
                html,
                @"<ul\b[^>]*\bclass=""[^""]*\bnolist\b[^""]*""[^>]*>.*?</ul>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            html = Regex.Replace(
                html,
                @"(?<introOpen><p\b[^>]*>)(?<introBody>.*?)</p>\s*<(?<tag>ul|ol)\b[^>]*>(?<body>.*?)</\k<tag>\s*>\s*(?=<p\b)",
                match =>
                {
                    var introduction = HtmlTextCore(match.Groups["introBody"].Value);
                    var separator = introduction.EndsWith(":", StringComparison.Ordinal) ? " " : "; ";
                    var items = Regex.Matches(
                        match.Groups["body"].Value,
                        @"<li\b[^>]*>(?<body>.*?)(?=<li\b|</li\b|</(?:ul|ol)\b|$)",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                        .Select(item =>
                        {
                            var value = HtmlTextCore(
                                item.Groups["body"].Value,
                                includeCode: true);
                            return Regex.IsMatch(
                                item.Groups["body"].Value,
                                @"</code>\s*$",
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                                ? value
                                : value.TrimEnd('.', ' ');
                        })
                        .Where(value => value.Length > 0)
                        .ToList();
                    var listText = string.Join("; ", items);
                    return match.Groups["introOpen"].Value +
                        match.Groups["introBody"].Value +
                        separator +
                        WebUtility.HtmlEncode(listText) +
                        "</p>";
                },
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var listMatches = Regex.Matches(
                html,
                @"<li\b[^>]*>(?<body>.*?)(?=<li\b|</li\b|</(?:ul|ol)\b|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (listMatches.Count > 0)
            {
                var listItemIndex = 0;
                html = Regex.Replace(
                    html,
                    @"<li\b[^>]*>(?<body>.*?)(?=<li\b|</li\b|</(?:ul|ol)\b|$)",
                    match =>
                    {
                        var item = HtmlTextCore(
                            match.Groups["body"].Value,
                            includeCode: true);
                        if (item.Length == 0)
                            return "";
                        if (listItemIndex + 1 < listMatches.Count &&
                            !Regex.IsMatch(
                                match.Groups["body"].Value,
                                @"</code>\s*$",
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                            item = item.TrimEnd('.', ' ');
                        return (listItemIndex++ == 0 ? " " : "; ") +
                            WebUtility.HtmlEncode(item);
                    },
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            return html;
        }

        static string HtmlTextCore(string html, bool includeCode = false)
        {
            var repairedMalformedHref = Regex.Replace(
                html,
                @"(?<prefix>\bhref\s*=\s*"")(?<url>[^""\s>]+)>(?=\s*[A-Za-z])",
                match =>
                    $"{match.Groups["prefix"].Value}{match.Groups["url"].Value}\">",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var withoutIgnored = Regex.Replace(
                repairedMalformedHref,
                includeCode
                    ? @"<(?:script|style|svg)\b[^>]*>.*?</(?:script|style|svg)>"
                    : @"<(?:script|style|svg|pre|devsite-code)\b[^>]*>.*?</(?:script|style|svg|pre|devsite-code)>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var withBreaks = Regex.Replace(
                withoutIgnored,
                @"</?(?:p|div|li|tr|td|th|dd|dt|br|ul|ol|blockquote)\b[^>]*>",
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return CleanSourceText(StripHtmlTags(withBreaks, addWhitespace: false));
        }

        static string HtmlTableCellText(string html)
        {
            var listItems = Regex.Matches(
                html,
                @"<li\b[^>]*>(?<body>.*?)(?=<li\b|</(?:ul|ol)\b|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(match => HtmlText(match.Groups["body"].Value))
                .Where(item => item.Length > 0)
                .ToList();
            if (listItems.Count == 0)
                return HtmlText(html);

            var withoutListItems = Regex.Replace(
                html,
                @"<li\b[^>]*>.*?(?=<li\b|</(?:ul|ol)\b|$)",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var listIntroduction = HtmlText(withoutListItems);
            return listIntroduction.EndsWith(":", StringComparison.Ordinal)
                ? CleanSourceText($"{listIntroduction} {string.Join("; ", listItems)}")
                : Regex.IsMatch(
                    listIntroduction,
                    @"\bor$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    ? CleanSourceText($"{listIntroduction} {string.Join("; ", listItems)}")
                    : HtmlText(html);
        }

        static string HtmlCodeText(string html)
        {
            var withoutIgnored = Regex.Replace(
                html,
                @"<(?:script|style|svg)\b[^>]*>.*?</(?:script|style|svg)>",
                "",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var code = StripHtmlTags(withoutIgnored, addWhitespace: false);
            code = WebUtility.HtmlDecode(code)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            code = Regex.Replace(
                code,
                @"\{@code\s+(?<value>.*?)\}",
                match => match.Groups["value"].Value,
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            return string.Join(
                    "\n",
                    code.Split('\n').Select(line => line.TrimEnd()))
                .Trim('\n');
        }

        static string StripHtmlTags(string html, bool addWhitespace = true)
        {
            var text = new StringBuilder(html.Length);
            var inTag = false;
            var quote = '\0';
            for (var index = 0; index < html.Length; index++)
            {
                var character = html[index];
                if (!inTag)
                {
                    if (character == '<' &&
                        index + 1 < html.Length &&
                        (char.IsLetter(html[index + 1]) ||
                         html[index + 1] is '/' or '!' or '?'))
                    {
                        inTag = true;
                        quote = '\0';
                        if (addWhitespace)
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

        internal static string FirstSentence(string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] is not ('.' or '!' or '?') ||
                    !IsSentenceBoundary(text, index) ||
                    (text[index] == '.' && (IsAbbreviation(text, index) || IsEllipsis(text, index))))
                {
                    continue;
                }

                var end = index + 1;
                while (end < text.Length && text[end] is ')' or ']' or '}')
                    end++;
                return text[..end];
            }

            return text;
        }

        static bool IsSentenceBoundary(string text, int punctuationIndex)
        {
            var next = punctuationIndex + 1;
            while (next < text.Length && text[next] is ')' or ']' or '}')
                next++;
            return next == text.Length || char.IsWhiteSpace(text[next]);
        }

        static bool IsEllipsis(string text, int periodIndex) =>
            (periodIndex > 0 && text[periodIndex - 1] == '.') ||
            (periodIndex + 1 < text.Length && text[periodIndex + 1] == '.');

        static bool IsAbbreviation(string text, int periodIndex)
        {
            var tokenStart = periodIndex;
            while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1]))
                tokenStart--;
            var token = text[tokenStart..(periodIndex + 1)]
                .Trim('(', ')', '[', ']', '{', '}', '"', '\'');
            var isAbbreviation = token.Equals("e.g.", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("i.e.", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("vs.", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("etc.", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(
                    token,
                    @"^(?:[A-Za-z]\.){2,}$",
                    RegexOptions.CultureInvariant);
            if (HasClosingDelimiterBoundary(text, periodIndex))
            {
                var next = periodIndex + 1;
                while (next < text.Length && text[next] is ')' or ']' or '}')
                    next++;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                    next++;
                return isAbbreviation && next < text.Length && char.IsLower(text[next]);
            }
            return isAbbreviation;
        }

        static bool HasClosingDelimiterBoundary(string text, int periodIndex)
        {
            var next = periodIndex + 1;
            var hasClosingDelimiter = false;
            while (next < text.Length && text[next] is ')' or ']' or '}')
            {
                    hasClosingDelimiter = true;
                    next++;
            }
            return hasClosingDelimiter &&
                    (next == text.Length || char.IsWhiteSpace(text[next]));

        }
    }

    sealed record SourceMember(
        string Name,
        bool IsConstructor,
        bool IsField,
        List<string>? ArgumentDescriptors,
        SourceDocs? Docs,
        string Url);

    sealed record SourceParagraph(string Text, bool IsCode);

    sealed record SourceDocs(
        string Summary,
        List<SourceParagraph> Paragraphs,
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
            else if (Regex.IsMatch(
                value,
                @"^[A-Z]$",
                RegexOptions.CultureInvariant))
            {
                descriptor = "Ljava/lang/Object;";
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
