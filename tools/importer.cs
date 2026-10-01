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
    const string DreamFocusSourceUrl =
        AndroidReference + "android/service/dreams/DreamService#onWindowFocusChanged(boolean)";
    const string DreamFocusMemberId =
        "M:Android.Service.Dreams.DreamService.OnWindowFocusChanged(System.Boolean)";
    const string IncorrectDreamFocusRemark =
        "This hook is called whenever the window focus changes. See View.onWindowFocusChangedNotLocked(boolean) for more information.";
    const string CorrectDreamFocusRemark =
        "This hook is called whenever the window focus changes. See View.onWindowFocusChanged(boolean) for more information.";
    const string StaleDreamFocusSourceLink =
        "<a href=\"/reference/android/view/View#onWindowFocusChanged(boolean)\">View.onWindowFocusChangedNotLocked(boolean)</a>";
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
    const string LegacyAndroidAttribution =
        "Portions of this page are modifications based on work created and shared by the " +
        "<format type=\"text/html\"><a href=\"https://developers.google.com/terms/site-policies\">" +
        "Android Open Source Project</a></format> and used according to terms described in the " +
        "<format type=\"text/html\"><a href=\"https://creativecommons.org/licenses/by/2.5/\">" +
        "Creative Commons 2.5 Attribution License.</a></format>";
    const string ControlTemplateMemberId =
        "M:Android.Service.Controls.Control.StatefulBuilder.SetControlTemplate(Android.Service.Controls.Templates.ControlTemplate)";
    const string ControlTemplateSourceUrl =
        AndroidReference + "android/service/controls/Control.StatefulBuilder#setControlTemplate(android.service.controls.templates.ControlTemplate)";
    const string ControlTemplateLead =
        "Set the ControlTemplate to define the primary user interaction";
    const string ControlTemplateDescription =
        "Devices may support a variety of user interactions, and all interactions cannot be represented " +
        "with a single ControlTemplate. Therefore, the selected template should be most closely aligned " +
        "with what the expected primary device action will be. Any secondary interactions can be done " +
        "via the setAppIntent(PendingIntent).";
    const string LegacyControlTemplateParagraph =
        ControlTemplateLead + " " + ControlTemplateDescription;
    const string LegacyControlTemplateSummary =
        ControlTemplateLead + " Devices may support a variety of user interactions, and all interactions " +
        "cannot be represented with a single ControlTemplate.";
    static readonly KnownControlsLifecycleRepair[] KnownControlsLifecycleRepairs =
    [
        new(
            "M:Android.Service.Controls.ControlsProviderService.OnBind(Android.Content.Intent)",
            AndroidReference + "android/service/controls/ControlsProviderService#onBind(android.content.Intent)",
            "Return the communication channel to the service.",
            [
                "Return the communication channel to the service. May return null if clients can not bind to the service. The returned IBinder is usually for a complex interface that has been described using aidl.",
                "Note that unlike other application components, calls on to the IBinder interface returned here may not happen on the main thread of the process. More information about the main thread can be found in Processes and Threads.",
            ],
            [
                "Return the communication channel to the service.",
                "The returned IBinder is usually for a complex interface that has been described using aidl.",
            ],
            null),
        new(
            "M:Android.Service.Controls.ControlsProviderService.OnUnbind(Android.Content.Intent)",
            AndroidReference + "android/service/controls/ControlsProviderService#onUnbind(android.content.Intent)",
            "Called when all clients have disconnected from a particular interface published by the service.",
            [
                "Called when all clients have disconnected from a particular interface published by the service. The default implementation does nothing and returns false.",
            ],
            [
                "Called when all clients have disconnected from a particular interface published by the service.",
            ],
            "Return true if you would like to have the service's onRebind(Intent) method later called when new clients bind to it."),
    ];
    static readonly KnownBooleanReturnRepair[] KnownBooleanReturnRepairs =
    [
        new(
            JavaReference + "java.base/java/util/concurrent/ConcurrentHashMap.html#remove(java.lang.Object,java.lang.Object)",
            "the previous value associated with key, or null if there was no mapping for key",
            "true if the value was removed",
            "the previous value associated with <c>key</c>, or <c>null</c> if there was no mapping for <c>key</c>"),
        new(
            JavaReference + "java.base/java/util/concurrent/ConcurrentHashMap.html#replace(K,V,V)",
            "the previous value associated with the specified key, or null if there was no mapping for the key",
            "true if the value was replaced",
            "the previous value associated with the specified key, or <c>null</c> if there was no mapping for the key"),
        new(
            JavaReference + "java.base/java/util/concurrent/ConcurrentSkipListMap.html#remove(java.lang.Object,java.lang.Object)",
            "the previous value associated with the specified key, or null if there was no mapping for the key",
            "true if the value was removed",
            "the previous value associated with the specified key, or <c>null</c> if there was no mapping for the key"),
        new(
            JavaReference + "java.base/java/util/concurrent/ConcurrentSkipListMap.html#replace(K,V,V)",
            "the previous value associated with the specified key, or null if there was no mapping for the key",
            "true if the value was replaced",
            "the previous value associated with the specified key, or <c>null</c> if there was no mapping for the key"),
    ];
    static readonly KnownJavaExampleRepair[] KnownJavaExampleRepairs =
    [
        new(
            JavaReference + "java.base/java/time/temporal/ChronoField.html#adjustInto(R,long)",
            "   // these two lines are equivalent, but the second approach is recommended\n" +
            "   temporal = thisField.adjustInto(temporal);\n" +
            "   temporal = temporal.with(thisField);",
            "   // these two lines are equivalent, but the second approach is recommended\n" +
            "   temporal = thisField.adjustInto(temporal, newValue);\n" +
            "   temporal = temporal.with(thisField, newValue);"),
        new(
            JavaReference + "java.base/java/time/temporal/ChronoUnit.html#addTo(R,long)",
            "   // these two lines are equivalent, but the second approach is recommended\n" +
            "   temporal = thisUnit.addTo(temporal);\n" +
            "   temporal = temporal.plus(thisUnit);",
            "   // these two lines are equivalent, but the second approach is recommended\n" +
            "   temporal = thisUnit.addTo(temporal, amount);\n" +
            "   temporal = temporal.plus(amount, thisUnit);"),
        new(
            JavaReference + "java.base/java/time/zone/ZoneRules.html#getTransition(java.time.LocalDateTime)",
            "  ZoneOffsetTransition trans = rules.getTransition(localDT);\n" +
            "  if (trans != null) {\n" +
            "    // Gap or Overlap: determine what to do from transition\n" +
            "  } else {\n" +
            "    // Normal case: only one valid offset\n" +
            "    zoneOffset = rule.getOffset(localDT);\n" +
            "  }",
            "  ZoneOffsetTransition trans = rules.getTransition(localDT);\n" +
            "  if (trans != null) {\n" +
            "    // Gap or Overlap: determine what to do from transition\n" +
            "  } else {\n" +
            "    // Normal case: only one valid offset\n" +
            "    zoneOffset = rules.getOffset(localDT);\n" +
            "  }",
            "M:Java.Time.Zone.ZoneRules.GetTransition(Java.Time.LocalDateTime)"),
    ];
    static readonly KnownJavaProseRepair[] KnownJavaProseRepairs =
    [
        new(
            JavaReference + "java.base/java/time/temporal/ChronoUnit.html#ERAS",
            "Unit that represents the concept of an era. The ISO calendar system doesn't have eras thus it is impossible to add an era to a date or date-time. The estimated duration of the era is artificially defined as 1,000,000,000 Years. When used with other calendar systems there are no restrictions on the unit.",
            "Unit that represents the concept of an era. The estimated duration of the era is artificially defined as 1,000,000,000 Years. When used with other calendar systems there are no restrictions on the unit."),
    ];
    static readonly KnownAndroidParameterRepair[] KnownAndroidParameterRepairs =
    [
        new(
            AndroidReference + "android/net/vcn/VcnCellUnderlyingNetworkTemplate.Builder#setOperatorPlmnIds(java.util.Set<java.lang.String>)",
            "M:Android.Net.Vcn.VcnCellUnderlyingNetworkTemplate.Builder.SetOperatorPlmnIds(System.Collections.Generic.ICollection{System.String})",
            "operatorPlmnIds",
            "the matching operator PLMN IDs in String. Network with one of the matching PLMN IDs can match this template. If the set is empty, any PLMN ID will match. The default is an empty set. A valid PLMN is a concatenation of MNC and MCC, and thus consists of 5 or 6 decimal digits. This value cannot be null.",
            "the matching operator PLMN IDs in String. Network with one of the matching PLMN IDs can match this template. If the set is empty, any PLMN ID will match. The default is an empty set. A valid PLMN is a concatenation of MCC and MNC, and thus consists of 5 or 6 decimal digits. This value cannot be null."),
    ];
    static readonly KnownAndroidTextRepair[] KnownAndroidTextRepairs =
    [
        new(
            AndroidReference + "android/ranging/ble/cs/BleCsRangingCapabilities#CS_SECURITY_LEVEL_ONE",
            "F:Android.Ranging.Ble.CS.BleCsRangingCapabilitiesCsSecurityLevel.One",
            "summary",
            "Security Level 1: Either CS tone or CS RTT..",
            "Security Level 1: Either CS tone or CS RTT."),
        new(
            AndroidReference + "android/ranging/ble/cs/BleCsRangingParams.Builder#Builder(java.lang.String)",
            "M:Android.Ranging.Ble.CS.BleCsRangingParams.Builder.#ctor(System.String)",
            "param:peerBluetoothAddress",
            "The address of the peer device must be non-null Bluetooth address.",
            "The address of the peer device must be a non-null Bluetooth address."),
        new(
            AndroidReference + "android/ranging/ble/cs/BleCsRangingParams#writeToParcel(android.os.Parcel,%20int)",
            "M:Android.Ranging.Ble.CS.BleCsRangingParams.WriteToParcel(Android.OS.Parcel,Android.OS.ParcelableWriteFlags)",
            "summary",
            "Flatten this object in to a Parcel.",
            "Flatten this object into a Parcel."),
        new(
            AndroidReference + "android/ranging/ble/cs/BleCsRangingParams#writeToParcel(android.os.Parcel,%20int)",
            "M:Android.Ranging.Ble.CS.BleCsRangingParams.WriteToParcel(Android.OS.Parcel,Android.OS.ParcelableWriteFlags)",
            "remarks",
            "Flatten this object in to a Parcel.",
            "Flatten this object into a Parcel."),
    ];
    static readonly KnownAndroidProseRepair[] KnownAndroidProseRepairs =
    [
        new(
            AndroidReference + "android/adservices/measurement/DeletionRequest.Builder#setDeletionMode(int)",
            "M:Android.AdServices.Measurement.DeletionRequest.Builder.SetDeletionMode(Android.AdServices.Measurement.DeletionRequestDeletionMode)",
            "Set the match behavior for the supplied params.",
            "Set the deletion mode for the supplied params.",
            "Set the match behavior for the supplied params. DeletionRequest.DELETION_MODE_ALL: All data associated with the selected records will be deleted. DeletionRequest.DELETION_MODE_EXCLUDE_INTERNAL_DATA: All data except the internal system data (e.g. rate limits) associated with the selected records will be deleted.",
            "Set the deletion mode for the supplied params. DeletionRequest.DELETION_MODE_ALL: All data associated with the selected records will be deleted. DeletionRequest.DELETION_MODE_EXCLUDE_INTERNAL_DATA: All data except the internal system data (e.g. rate limits) associated with the selected records will be deleted."),
    ];
    static readonly KnownEapChannelCorrection[] KnownEapChannelCorrections =
    [
        new(
            "M:Android.Net.Eap.EapAkaInfo.Builder.SetReauthId(System.Byte[])",
            new("setReauthId", "([B)Landroid/net/eap/EapAkaInfo$Builder;", false),
            "Android.Net.Eap.EapAkaInfo+Builder",
            "public Android.Net.Eap.EapAkaInfo.Builder SetReauthId (byte[] reauthId);",
            [("reauthId", "System.Byte[]")],
            new(
                "Sets the re-authentication ID for next use.",
                [new("Sets the re-authentication ID for next use.", false)],
                new(StringComparer.Ordinal)
                {
                    ["reauthId"] = "byte: byte[] representing the client's EAP Identity. This value cannot be null.",
                },
                "Builder this, to facilitate chaining. This value cannot be null.",
                new(StringComparer.Ordinal),
                AndroidReference + "android/net/eap/EapAkaInfo.Builder#setReauthId(byte[])",
                "android.net.eap.EapAkaInfo.Builder.setReauthId",
                "android"),
            "param:reauthId",
            "byte[] representing the client's EAP Identity. This value cannot be null.",
            null),
        new(
            "M:Android.Net.Eap.EapSessionConfig.Builder.SetEapMsChapV2Config(System.String,System.String)",
            new("setEapMsChapV2Config", "(Ljava/lang/String;Ljava/lang/String;)Landroid/net/eap/EapSessionConfig$Builder;", false),
            "Android.Net.Eap.EapSessionConfig+Builder",
            "public Android.Net.Eap.EapSessionConfig.Builder SetEapMsChapV2Config (string username, string password);",
            [("username", "System.String"), ("password", "System.String")],
            new(
                "Sets the configuration for EAP MSCHAPv2.",
                [new("Sets the configuration for EAP MSCHAPv2.", false)],
                new(StringComparer.Ordinal)
                {
                    ["username"] = "String: String the client account's username to be authenticated. This value cannot be null.",
                    ["password"] = "String: String the client account's password to be authenticated. This value cannot be null.",
                },
                "Builder this, to faciliate chaining. This value cannot be null.",
                new(StringComparer.Ordinal),
                AndroidReference + "android/net/eap/EapSessionConfig.Builder#setEapMsChapV2Config(java.lang.String,%20java.lang.String)",
                "android.net.eap.EapSessionConfig.Builder.setEapMsChapV2Config",
                "android"),
            "returns",
            "Builder this, to faciliate chaining. This value cannot be null.",
            "Builder this, to facilitate chaining. This value cannot be null."),
    ];

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
                    file.SelectOwners(options.Member, interfaceMemberResolver, options.ApiSince);
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
                .Where(item => RequiresSourceLoad(item.File, item.Owner))
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
                    var unownedLegacyAttribution = FindUnownedLegacyAttribution(
                        file,
                        owner);
                    if (unownedLegacyAttribution is not null)
                    {
                        ReportSourceReferenceCleanupSkip(
                            report,
                            file,
                            owner,
                            owner.SourceRequest?.Url ?? "",
                            unownedLegacyAttribution);
                        continue;
                    }
                    var mapping = MapOwner(owner, pages);
                    if (ReportMappingFailure(report, file, owner, mapping))
                        continue;

                    var eapCorrection = RepairKnownEapChannel(text, file, owner, mapping.Docs!);
                    var eapCandidates = KnownEapChannelRepairCandidateTargets(owner);
                    if (eapCandidates.Count > 0 && eapCorrection.Targets.Count == 0)
                    {
                        foreach (var target in eapCandidates)
                        {
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath, owner.Id, target, "importer_eap_channel_preserved",
                                "The complete registered member, official source contract, and prior importer-owned Docs could not all be verified; the existing channel was preserved.",
                                mapping.SourceUrl));
                        }
                        continue;
                    }

                    var lifecycleTargets = KnownControlsLifecycleRepairTargets(owner);
                    if (lifecycleTargets.Count > 0)
                    {
                        var lifecycleRepair = RepairKnownControlsLifecycle(
                            text, file, owner, mapping.Docs!);
                        if (lifecycleRepair.Reason is not null || remaining < lifecycleTargets.Count)
                        {
                            foreach (var target in lifecycleTargets)
                            {
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath, owner.Id, target,
                                    lifecycleRepair.Reason ?? "max_changes_reached",
                                    lifecycleRepair.Detail ??
                                        $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl));
                            }
                        }
                        else
                        {
                            text = lifecycleRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining -= lifecycleTargets.Count;
                            foreach (var target in lifecycleTargets)
                            {
                                report.Entries.Add(ReportEntry.Changed(
                                    "would_apply", file.RelativePath, owner.Id, target,
                                    mapping.SourceUrl,
                                    "importer_known_unsafe_controls_lifecycle_repair",
                                    "Withdrew exact inherited Service contracts contradicted by the final ControlsProviderService implementations; retained only verified reference prose."));
                            }
                        }
                    }

                    if (HasKnownAndroidParagraphBoundaryRepairCandidate(owner))
                    {
                        var boundaryRepair = RepairKnownAndroidParagraphBoundary(
                            text,
                            file,
                            owner,
                            mapping.Docs!);
                        if (boundaryRepair.Reason is not null || remaining < 2)
                        {
                            foreach (var target in new[] { "summary", "remarks" })
                            {
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    target,
                                    boundaryRepair.Reason ?? "max_changes_reached",
                                    boundaryRepair.Detail ??
                                        $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl));
                            }
                        }
                        else if (!boundaryRepair.Text.Equals(text, StringComparison.Ordinal))
                        {
                            text = boundaryRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining -= 2;
                            foreach (var target in new[] { "summary", "remarks" })
                            {
                                report.Entries.Add(ReportEntry.Changed(
                                    "would_apply",
                                    file.RelativePath,
                                    owner.Id,
                                    target,
                                    mapping.SourceUrl,
                                    "importer_known_android_paragraph_boundary_repair",
                                    "Restored the exact official source paragraph boundary in an importer-owned summary and remarks."));
                            }
                        }
                    }

                    var copiedDescriptionRepair = RepairCopiedDescriptionLabels(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    ReportCopiedDescriptionRepairSkips(
                        report,
                        file,
                        owner,
                        mapping.SourceUrl,
                        copiedDescriptionRepair.Skips);
                    if (copiedDescriptionRepair.Targets.Count > 0)
                    {
                        if (remaining < copiedDescriptionRepair.Targets.Count)
                        {
                            foreach (var target in copiedDescriptionRepair.Targets)
                            {
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    target,
                                    "max_changes_reached",
                                    $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl));
                            }
                        }
                        else
                        {
                            text = copiedDescriptionRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining -= copiedDescriptionRepair.Targets.Count;
                            foreach (var target in copiedDescriptionRepair.Targets)
                            {
                                report.Entries.Add(ReportEntry.Changed(
                                    "would_apply",
                                    file.RelativePath,
                                    owner.Id,
                                    target,
                                    mapping.SourceUrl,
                                    "importer_copied_description_repair",
                                    "Replaced an exact importer-generated Javadoc copied-description label."));
                            }
                        }
                    }

                    var booleanReturnRepair = RepairKnownBooleanReturn(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    if (booleanReturnRepair.Skip is not null)
                    {
                        ReportBooleanReturnRepairSkip(
                            report,
                            file,
                            owner,
                            mapping.SourceUrl,
                            booleanReturnRepair.Skip);
                    }
                    else if (booleanReturnRepair.Repaired)
                    {
                        if (remaining == 0)
                        {
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                "returns",
                                "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                        }
                        else
                        {
                            text = booleanReturnRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining--;
                            report.Entries.Add(ReportEntry.Changed(
                                "would_apply",
                                file.RelativePath,
                                owner.Id,
                                "returns",
                                mapping.SourceUrl,
                                "importer_known_boolean_return_repair",
                                "Replaced an exact importer-generated Boolean return description with the exact official Java return contract."));
                        }
                    }

                    var javaExampleRepair = RepairKnownJavaExample(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    if (javaExampleRepair.Repaired)
                    {
                        if (remaining == 0)
                        {
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                "remarks",
                                "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                        }
                        else
                        {
                            text = javaExampleRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining--;
                            report.Entries.Add(ReportEntry.Changed(
                                "would_apply",
                                file.RelativePath,
                                owner.Id,
                                "remarks",
                                mapping.SourceUrl,
                                "importer_known_java_example_repair",
                                "Corrected an exact importer-generated Java example using its documented parameter names."));
                        }
                    }

                    var javaProseRepair = RepairKnownJavaProse(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    if (javaProseRepair.Repaired)
                    {
                        if (remaining == 0)
                        {
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                "remarks",
                                "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                        }
                        else
                        {
                            text = javaProseRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining--;
                            report.Entries.Add(ReportEntry.Changed(
                                "would_apply",
                                file.RelativePath,
                                owner.Id,
                                "remarks",
                                mapping.SourceUrl,
                                "importer_known_java_prose_repair",
                                "Removed an exact importer-generated Java statement contradicted by Android API documentation."));
                        }
                    }

                    var unsafeParameterRepair = RepairKnownUnsafeParameter(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    if (unsafeParameterRepair.Repaired)
                    {
                        if (remaining == 0)
                        {
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                $"param:{unsafeParameterRepair.ParameterName}",
                                "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                        }
                        else
                        {
                            text = unsafeParameterRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining--;
                            report.Entries.Add(ReportEntry.Changed(
                                "would_apply",
                                file.RelativePath,
                                owner.Id,
                                $"param:{unsafeParameterRepair.ParameterName}",
                                mapping.SourceUrl,
                                "importer_known_unsafe_parameter_repair",
                                "Restored an exact importer-generated parameter to its placeholder because its official source channel is unsafe."));
                        }
                    }

                    var androidParameterRepair = RepairKnownAndroidParameter(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    if (androidParameterRepair.Repaired)
                    {
                        if (remaining == 0)
                        {
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                $"param:{androidParameterRepair.ParameterName}",
                                "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                        }
                        else
                        {
                            text = androidParameterRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining--;
                            report.Entries.Add(ReportEntry.Changed(
                                "would_apply",
                                file.RelativePath,
                                owner.Id,
                                $"param:{androidParameterRepair.ParameterName}",
                                mapping.SourceUrl,
                                "importer_known_android_parameter_repair",
                                "Corrected an exact importer-generated Android parameter description using Android API documentation."));
                        }
                    }

                    var androidTextRepair = RepairKnownAndroidText(text, file, owner, mapping.Docs!);
                    if (androidTextRepair.Targets.Count > 0)
                    {
                        var exceedsLimit = remaining < androidTextRepair.Targets.Count;
                        if (!exceedsLimit)
                        {
                            text = androidTextRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining -= androidTextRepair.Targets.Count;
                        }
                        foreach (var target in androidTextRepair.Targets)
                        {
                            report.Entries.Add(exceedsLimit
                                ? ReportEntry.Skipped(
                                    file.RelativePath, owner.Id, target, "max_changes_reached",
                                    $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl)
                                : ReportEntry.Changed(
                                    "would_apply", file.RelativePath, owner.Id, target, mapping.SourceUrl,
                                    "importer_known_android_text_repair",
                                    "Corrected an allow-listed official-source typo in exact importer-owned text."));
                        }
                    }

                    var androidProseRepair = RepairKnownAndroidProse(
                        text,
                        file,
                        owner,
                        mapping.Docs!);
                    if (androidProseRepair.Repaired)
                    {
                        if (remaining < 2)
                        {
                            RestoreOffsetsAfterSkippedRepair(file, owner, text);
                            foreach (var target in new[] { "summary", "remarks" })
                            {
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    target,
                                    "max_changes_reached",
                                    $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl));
                            }
                        }
                        else
                        {
                            text = androidProseRepair.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining -= 2;
                            foreach (var target in new[] { "summary", "remarks" })
                            {
                                report.Entries.Add(ReportEntry.Changed(
                                    "would_apply",
                                    file.RelativePath,
                                    owner.Id,
                                    target,
                                    mapping.SourceUrl,
                                    "importer_known_android_prose_repair",
                                    "Corrected an exact importer-generated Android source typo that confuses deletion mode with match behavior."));
                            }
                        }
                    }

                    if (eapCorrection.Targets.Count > 0)
                    {
                        var target = eapCorrection.Targets[0];
                        if (remaining == 0)
                        {
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath, owner.Id, target, "max_changes_reached",
                                $"The --max-changes limit of {options.MaxChanges} was reached.",
                                mapping.SourceUrl));
                        }
                        else
                        {
                            text = eapCorrection.Text;
                            file.UpdateBlockOffsets(owner.Order, text);
                            fileChanged = true;
                            ownerChanged = true;
                            remaining--;
                            report.Entries.Add(ReportEntry.Changed(
                                "would_apply", file.RelativePath, owner.Id, target, mapping.SourceUrl,
                                target == "param:reauthId"
                                    ? "importer_eap_unsafe_parameter_withdrawal"
                                    : "importer_eap_return_typo_repair",
                                target == "param:reauthId"
                                    ? "Withdrew the exact importer-owned parameter because the official source confuses a re-authentication ID with the client's EAP identity."
                                    : "Corrected the exact importer-owned EAP builder return typo."));
                        }
                    }
                    foreach (var placeholder in owner.Placeholders.OrderBy(item => item.Order))
                    {
                        var replacement = ReplacementFor(
                            placeholder,
                            mapping.Docs!,
                            owner.IsEnumField);
                        if (IsKnownUnsafeContinueStrokeDuration(
                                mapping.SourceUrl,
                                placeholder.Target))
                        {
                            replacement = Replacement.Skip(
                                "source_channel_ambiguous",
                                "The exact Android ContinueStroke source permits zero duration even though the constructed StrokeDescription requires a positive duration.");
                        }
                        replacement = LimitOverlappingRemarksReplacement(
                            placeholder,
                            mapping.Docs!,
                            replacement,
                            owner.Docs.Element("remarks"));
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
                            replacement,
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
                    var summarySourceTextRepair = mapping.Docs is not null &&
                        HasImporterOwnedSummarySourceTextRefresh(owner, mapping.Docs);
                    var enumDiscardedMetadataRepair = mapping.Docs is not null &&
                        HasEnumDiscardedMetadataCandidate(file, owner);
                    var augmentedRemarksRepair = HasAugmentedRemarksPlaceholder(file, owner);
                    var truncatedSummaryRepair = HasTruncatedImporterSummary(file, owner);
                    var codeExampleRepair = HasIncompleteCodeExampleRemarks(file, owner);
                    var metadataOnlyRemarksRepair = HasMetadataOnlyRemarks(file, owner);
                    var channelOnlyMetadataRepair =
                        mapping.Docs!.UnsafeTargets?.ContainsKey("remarks") != true &&
                        HasChannelOnlySourceMetadata(
                            file,
                            owner,
                            mapping.Docs);
                    if (!ownerChanged &&
                        mapping.Docs is not null &&
                        (enumSummaryRepair ||
                         enumListRepair ||
                        summarySourceTextRepair ||
                         enumDiscardedMetadataRepair ||
                         augmentedRemarksRepair ||
                         truncatedSummaryRepair ||
                         codeExampleRepair ||
                         metadataOnlyRemarksRepair ||
                         channelOnlyMetadataRepair))
                    {
                        var refreshed = summarySourceTextRepair
                            ? RefreshImporterOwnedSummarySourceText(text, file, owner, mapping.Docs)
                            : truncatedSummaryRepair
                                ? ReplaceTruncatedSummary(text, file, owner, mapping.Docs)
                                : text;
                        if (!refreshed.Equals(text, StringComparison.Ordinal))
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                        if (codeExampleRepair)
                        {
                            refreshed = ReplaceIncompleteCodeExampleRemarks(refreshed, file, owner, mapping.Docs);
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                        }
                        SourceReferenceCleanupSkip? sourceReferenceCleanupSkip = null;
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
                                out sourceReferenceCleanupSkip,
                                allowEnumCreation: false,
                                addMetadataForChannelOnlyMember: channelOnlyMetadataRepair);
                            file.UpdateBlockOffsets(owner.Order, refreshed);
                        }
                        if (sourceReferenceCleanupSkip is not null)
                        {
                            ReportSourceReferenceCleanupSkip(
                                report,
                                file,
                                owner,
                                mapping.SourceUrl,
                                sourceReferenceCleanupSkip);
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

                    var canRepairExistingDocumentation = !ownerChanged;
                    if (canRepairExistingDocumentation &&
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

                    if (canRepairExistingDocumentation &&
                        mapping.Docs is not null &&
                        HasPotentialImporterOwnedRemarksRefresh(file, owner))
                    {
                        var remarksRefresh = RefreshImporterOwnedRemarks(
                            text,
                            file,
                            owner,
                            mapping.Docs);
                        if (remarksRefresh.Reason is not null)
                        {
                            report.Entries.Add(ReportEntry.Skipped(
                                file.RelativePath,
                                owner.Id,
                                "remarks",
                                remarksRefresh.Reason,
                                remarksRefresh.Detail!,
                                mapping.SourceUrl));
                        }
                        else if (!remarksRefresh.Text.Equals(text, StringComparison.Ordinal))
                        {
                            if (remaining == 0)
                            {
                                report.Entries.Add(ReportEntry.Skipped(
                                    file.RelativePath,
                                    owner.Id,
                                    "remarks",
                                    "max_changes_reached",
                                    $"The --max-changes limit of {options.MaxChanges} was reached.",
                                    mapping.SourceUrl));
                            }
                            else
                            {
                                text = remarksRefresh.Text;
                                file.UpdateBlockOffsets(owner.Order, text);
                                fileChanged = true;
                                ownerChanged = true;
                                remaining--;
                                report.Entries.Add(ReportEntry.Changed(
                                    "would_apply",
                                    file.RelativePath,
                                    owner.Id,
                                    "remarks",
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
                        var sourceDocumentation = AddSourceDocumentationIfSafe(
                            text,
                            file,
                            owner,
                            mapping.Docs,
                            out var sourceReferenceCleanupSkip,
                            addMetadataForChannelOnlyMember: importedSourceChannel);
                        if (sourceReferenceCleanupSkip is not null)
                        {
                            ReportSourceReferenceCleanupSkip(
                                report,
                                file,
                                owner,
                                mapping.SourceUrl,
                                sourceReferenceCleanupSkip);
                        }
                        text = sourceDocumentation;
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
            return MappingResult.Success(WithSemanticSummaryIfNecessary(
                WithKnownAndroidTextCorrections(owner.Id, fieldDocs)));
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
        docs = WithKnownEapChannelCorrections(owner, docs);
        docs = WithoutKnownUnsafeAndroidSourceChannels(owner.Id, docs);
        docs = WithoutKnownUnsafeJavaSourceChannels(owner.Id, docs);
        docs = WithoutKnownUnsafeHardwareBufferCreateRemark(owner.Id, docs);
        docs = WithoutKnownUnsafeRemoteEntryGuidance(owner.Id, docs);
        docs = WithKnownAndroidTextCorrections(owner.Id, docs);
        docs = WithoutKnownUnsafeControlsLifecycleChannels(owner.Id, docs);
        return MappingResult.Success(WithSemanticSummaryIfNecessary(docs));
    }

    sealed record KnownControlsLifecycleRepair(
        string MemberId,
        string SourceUrl,
        string Summary,
        string[] OriginalParagraphs,
        string[] FirstParagraphReplacement,
        string? IncorrectReturn)
    {
        public IEnumerable<string> SafeParagraphs =>
            FirstParagraphReplacement.Concat(OriginalParagraphs.Skip(1));
    }

    static SourceDocs WithoutKnownUnsafeControlsLifecycleChannels(string ownerId, SourceDocs docs)
    {
        var repair = KnownControlsLifecycleRepairs.SingleOrDefault(candidate =>
            candidate.MemberId == ownerId &&
            candidate.SourceUrl == docs.SourceUrl &&
            docs.SourceKind == "android");
        if (repair is null)
            return docs;

        var paragraphs = docs.Paragraphs.SelectMany(paragraph =>
            !paragraph.IsCode && paragraph.Text == repair.OriginalParagraphs[0]
                ? repair.FirstParagraphReplacement
                    .Select(text => new SourceParagraph(text, false))
                : [paragraph]).ToList();
        if (repair.IncorrectReturn is not null &&
            RemoveLeadingJavaType(docs.Returns) == repair.IncorrectReturn)
        {
            var targets = docs.UnsafeTargets is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(docs.UnsafeTargets, StringComparer.Ordinal);
            targets["returns"] =
                "The inherited Service return describes a caller choice, but ControlsProviderService.onUnbind is final and always returns true.";
            return docs with { Paragraphs = paragraphs, UnsafeTargets = targets };
        }
        return docs with { Paragraphs = paragraphs };
    }

    static SourceDocs WithKnownAndroidTextCorrections(string memberId, SourceDocs docs)
    {
        foreach (var repair in KnownAndroidTextRepairs.Where(repair =>
            repair.MemberId == memberId && repair.SourceUrl == docs.SourceUrl &&
            docs.SourceKind == "android"))
        {
            if (repair.Target.StartsWith("param:", StringComparison.Ordinal))
            {
                var name = repair.Target["param:".Length..];
                if (docs.Parameters.TryGetValue(name, out var parameter) &&
                    RemoveLeadingJavaType(parameter) == repair.IncorrectText)
                {
                    var parameters = new Dictionary<string, string>(docs.Parameters, StringComparer.Ordinal)
                    {
                        [name] = repair.CorrectText,
                    };
                    docs = docs with { Parameters = parameters };
                }
            }
            else
            {
                docs = docs with
                {
                    Summary = docs.Summary == repair.IncorrectText ? repair.CorrectText : docs.Summary,
                    Paragraphs = docs.Paragraphs.Select(paragraph =>
                        !paragraph.IsCode && paragraph.Text == repair.IncorrectText
                            ? paragraph with { Text = repair.CorrectText }
                            : paragraph).ToList(),
                };
            }
        }
        return docs;
    }

    static bool MatchesKnownEapMember(DocsOwner owner, KnownEapChannelCorrection correction) =>
        owner.Id == correction.MemberId &&
        owner.MemberRegistration == correction.Registration &&
        owner.SourceRequest?.Kind == "android" &&
        owner.SourceRequest.Url == correction.Source.SourceUrl.Split('#')[0] &&
        owner.Member?.Element("MemberType")?.Value == "Method" &&
        owner.Member.Elements("MemberSignature").Where(signature =>
                (string?)signature.Attribute("Language") == "C#")
            .Select(signature => (string?)signature.Attribute("Value"))
            .SequenceEqual([correction.ManagedSignature]) &&
        owner.Member.Element("ReturnValue")?.Element("ReturnType")?.Value == correction.ReturnType &&
        owner.Member.Element("Parameters")?.Elements("Parameter")
            .Select(parameter => ((string?)parameter.Attribute("Name") ?? "",
                (string?)parameter.Attribute("Type") ?? ""))
            .SequenceEqual(correction.ParameterTypes) == true;

    static bool MatchesKnownEapSource(SourceDocs actual, SourceDocs expected) =>
        actual.SourceKind == expected.SourceKind &&
        actual.SourceUrl == expected.SourceUrl &&
        actual.SourceLabel == expected.SourceLabel &&
        actual.Summary == expected.Summary &&
        actual.Paragraphs.SequenceEqual(expected.Paragraphs) &&
        actual.Returns == expected.Returns &&
        actual.Parameters.Count == expected.Parameters.Count &&
        actual.Parameters.All(pair => expected.Parameters.TryGetValue(pair.Key, out var value) && pair.Value == value) &&
        actual.Exceptions.Count == 0 &&
        actual.UnsafeTargets is null &&
        !actual.HasMalformedSourceMarkup;

    static SourceDocs WithKnownEapChannelCorrections(DocsOwner owner, SourceDocs docs)
    {
        var correction = KnownEapChannelCorrections.SingleOrDefault(candidate =>
            MatchesKnownEapMember(owner, candidate) && MatchesKnownEapSource(docs, candidate.Source));
        if (correction is null)
            return docs;
        return correction.CorrectText is null
            ? docs with
            {
                EapCorrection = correction,
                UnsafeTargets = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [correction.Target] =
                        "The exact official source misidentifies the re-authentication ID as the client's EAP identity; no safe same-channel prose was available.",
                },
            }
            : docs with { Returns = correction.CorrectText, EapCorrection = correction };
    }

    static List<string> KnownEapChannelRepairCandidateTargets(DocsOwner owner) =>
        KnownEapChannelCorrections.Where(correction => correction.MemberId == owner.Id)
            .Where(correction => owner.Docs.Elements(correction.Target.Split(':')[0]).Any(channel =>
                (correction.Target == "returns" || (string?)channel.Attribute("name") == "reauthId") &&
                channel.Value == correction.IncorrectText))
            .Select(correction => correction.Target).ToList();

    static XElement KnownEapPriorDocs(KnownEapChannelCorrection correction)
    {
        var source = correction.Source;
        return new XElement("Docs",
            correction.ParameterTypes.Select(parameter => new XElement("param",
                new XAttribute("name", parameter.Name),
                RemoveLeadingJavaType(source.Parameters[parameter.Name]))),
            new XElement("summary", source.Summary),
            new XElement("returns", source.Returns),
            new XElement("remarks",
                source.Paragraphs.Select(DocumentationElement),
                ImporterSourceReference(source),
                XElement.Parse($"<para>{AndroidAttribution}</para>")));
    }

    static AndroidTextRepairResult RepairKnownEapChannel(
        string text, LoadedFile file, DocsOwner owner, SourceDocs source)
    {
        if (source.EapCorrection is not { } correction ||
            !MatchesKnownEapMember(owner, correction))
            return new(text, []);
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        var expectedDocs = KnownEapPriorDocs(correction);
        if (!TryParseDocsBlock(blockText, out var actual) ||
            !XNode.DeepEquals(actual, owner.Docs) ||
            !ImporterMarkupEquals(actual, expectedDocs) ||
            actual.Elements().Where(channel => channel.Name != "remarks")
                .Any(channel => !HasPlainTextContent(channel, out var value) ||
                    value != expectedDocs.Elements(channel.Name)
                        .Single(expected => (string?)expected.Attribute("name") == (string?)channel.Attribute("name")).Value) ||
            actual.Element("remarks")!.Elements("para").First().Value != correction.Source.Paragraphs[0].Text)
            return new(text, []);
        var target = actual.Elements(correction.Target.Split(':')[0]).Single(channel =>
            correction.Target == "returns" || (string?)channel.Attribute("name") == "reauthId");
        if (!TryGetElementSpan(blockText, target, out var span))
            return new(text, []);
        var replacement = new XElement(target.Name, target.Attributes(), correction.CorrectText ?? "To be added.")
            .ToString(SaveOptions.DisableFormatting);
        return new(
            text[..block.Start] + blockText[..span.Start] + replacement +
            blockText[span.End..] + text[block.End..],
            [correction.Target]);
    }

    static SourceDocs WithoutKnownUnsafeAndroidSourceChannels(string ownerId, SourceDocs docs)
    {
        var sourceText = string.Join(
            "\n",
            new[] { docs.Summary, docs.Returns }
                .Concat(docs.Paragraphs.Select(paragraph => paragraph.Text))
                .Concat(docs.Parameters.Values));
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ownerId == "P:Android.Security.KeyStoreException.RetryPolicy" &&
            docs.SourceUrl.Equals(
                AndroidReference + "android/security/KeyStoreException#getRetryPolicy()",
                StringComparison.Ordinal) &&
            docs.Returns.Equals(
                "Value is either 0 or a combination of the following: RETRY_NEVER; RETRY_WITH_EXPONENTIAL_BACKOFF; RETRY_WHEN_CONNECTIVITY_AVAILABLE; RETRY_AFTER_NEXT_REBOOT",
                StringComparison.Ordinal))
        {
            targets["value"] =
                "The exact Android return text describes mutually exclusive retry-policy codes as combinable flags.";
        }

        if (ownerId == "M:Android.Net.Wifi.Aware.PublishConfig.Builder.SetPublishType(Android.Net.Wifi.Aware.PublishType)" &&
            sourceText.Contains(
                "solicited (aka active - publish packets are transmitted over-the-air)",
                StringComparison.Ordinal) &&
            sourceText.Contains(
                "unsolicited (aka passive - no publish packets are transmitted",
                StringComparison.Ordinal))
        {
            const string detail =
                "The exact Android source reverses the documented solicited and unsolicited publish semantics.";
            targets["summary"] = detail;
            targets["remarks"] = detail;
        }

        if (ownerId == "M:Android.Net.Wifi.Aware.SubscribeConfig.Builder.SetSubscribeType(Android.Net.Wifi.Aware.SubscribeType)" &&
            sourceText.Contains(
                "passive (no subscribe packets are transmitted, a match is made against a solicited/active publish session",
                StringComparison.Ordinal))
        {
            const string detail =
                "The exact Android source pairs passive subscribers with the wrong publish-session type.";
            targets["summary"] = detail;
            targets["remarks"] = detail;
        }

        if ((ownerId is
                "M:Android.Net.Wifi.Aware.SubscribeConfig.Builder.SetMaxDistanceMm(System.Int32)" or
                "M:Android.Net.Wifi.Aware.SubscribeConfig.Builder.SetMinDistanceMm(System.Int32)") &&
            sourceText.Contains("min <= distance <= max", StringComparison.Ordinal) &&
            sourceText.Contains("distance <= max or distance >= min", StringComparison.Ordinal))
        {
            targets["remarks"] =
                "The exact Android source combines mutually exclusive legacy and ingress/egress geofence rules.";
        }

        if (ownerId == "M:Android.Net.Wifi.Aware.WifiAwareNetworkSpecifier.Builder.SetPort(System.Int32)" &&
            docs.Parameters.TryGetValue("port", out var port) &&
            port.Contains("between 0 and 65535 inclusive", StringComparison.Ordinal))
        {
            targets["param:port"] =
                "The exact Android source allows port 0 even though the corresponding setter rejects non-positive ports.";
        }

        if (IsKnownUnsafeContinueStrokeDuration(docs.SourceUrl) &&
            sourceText.Contains(
                "duration for the new stroke",
                StringComparison.OrdinalIgnoreCase) &&
            sourceText.Contains(
                "must not be negative",
                StringComparison.OrdinalIgnoreCase))
        {
            targets["param:duration"] =
                "The exact Android source permits zero duration even though ContinueStroke constructs a StrokeDescription that requires a positive duration.";
        }

        if (ownerId == "M:Android.AdServices.AdSelection.PersistAdSelectionResultRequest.Builder.SetAdSelectionResult(System.Byte[])" &&
            UrlsEqual(
                docs.SourceUrl,
                "https://developer.android.com/reference/android/adservices/adselection/PersistAdSelectionResultRequest.Builder#setAdSelectionResult(byte[])") &&
            docs.Summary.Equals(
                "Sets the ad selection result String.",
                StringComparison.Ordinal) &&
            docs.Paragraphs.Select(paragraph => paragraph.Text).SequenceEqual(
                ["Sets the ad selection result String."],
                StringComparer.Ordinal))
        {
            const string detail =
                "The exact Android source describes the byte-array input as a String.";
            targets["summary"] = detail;
            targets["remarks"] = detail;
        }

        if (ownerId == "M:Android.AdServices.AdSelection.ReportEventRequest.Builder.SetReportingDestinations(System.Int32)" &&
            UrlsEqual(
                docs.SourceUrl,
                "https://developer.android.com/reference/android/adservices/adselection/ReportEventRequest.Builder#setReportingDestinations(int)") &&
            docs.Summary.Equals(
                "Sets the bitfield of reporting destinations to report to (buyer, seller, or both).",
                StringComparison.Ordinal) &&
            docs.Paragraphs.Select(paragraph => paragraph.Text).SequenceEqual(
                [
                    "Sets the bitfield of reporting destinations to report to (buyer, seller, or both).",
                    "See ReportEventRequest.getReportingDestinations() for more information.",
                ],
                StringComparer.Ordinal))
        {
            const string detail =
                "The exact Android source omits the valid component-seller reporting destination.";
            targets["summary"] = detail;
            targets["remarks"] = detail;
        }

        return targets.Count == 0
            ? docs
            : docs with
            {
                Paragraphs = targets.ContainsKey("remarks") ? [] : docs.Paragraphs,
                UnsafeTargets = targets,
            };
    }

    const string KnownUnsafeZoneTransitionMemberId =
        "M:Java.Time.Zone.ZoneOffsetTransitionRule.Of(Java.Time.Month,System.Int32,Java.Time.DayOfWeek,Java.Time.LocalTime,System.Boolean,Java.Time.Zone.ZoneOffsetTransitionRule.TimeDefinition,Java.Time.ZoneOffset,Java.Time.ZoneOffset,Java.Time.ZoneOffset)";
    const string KnownUnsafeZoneTransitionSourceUrl =
        JavaReference + "java.base/java/time/zone/ZoneOffsetTransitionRule.html#of(java.time.Month,int,java.time.DayOfWeek,java.time.LocalTime,boolean,java.time.zone.ZoneOffsetTransitionRule.TimeDefinition,java.time.ZoneOffset,java.time.ZoneOffset,java.time.ZoneOffset)";
    const string KnownUnsafeZoneTransitionTime =
        "the cutover time in the 'before' offset, not null";

    static bool IsKnownUnsafeZoneTransitionTime(string memberId, SourceDocs docs) =>
        memberId == KnownUnsafeZoneTransitionMemberId &&
        docs.SourceKind == "java" &&
        docs.SourceUrl.Equals(KnownUnsafeZoneTransitionSourceUrl, StringComparison.Ordinal) &&
        docs.Parameters.TryGetValue("time", out var time) &&
        time.Equals(KnownUnsafeZoneTransitionTime, StringComparison.Ordinal);

    static SourceDocs WithoutKnownUnsafeJavaSourceChannels(string memberId, SourceDocs docs)
    {
        if (!IsKnownUnsafeZoneTransitionTime(memberId, docs))
            return docs;

        var targets = docs.UnsafeTargets is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(docs.UnsafeTargets, StringComparer.Ordinal);
        targets["param:time"] =
            "The exact Java 21 source describes the cutover time as relative to the before offset, " +
            "but that interpretation applies only to WALL; UTC and STANDARD use their respective time bases.";
        return docs with { UnsafeTargets = targets };
    }

    static bool HasKnownUnsafeZoneTransitionTimeCandidate(DocsOwner owner) =>
        owner.Id == KnownUnsafeZoneTransitionMemberId &&
        owner.Docs.Elements("param").Any(parameter =>
            (string?)parameter.Attribute("name") == "time" &&
            parameter.Value.Equals(KnownUnsafeZoneTransitionTime, StringComparison.Ordinal));

    const string KnownUnsafeContinueStrokeSourceUrl =
        "https://developer.android.com/reference/android/accessibilityservice/GestureDescription.StrokeDescription#continueStroke(android.graphics.Path,%20long,%20long,%20boolean)";

    static bool IsKnownUnsafeContinueStrokeDuration(string sourceUrl) =>
        UrlsEqual(sourceUrl, KnownUnsafeContinueStrokeSourceUrl) ||
        sourceUrl.Contains(
            "GestureDescription.StrokeDescription#continueStroke",
            StringComparison.Ordinal);

    static bool IsKnownUnsafeContinueStrokeDuration(
        string sourceUrl,
        string target) =>
        target == "param:duration" &&
        IsKnownUnsafeContinueStrokeDuration(sourceUrl);

    static bool IsUnsafeContinueStrokeDuration(SourceDocs docs) =>
        IsKnownUnsafeContinueStrokeDuration(docs.SourceUrl) &&
        docs.Parameters.TryGetValue("duration", out var duration) &&
        duration.Contains("duration for the new stroke", StringComparison.OrdinalIgnoreCase) &&
        duration.Contains("must not be negative", StringComparison.OrdinalIgnoreCase);

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

    static SourceDocs WithoutKnownUnsafeHardwareBufferCreateRemark(
        string ownerId,
        SourceDocs docs)
    {
        const string owner =
            "M:Android.Hardware.HardwareBuffer.Create(System.Int32,System.Int32,Android.Hardware.HardwareBufferFormat,System.Int32,Android.Hardware.HardwareBufferUsage)";
        const string sourceUrl =
            "https://developer.android.com/reference/android/hardware/HardwareBuffer#create(int,%20int,%20int,%20int,%20long)";
        const string unsafeRemark =
            "Calling this method will throw an IllegalStateException if format is not a supported Format type.";

        if (!ownerId.Equals(owner, StringComparison.Ordinal) ||
            !docs.SourceUrl.Equals(sourceUrl, StringComparison.Ordinal))
        {
            return docs;
        }

        return docs with
        {
            Paragraphs = docs.Paragraphs
                .Where(paragraph => paragraph.IsCode ||
                    !NormalizeText(paragraph.Text).Equals(
                        unsafeRemark,
                        StringComparison.Ordinal))
                .ToList(),
        };
    }

    static SourceDocs WithoutKnownUnsafeRemoteEntryGuidance(
        string ownerId,
        SourceDocs docs)
    {
        const string remoteGetOwner =
            "M:Android.Service.Credentials.BeginGetCredentialResponse.Builder.SetRemoteCredentialEntry(Android.Service.Credentials.RemoteEntry)";
        const string remoteGetUrl =
            "https://developer.android.com/reference/android/service/credentials/BeginGetCredentialResponse.Builder#setRemoteCredentialEntry(android.service.credentials.RemoteEntry)";
        const string remoteGetEntryGuidance =
            "When constructing the CredentialEntry object, the pendingIntent must be set such that it leads to an activity that can provide UI to fulfill the request on a remote device. When user selects this remoteCredentialEntry, the system will invoke the pendingIntent set on the CredentialEntry.";
        const string remoteGetResponseGuidance =
            "Once the remote credential flow is complete, the Activity result should be set to Activity.RESULT_OK and an extra with the CredentialProviderService.EXTRA_GET_CREDENTIAL_RESPONSE key should be populated with a Credential object.";
        const string remoteCreateOwner =
            "M:Android.Service.Credentials.BeginCreateCredentialResponse.Builder.SetRemoteCreateEntry(Android.Service.Credentials.RemoteEntry)";
        const string remoteCreateUrl =
            "https://developer.android.com/reference/android/service/credentials/BeginCreateCredentialResponse.Builder#setRemoteCreateEntry(android.service.credentials.RemoteEntry)";
        const string remoteCreateEntryGuidance =
            "When constructing the CreateEntry object, the pendingIntent must be set such that it leads to an activity that can provide UI to fulfill the request on a remote device. When user selects this remoteCreateEntry, the system will invoke the pendingIntent set on the CreateEntry.";

        string[]? unsafeRemarks = ownerId switch
        {
            remoteGetOwner when docs.SourceUrl.Equals(remoteGetUrl, StringComparison.Ordinal) =>
            [
                remoteGetEntryGuidance,
                remoteGetResponseGuidance,
            ],
            remoteCreateOwner when docs.SourceUrl.Equals(remoteCreateUrl, StringComparison.Ordinal) =>
            [
                remoteCreateEntryGuidance,
            ],
            _ => null,
        };
        if (unsafeRemarks is null)
            return docs;

        var normalizedUnsafeRemarks = unsafeRemarks
            .Select(NormalizeText)
            .ToHashSet(StringComparer.Ordinal);
        return docs with
        {
            Paragraphs = docs.Paragraphs
                .Where(paragraph => paragraph.IsCode ||
                    !normalizedUnsafeRemarks.Contains(NormalizeText(paragraph.Text)))
                .ToList(),
        };
    }

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
        targets.UnionWith(KnownAndroidTextRepairCandidateTargets(owner));
        targets.UnionWith(KnownEapChannelRepairCandidateTargets(owner));
        targets.UnionWith(KnownControlsLifecycleRepairTargets(owner));
        if (IsEnumSummaryRepairCandidate(owner) ||
            HasTruncatedImporterSummary(file, owner) ||
            KnownControlsLifecycleRepairTargets(owner).Count > 0 ||
            HasKnownAndroidParagraphBoundaryRepairCandidate(owner) ||
            HasCopiedDescriptionSummaryRepairCandidate(file, owner) ||
            HasPotentialEnumListRepair(file, owner))
            targets.Add("summary");
        if (HasDeprecatedValueRepairCandidate(file, owner))
            targets.Add("value");
        if (HasKnownIncorrectBooleanReturnRepairCandidate(file, owner))
            targets.Add("returns");
        if (HasKnownAndroidProseRepairCandidate(file, owner))
        {
            targets.Add("summary");
            targets.Add("remarks");
        }
        if (HasKnownUnsafeZoneTransitionTimeCandidate(owner))
            targets.Add("param:time");
        if (HasAugmentedRemarksPlaceholder(file, owner) ||
            HasKnownAndroidParagraphBoundaryRepairCandidate(owner) ||
            HasPotentialImporterOwnedRemarksRefresh(file, owner) ||
            HasIncompleteCodeExampleRemarks(file, owner) ||
            HasMetadataOnlyRemarks(file, owner) ||
            HasCopiedDescriptionRemarksRepairCandidate(file, owner) ||
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

    static bool RequiresSourceLoad(LoadedFile file, DocsOwner owner) =>
        owner.Placeholders.Count > 0 ||
        IsEnumSummaryRepairCandidate(owner) ||
        HasPotentialEnumListRepair(file, owner) ||
        HasEnumDiscardedMetadataCandidate(file, owner) ||
        HasDeprecatedValueRepairCandidate(file, owner) ||
        HasAugmentedRemarksPlaceholder(file, owner) ||
        HasPotentialImporterOwnedRemarksRefresh(file, owner) ||
        HasTruncatedImporterSummary(file, owner) ||
        HasKnownAndroidParagraphBoundaryRepairCandidate(owner) ||
        HasIncompleteCodeExampleRemarks(file, owner) ||
        HasMetadataOnlyRemarks(file, owner) ||
        HasCopiedDescriptionRepairCandidate(file, owner) ||
        HasKnownIncorrectBooleanReturnRepairCandidate(file, owner) ||
        KnownAndroidTextRepairCandidateTargets(owner).Count > 0 ||
        KnownEapChannelRepairCandidateTargets(owner).Count > 0 ||
        HasKnownAndroidProseRepairCandidate(file, owner) ||
        HasKnownUnsafeZoneTransitionTimeCandidate(owner);

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
        if (docs.UnsafeTargets?.TryGetValue(placeholder.Target, out var unsafeTargetDetail) == true)
            return Replacement.Skip("source_channel_ambiguous", unsafeTargetDetail);

        if (docs.HasMalformedSourceMarkup &&
            docs.SourceUrl.Equals(
                "https://developer.android.com/reference/android/adservices/customaudience/FetchAndJoinCustomAudienceRequest.Builder#setFetchUri(android.net.Uri)",
                StringComparison.Ordinal))
        {
            return Replacement.Skip(
                "source_documentation_malformed",
                "The official Android source contains malformed trailing markup in its FetchAndJoinCustomAudienceRequest.getFetchUri reference.");
        }

        if (placeholder.IsImporterMetadataRepair)
            return RemarksReplacementOrSkip(docs.Paragraphs, "source_remarks_missing");

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
            "remarks" or "para" => RemarksReplacementOrSkip(
                docs.Paragraphs,
                "source_remarks_missing"),
            "param" => docs.Parameters.TryGetValue(placeholder.Key, out var parameter)
                ? ChannelValueOrSkip(parameter, "param", "source_parameter_missing")
                : Replacement.Skip(
                    "source_parameter_missing",
                    $"The exact source member did not document parameter '{placeholder.Key}'."),
            "returns" => ReturnReplacement(docs),
            "value" => ValueReplacement(docs),
            "exception" => ExceptionReplacement(placeholder, docs),
            _ => Replacement.Skip(
                "unsupported_placeholder_target",
                $"Placeholder element <{placeholder.Name}> is not imported."),
        };
    }

    static Replacement LimitOverlappingRemarksReplacement(
        Placeholder placeholder,
        SourceDocs docs,
        Replacement replacement,
        XElement? existingRemarks)
    {
        if (placeholder.Name != "para" ||
            replacement.Remarks is null ||
            existingRemarks is null)
        {
            return replacement;
        }

        var sourceFragments = ExpandRemarksFragments(docs.Paragraphs);
        var elements = existingRemarks.Elements().ToList();
        var representedElements = elements
            .Select((element, index) => new
            {
                Index = index,
                Fragments = MatchingSourceFragmentIndexes(element, sourceFragments),
            })
            .Where(item => item.Fragments.Count > 0)
            .ToList();
        if (representedElements.Count == 0)
            return replacement;

        if (placeholder.RemarksChildIndex < 0 ||
            placeholder.RemarksChildIndex >= elements.Count ||
            NormalizeText(elements[placeholder.RemarksChildIndex].Value) is not
                ("To be added" or "To be added."))
        {
            return Replacement.Skip(
                "source_remarks_overlap_order_ambiguous",
                "Existing source fragments overlap a remarks placeholder whose exact child position could not be verified.");
        }

        var represented = new HashSet<int>();
        var lastFragmentIndex = -1;
        foreach (var element in representedElements)
        {
            if (element.Fragments.Zip(element.Fragments.Skip(1), (left, right) => right == left + 1)
                    .Any(isConsecutive => !isConsecutive) ||
                element.Fragments[0] <= lastFragmentIndex)
            {
                return Replacement.Skip(
                    "source_remarks_overlap_order_ambiguous",
                    "Existing source fragments were not in one unambiguous authoritative order.");
            }

            represented.UnionWith(element.Fragments);
            lastFragmentIndex = element.Fragments[^1];
        }

        var missing = Enumerable.Range(0, sourceFragments.Count)
            .Where(index => !represented.Contains(index))
            .ToList();
        if (missing.Count == 0)
        {
            return Replacement.RemoveRemarksPlaceholder();
        }

        var beforePlaceholder = representedElements
            .Where(item => item.Index < placeholder.RemarksChildIndex)
            .SelectMany(item => item.Fragments)
            .ToList();
        var afterPlaceholder = representedElements
            .Where(item => item.Index > placeholder.RemarksChildIndex)
            .SelectMany(item => item.Fragments)
            .ToList();
        if (missing.Any(fragment =>
                beforePlaceholder.Any(representedFragment => representedFragment >= fragment) ||
                afterPlaceholder.Any(representedFragment => representedFragment <= fragment)))
        {
            return Replacement.Skip(
                "source_remarks_overlap_order_conflict",
                "Replacing this placeholder would place source fragments out of their authoritative order without moving existing XML.");
        }

        var limited = missing
            .Select(index => sourceFragments[index])
            .ToList();
        return Replacement.UseRemarks(limited);
    }

    static List<string> SplitSourceSentences(string text)
    {
        var remaining = CleanSourceText(text);
        var sentences = new List<string>();
        while (remaining.Length > 0)
        {
            var sentence = SourcePage.FirstSentence(remaining).Trim();
            if (sentence.Length == 0)
            {
                break;
            }
            sentences.Add(sentence);
            if (sentence.Length >= remaining.Length)
                break;
            remaining = remaining[sentence.Length..].TrimStart();
        }
        return sentences;
    }

    static string NormalizeRemarksText(string value) =>
        NormalizeText(Regex.Replace(
            value,
            @"</?[A-Za-z][^>]*>",
            " ",
            RegexOptions.CultureInvariant));

    static string? DeprecationAwareValueText(SourceDocs docs)
    {
        var first = docs.Paragraphs.FirstOrDefault(paragraph =>
            !paragraph.IsCode && IsMeaningfulChannel(paragraph.Text, "remarks"));
        return first is not null && IsDeprecationParagraph(first.Text)
            ? FirstDocumentationParagraph(docs)
            : docs.Summary;
    }

    static Replacement RemarksReplacementOrSkip(
        IEnumerable<SourceParagraph> paragraphs,
        string missingReason)
    {
        var usable = UsableRemarks(paragraphs);
        return usable.Count > 0
            ? Replacement.UseRemarks(usable)
            : Replacement.Skip(
                missingReason,
                "The exact source member did not provide this documentation channel.");
    }

    static List<SourceParagraph> UsableRemarks(
        IEnumerable<SourceParagraph> paragraphs)
    {
        var cleaned = paragraphs
            .Select(paragraph => paragraph.IsCode
                ? paragraph
                : paragraph with { Text = CleanSourceText(paragraph.Text) })
            .ToList();
        return cleaned
            .Where((paragraph, index) => paragraph.IsCode
                ? !string.IsNullOrWhiteSpace(paragraph.Text)
                : IsMeaningfulChannel(paragraph.Text, "remarks") ||
                  (index + 1 < cleaned.Count &&
                   cleaned[index + 1].IsCode &&
                   !string.IsNullOrWhiteSpace(cleaned[index + 1].Text) &&
                   (IsExplanatoryJavaCodeLeadIn(paragraph.Text) ||
                    IsCddlCodeLeadIn(paragraph.Text))))
            .ToList();
    }

    static bool IsCddlCodeLeadIn(string text) =>
        NormalizeText(text).EndsWith(
            "CBOR with the following CDDL:",
            StringComparison.Ordinal);

    static bool IsExplanatoryJavaCodeLeadIn(string text) =>
        Regex.IsMatch(
            NormalizeText(text),
            @"\b(?:equivalent to|as in|for example|for instance|following (?:code|steps)|to implement|can be used to)\b.*:$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
        out int elementEnd,
        out int contentStart,
        out int contentEnd)
    {
        elementStart = 0;
        elementEnd = 0;
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
                        elementEnd = tagEnd + 1;
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
                XCData => true,
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

    static Replacement ReturnReplacement(SourceDocs docs)
    {
        var replacement = ChannelValueOrSkip(
            docs.Returns,
            "returns",
            "source_return_missing");
        return replacement.Text == "this build" &&
            docs.SourceUrl.StartsWith(
                "https://developer.android.com/reference/android/service/autofill/ImageTransformation.Builder#addOption(",
                StringComparison.Ordinal)
            ? replacement with { Text = "this builder" }
            : replacement;
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
        var normalized = NormalizeText(value).Trim();
        var unpunctuated = normalized.TrimEnd('.').Trim();
        if (unpunctuated.Length == 0 ||
            unpunctuated.Equals("See also:", StringComparison.OrdinalIgnoreCase) ||
            unpunctuated.Equals("See also", StringComparison.OrdinalIgnoreCase) ||
            unpunctuated.StartsWith(
                "Content and code samples on this page are subject to the licenses described in the Content License",
                StringComparison.OrdinalIgnoreCase) ||
            unpunctuated.StartsWith(
                "Java and OpenJDK are trademarks or registered trademarks of Oracle and/or its affiliates",
                StringComparison.OrdinalIgnoreCase) ||
            unpunctuated.Contains("ERROR(", StringComparison.Ordinal) ||
            unpunctuated.Contains("()}", StringComparison.Ordinal) ||
            Regex.IsMatch(
                unpunctuated,
                @"^Last updated \d{4}-\d{2}-\d{2} UTC$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            Regex.IsMatch(
                unpunctuated,
                @"^Constant Value:\s*\S+(?:\s+\(\S+\))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;
        if (Regex.IsMatch(
            unpunctuated,
            @"^\[[A-Za-z][A-Za-z0-9_-]*\]$",
            RegexOptions.CultureInvariant))
            return false;
        if (channel is "returns" or "value" or "param")
        {
            if (Regex.IsMatch(
                unpunctuated,
                @"^(?:boolean|byte|char|double|float|int|long|short|void|[A-Z][\w$]*(?:<[^>]+>)?(?:\[\])?|[\w$]+(?:\.[\w$]+)+(?:<[^>]+>)?(?:\[\])?)$",
                RegexOptions.CultureInvariant))
                return false;
            if (Regex.IsMatch(
                unpunctuated,
                @"^This value (?:cannot|can|may|must not) be null$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return false;
        }
        if (channel == "summary" && Regex.IsMatch(
            unpunctuated,
            @"^This (?:constant|method|field|class|interface) (?:is|was) deprecated(?: in API level \d+)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;
        if (unpunctuated.EndsWith(":", StringComparison.Ordinal) ||
            normalized.EndsWith("i.e.", StringComparison.OrdinalIgnoreCase) ||
            (!normalized.EndsWith(".", StringComparison.Ordinal) &&
             Regex.IsMatch(
                unpunctuated,
                @"\b(?:and|or|as|at|by|for|from|in|of|on|to|with|about|against|among|around|before|behind|below|beneath|beside|between|beyond|during|except|inside|into|near|off|over|since|through|throughout|toward|towards|under|underneath|until|up|upon|via|within|without)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
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
        Replacement replacement,
        out string updated,
        out string error)
    {
        if (replacement.Text is null)
        {
            updated = text;
            error = replacement.Detail;
            return false;
        }
        return TryReplacePlaceholder(
            text,
            block,
            placeholder,
            replacement.Text,
            replacement.Remarks,
            out updated,
            out error);
    }

    static bool TryReplacePlaceholder(
        string text,
        DocsBlock block,
        Placeholder placeholder,
        string replacement,
        out string updated,
        out string error) =>
        TryReplacePlaceholder(
            text,
            block,
            placeholder,
            replacement,
            null,
            out updated,
            out error);

    static bool TryReplacePlaceholder(
        string text,
        DocsBlock block,
        Placeholder placeholder,
        string replacement,
        IReadOnlyList<SourceParagraph>? remarksReplacement,
        out string updated,
        out string error)
    {
        var blockText = text[block.Start..block.End];
        if (placeholder.IsImporterMetadataRepair)
        {
            if (!TryParseDocsBlock(blockText, out var document))
            {
                updated = text;
                error = "Could not parse the <Docs> block for the structurally identified importer metadata remarks repair.";
                return false;
            }

            var remarksCandidates = document.Elements("remarks")
                .Where(LoadedFile.IsImporterAugmentedRemarksPlaceholder)
                .ToList();
            if (placeholder.MetadataRepairIndex < 0 ||
                placeholder.MetadataRepairIndex >= remarksCandidates.Count ||
                !TryGetElementSpan(
                    blockText,
                    remarksCandidates[placeholder.MetadataRepairIndex],
                    out var remarksSpan))
            {
                updated = text;
                error = "Could not locate the structurally identified importer metadata remarks repair without scanning CDATA, comments, or processing instructions.";
                return false;
            }

            var remarksElement = remarksCandidates[placeholder.MetadataRepairIndex];
            var directPlaceholder = remarksElement.Nodes()
                .OfType<XText>()
                .FirstOrDefault(node => node is not XCData &&
                    NormalizeText(node.Value) is "To be added" or "To be added.");
            var emptyParagraphs = directPlaceholder is null
                ? remarksElement.Elements("para")
                    .Where(paragraph => !paragraph.HasElements &&
                        NormalizeText(paragraph.Value).Length == 0)
                    .ToList()
                : [];
            var emptyParagraph = emptyParagraphs.Count == 1
                ? emptyParagraphs[0]
                : null;
            if (directPlaceholder is null && emptyParagraph is null)
            {
                updated = text;
                error = "Could not locate the structurally identified importer metadata remarks repair.";
                return false;
            }

            if (directPlaceholder is not null)
            {
                directPlaceholder.ReplaceWith(remarksReplacement is null
                    ? new[] { new XElement("para", replacement) }
                    : remarksReplacement.Select(DocumentationElement).ToArray());
            }
            else if (remarksReplacement is not null)
            {
                emptyParagraph!.ReplaceWith(
                    remarksReplacement.Select(DocumentationElement).ToArray());
            }
            else
                emptyParagraph!.Value = replacement;
            var repairedRemarks = remarksElement.ToString(SaveOptions.DisableFormatting);
            if (blockText[remarksSpan.Start..remarksSpan.End].Contains(
                    "\r\n",
                    StringComparison.Ordinal))
            {
                repairedRemarks = repairedRemarks.Replace("\n", "\r\n", StringComparison.Ordinal);
            }
            if (LoadedFile.IsImporterAugmentedRemarksPlaceholder(
                    XElement.Parse(repairedRemarks, LoadOptions.PreserveWhitespace)))
            {
                updated = text;
                error = "Importer metadata remarks repair remained a candidate after replacement.";
                return false;
            }
            var repairedBlock = blockText[..remarksSpan.Start] + repairedRemarks +
                blockText[remarksSpan.End..];
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
                out var elementEnd,
                out var localStart,
                out var localEnd))
        {
            updated = text;
            error = $"Could not locate the structurally identified {placeholder.Target} placeholder in its <Docs> block.";
            return false;
        }
        var escaped = XmlEscape(replacement);
        if (remarksReplacement is not null && placeholder.Name == "para")
        {
            var newline = blockText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var replacementStart = elementStart;
            string replacementMarkup;
            if (TryGetLineWhitespaceIndent(
                    blockText,
                    elementStart,
                    out var lineStart,
                    out var indent))
            {
                replacementStart = lineStart;
                replacementMarkup = string.Join(
                    newline,
                    remarksReplacement.Select(paragraph =>
                        RenderDocumentationParagraph(paragraph, indent)));
            }
            else
            {
                replacementMarkup = string.Concat(
                    remarksReplacement.Select(paragraph =>
                        RenderDocumentationParagraph(paragraph, "")));
            }
            var replacementParagraphBlock = blockText[..replacementStart] + replacementMarkup +
                blockText[elementEnd..];
            updated = text[..block.Start] + replacementParagraphBlock + text[block.End..];
            error = "";
            return true;
        }
        if (placeholder.Name == "remarks")
        {
            var newline = blockText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var multiline = TryGetLineWhitespaceIndent(
                blockText,
                elementStart,
                out _,
                out var indent);
            if (remarksReplacement is not null)
            {
                escaped = multiline
                    ? newline +
                      string.Join(
                          newline,
                          remarksReplacement.Select(paragraph =>
                              RenderDocumentationParagraph(paragraph, indent + "  "))) +
                      newline + indent
                    : string.Join(
                        "",
                        remarksReplacement.Select(paragraph =>
                            RenderDocumentationParagraph(paragraph, "")));
            }
            else if (multiline)
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
        => AddSourceDocumentationIfSafe(
            text,
            file,
            owner,
            docs,
            out _,
            allowEnumCreation,
            addMetadataForChannelOnlyMember);

    static string AddSourceDocumentationIfSafe(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs,
        out SourceReferenceCleanupSkip? cleanupSkip,
        bool allowEnumCreation = true,
        bool addMetadataForChannelOnlyMember = false)
    {
        cleanupSkip = null;
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
            var discardedMetadata = RemoveEnumDiscardedMetadata(updatedBlock, docs);
            if (discardedMetadata.Skip is not null)
                cleanupSkip = discardedMetadata.Skip;
            else
                updatedBlock = discardedMetadata.Text;
            if (updatedBlock.Equals(blockText, StringComparison.Ordinal))
                return text;
            return text[..block.Start] + updatedBlock + text[block.End..];
        }

        var hasRemarksPlaceholder = owner.Placeholders.Any(
            placeholder => placeholder.Name is "remarks" or "para");
        if (docs.Paragraphs.Count == 0 && hasRemarksPlaceholder)
        {
            if (HasAugmentedRemarksPlaceholder(file, owner))
            {
                var removedMetadata = RemoveImporterRemarksMetadata(blockText);
                if (removedMetadata.Skip is not null)
                {
                    cleanupSkip = removedMetadata.Skip;
                    return text;
                }
                blockText = removedMetadata.Text;
            }
            if (!addMetadataForChannelOnlyMember)
                return text[..block.Start] + blockText + text[block.End..];
            blockText = RemoveStandaloneRemarksPlaceholder(blockText);
        }

        var staleLinks = RemoveStaleSourceLinks(blockText, docs.SourceUrl, removeAll: false);
        if (staleLinks.Skip is not null)
            cleanupSkip = staleLinks.Skip;
        else
            blockText = staleLinks.Text;
        var duplicateLinks = RemoveDuplicateSourceLinks(blockText, docs.SourceUrl);
        if (duplicateLinks.Skip is not null)
            cleanupSkip ??= duplicateLinks.Skip;
        else
            blockText = duplicateLinks.Text;
        blockText = RemoveAugmentedRemarksPlaceholder(blockText);
        var metadataOnly = HasMetadataOnlyRemarks(blockText);
        if (ContainsSourceUrl(blockText, docs.SourceUrl))
        {
            if (!metadataOnly)
                return text[..block.Start] + blockText + text[block.End..];
            if (docs.Paragraphs.Count == 0)
            {
                return text[..block.Start] + blockText + text[block.End..];
            }
            var matchingLinks = RemoveMatchingSourceLinks(blockText, docs.SourceUrl);
            if (matchingLinks.Skip is not null)
                cleanupSkip ??= matchingLinks.Skip;
            else
                blockText = matchingLinks.Text;
        }

        if (!TryParseDocsBlock(blockText, out var actualDocs))
        {
            cleanupSkip = SourceReferenceCleanupSkip.NotLocated(
                "The current <Docs> block could not be parsed before adding importer source metadata.");
            return text;
        }
        var remarks = actualDocs.Element("remarks");
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
        if (!ContainsSourceUrl(actualDocs, docs.SourceUrl))
        {
            additions.Add(
                $"{paraIndent}<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
                $"title=\"Reference documentation\">{sourceLabel} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
                "</a></format></para>");
        }
        if (docs.SourceKind == "android" &&
            !ContainsSourceUrl(
                actualDocs,
                "https://developers.google.com/terms/site-policies"))
        {
            additions.Add($"{paraIndent}<para>{AndroidAttribution}</para>");
        }

        string replacementBlock;
        if (remarks is { IsEmpty: true })
        {
            if (!TryGetElementSpan(blockText, remarks, out var remarksSpan))
            {
                cleanupSkip = SourceReferenceCleanupSkip.NotLocated(
                    "The parser-identified empty <remarks> element could not be located without scanning CDATA, comments, or processing instructions.");
                return text;
            }
            var expanded =
                $"<remarks>{newline}" +
                string.Join(newline, additions) + newline +
                $"{childIndent}</remarks>";
            replacementBlock =
                blockText[..remarksSpan.Start] + expanded +
                blockText[remarksSpan.End..];
        }
        else if (remarks is not null)
        {
            if (!TryGetElementSpan(blockText, remarks, out var remarksSpan) ||
                !TryGetClosingElementStart(blockText, remarksSpan, out var remarksClose))
            {
                cleanupSkip = SourceReferenceCleanupSkip.NotLocated(
                    "The parser-identified <remarks> element could not be located without scanning CDATA, comments, or processing instructions.");
                return text;
            }
            var sourceReferenceParagraph = attributionOnly &&
                !replacedRemarksPlaceholder &&
                docs.Paragraphs.Count > 0
                ? remarks.Elements("para").FirstOrDefault(paragraph =>
                    TryGetImporterSourceReferenceUrl(
                        paragraph,
                        out var sourceUrl) &&
                    UrlsEqual(sourceUrl, docs.SourceUrl))
                : null;
            var sourceReferencePara = -1;
            if (sourceReferenceParagraph is not null &&
                TryGetElementSpan(
                    blockText,
                    sourceReferenceParagraph,
                    out var sourceReferenceSpan))
            {
                sourceReferencePara = sourceReferenceSpan.Start;
            }
            var attributionParagraph = remarks
                .Descendants("a")
                .FirstOrDefault(anchor => UrlsEqual(
                    (string?)anchor.Attribute("href") ?? "",
                    "https://developers.google.com/terms/site-policies"))
                ?.Ancestors("para")
                .FirstOrDefault();
            var attributionPara = -1;
            if (attributionParagraph is not null)
            {
                if (!TryGetElementSpan(blockText, attributionParagraph, out var attributionSpan))
                {
                    cleanupSkip = SourceReferenceCleanupSkip.NotLocated(
                        "The parser-identified attribution paragraph could not be located without scanning CDATA, comments, or processing instructions.");
                    return text;
                }
                attributionPara = attributionSpan.Start;
            }
            var insertionTarget = sourceReferencePara >= 0
                ? sourceReferencePara
                : attributionPara >= 0
                    ? attributionPara
                    : remarksClose;
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

    sealed record RemarksRefreshResult(string Text, string? Reason, string? Detail);

    static bool HasPotentialImporterOwnedRemarksRefresh(LoadedFile file, DocsOwner owner)
    {
        if (owner.IsEnumField || owner.SourceRequest is null)
            return false;

        var remarks = owner.Docs.Element("remarks");
        return remarks is not null &&
            (IsPotentialImporterOwnedRemarks(remarks, owner.SourceRequest.Kind) ||
             IsPotentialHybridImporterOwnedRemarks(remarks, owner.SourceRequest.Kind) ||
             FindDreamFocusRepairParagraph(owner.Id, owner.Docs, null) is not null);
    }

    static XElement? FindDreamFocusRepairParagraph(
        string memberId,
        XElement document,
        SourceDocs? source)
    {
        if (memberId != DreamFocusMemberId ||
            document.Element("remarks") is not XElement remarks ||
            remarks.HasAttributes ||
            SignificantNodes(remarks) is not
                [XElement paragraph, XElement sourceReference, XElement attribution] ||
            paragraph.Name != "para" ||
            paragraph.HasAttributes ||
            !HasPlainTextContent(paragraph, out var value) ||
            value != IncorrectDreamFocusRemark ||
            !IsImporterAttributionParagraph(attribution))
        {
            return null;
        }
        var expectedSource = new SourceDocs(
            "", [], new(), "", new(), DreamFocusSourceUrl,
            "android.service.dreams.DreamService.onWindowFocusChanged", "android");
        var legacyReference = XElement.Parse(
            $"<para><format type=\"text/html\"><a href=\"{DreamFocusSourceUrl}\" " +
            "title=\"Reference documentation\">Java documentation for " +
            "<code>android.service.dreams.DreamService.onWindowFocusChanged(boolean)</code>." +
            "</a></format></para>");
        if ((!ImporterMarkupEquals(sourceReference, ImporterSourceReference(expectedSource)) &&
             !ImporterMarkupEquals(sourceReference, legacyReference)) ||
            (source is not null &&
                (source.SourceKind != "android" ||
                 source.SourceUrl != DreamFocusSourceUrl ||
                 source.Paragraphs is not [{ Text: CorrectDreamFocusRemark, IsCode: false }])))
        {
            return null;
        }
        return paragraph;
    }

    static RemarksRefreshResult RefreshImporterOwnedRemarks(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var document))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The current <Docs> block could not be parsed before rebuilding importer-owned remarks.");
        }
        if (FindDreamFocusRepairParagraph(owner.Id, document, docs) is XElement focusParagraph)
        {
            if (!TryGetElementSpan(blockText, focusParagraph, out var focusSpan))
            {
                return new RemarksRefreshResult(
                    text,
                    "existing_remarks_not_importer_owned",
                    "The exact importer-owned focus paragraph could not be located safely.");
            }
            var correctedBlock = blockText[..focusSpan.Start] +
                $"<para>{XmlEscape(CorrectDreamFocusRemark)}</para>" +
                blockText[focusSpan.End..];
            return new RemarksRefreshResult(
                text[..block.Start] + correctedBlock + text[block.End..],
                null,
                null);
        }
        var remarks = document.Element("remarks");
        if (remarks is not null &&
            IsPotentialHybridImporterOwnedRemarks(remarks, docs.SourceKind))
        {
            return RefreshIncompleteImporterRemarks(text, file, owner, docs);
        }
        if (remarks is null || !IsPotentialImporterOwnedRemarks(remarks, docs.SourceKind))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing remarks did not have the strict importer source-reference and attribution structure.");
        }

        var sourceParagraphs = UsableRemarks(docs.Paragraphs);
        if (sourceParagraphs.Count == 0)
        {
            return new RemarksRefreshResult(
                text,
                "source_remarks_missing",
                "The exact source member did not provide usable remarks to refresh.");
        }

        var existing = remarks.Elements().ToList();
        var sourceReferenceIndex = existing.FindIndex(element =>
            TryGetImporterSourceReferenceUrl(
                element.ToString(SaveOptions.DisableFormatting),
                out _));
        var expectedSourceReference = ImporterSourceReference(docs);
        if (sourceReferenceIndex <= 0 ||
            !ImporterMarkupEquals(existing[sourceReferenceIndex], expectedSourceReference))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing importer source reference did not exactly match the mapped source member.");
        }

        var expectedSourceParagraphs = sourceParagraphs
            .Select(DocumentationElement)
            .ToList();
        var existingSourceParagraphs = existing.Take(sourceReferenceIndex).ToList();
        if (!MatchesSourceParagraphSubsequence(
                existingSourceParagraphs,
                expectedSourceParagraphs,
                allowKnownAndroidCorrections: true) &&
            (!HasLegacyFormattedSourceReference(existing[sourceReferenceIndex]) ||
             !MatchesSourceParagraphSubsequence(
                 CoalesceLegacyNestedCodeContainers(existingSourceParagraphs),
                 expectedSourceParagraphs,
                 allowKnownAndroidCorrections: true)))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing remarks prose was not an ordered structural subset of the exact mapped source.");
        }

        if (existingSourceParagraphs.Count == expectedSourceParagraphs.Count &&
            existingSourceParagraphs.Zip(
                expectedSourceParagraphs,
                (actual, expected) => ImporterMarkupEquals(actual, expected))
                .All(equal => equal))
        {
            return new RemarksRefreshResult(
                text,
                "source_remarks_current",
                "The importer-owned remarks already contain every visible source paragraph and code block.");
        }

        if (!TryGetElementSpan(blockText, remarks, out var remarksSpan))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The structurally verified remarks could not be located without scanning CDATA, comments, or processing instructions.");
        }

        var newline = file.Newline;
        var docsIndent = file.IndentAt(block.Start);
        var remarksIndent = docsIndent + "  ";
        var paragraphIndent = remarksIndent + "  ";
        var replacement = RenderImporterOwnedRemarks(
            sourceParagraphs,
            docs,
            newline,
            remarksIndent,
            paragraphIndent);
        var updatedBlock = blockText[..remarksSpan.Start] + replacement +
            blockText[remarksSpan.End..];
        return new RemarksRefreshResult(
            text[..block.Start] + updatedBlock + text[block.End..],
            null,
            null);
    }

    static bool IsPotentialImporterOwnedRemarks(XElement remarks, string sourceKind)
    {
        if (remarks.HasAttributes ||
            remarks.Nodes().Any(node => node switch
            {
                XElement => false,
                XCData => true,
                XText text => !string.IsNullOrWhiteSpace(text.Value),
                _ => true,
            }))
        {
            return false;
        }

        var elements = remarks.Elements().ToList();
        var sourceReferenceIndexes = elements
            .Select((element, index) => (element, index))
            .Where(item => TryGetImporterSourceReferenceUrl(
                item.element.ToString(SaveOptions.DisableFormatting),
                out _))
            .Select(item => item.index)
            .ToList();
        var requiredMetadataCount = sourceKind == "android" ? 2 : 1;
        if (sourceReferenceIndexes.Count != 1 ||
            sourceReferenceIndexes[0] != elements.Count - requiredMetadataCount ||
            sourceReferenceIndexes[0] == 0)
        {
            return false;
        }

        if (sourceKind == "android" &&
            !ImporterMarkupEquals(
                elements[^1],
                XElement.Parse($"<para>{AndroidAttribution}</para>")))
        {
            return false;
        }

        return elements
            .Take(sourceReferenceIndexes[0])
            .All(IsImporterRenderedSourceParagraph);
    }

    static bool IsPotentialHybridImporterOwnedRemarks(XElement remarks, string sourceKind)
    {
        if (sourceKind != "java")
        {
            return false;
        }

        var elements = remarks.Elements().ToList();
        var sourceReferenceIndexes = elements
            .Select((element, index) => (element, index))
            .Where(item => IsImporterJavaSourceReference(item.element))
            .Select(item => item.index)
            .ToList();
        if (sourceReferenceIndexes.Count != 1 ||
            sourceReferenceIndexes[0] != elements.Count - 2 ||
            sourceReferenceIndexes[0] == 0 ||
            !IsImporterAttributionParagraph(elements[^1]))
        {
            return false;
        }

        return true;
    }

    static bool IsImporterJavaSourceReference(XElement paragraph)
    {
        if (!TryGetImporterSourceReferenceUrl(paragraph, out var sourceUrl) ||
            !sourceUrl.StartsWith(JavaReference, StringComparison.Ordinal))
        {
            return false;
        }

        var anchor = paragraph.Descendants("a").SingleOrDefault();
        return anchor is not null &&
            NormalizeText(anchor.Nodes().OfType<XText>().FirstOrDefault()?.Value ?? "")
                .Equals("Java reference for", StringComparison.Ordinal);
    }

    static bool HasLegacyFormattedSourceReference(XElement sourceReference) =>
        sourceReference.DescendantNodes()
            .OfType<XText>()
            .Any(text => string.IsNullOrWhiteSpace(text.Value) &&
                (text.Value.Contains('\n') || text.Value.Contains('\r')));

    static IReadOnlyList<XElement> CoalesceLegacyNestedCodeContainers(
        IReadOnlyList<XElement> sourceParagraphs)
    {
        var coalesced = new List<XElement>();
        foreach (var paragraph in sourceParagraphs)
        {
            if (paragraph.Name == "code" &&
                coalesced.LastOrDefault() is XElement previous &&
                previous.Name == "code" &&
                paragraph.ToString(SaveOptions.DisableFormatting).Equals(
                    previous.ToString(SaveOptions.DisableFormatting),
                    StringComparison.Ordinal))
            {
                continue;
            }
            coalesced.Add(paragraph);
        }
        return coalesced;
    }

    static bool IsImporterRenderedSourceParagraph(XElement element)
    {
        if (element.Name.LocalName == "para" &&
            !element.HasAttributes &&
            HasPlainTextContent(element, out var prose))
        {
            return NormalizeText(prose).Length > 0;
        }

        if (element.Name.LocalName == "code" &&
            element.Attributes().Count() == 1 &&
            (string?)element.Attribute("lang") == "text/java" &&
            HasPlainTextContent(element, out var code))
        {
            return !string.IsNullOrWhiteSpace(code);
        }

        return false;
    }

    static bool MatchesSourceParagraphSubsequence(
        IReadOnlyList<XElement> existing,
        IReadOnlyList<XElement> expected,
        bool allowKnownAndroidCorrections = false)
    {
        if (existing.Count == 0 ||
            !ImporterMarkupEquals(
                existing[0],
                expected[0],
                allowKnownAndroidCorrections))
        {
            return false;
        }

        var expectedIndex = 1;
        foreach (var element in existing.Skip(1))
        {
            while (expectedIndex < expected.Count &&
                !ImporterMarkupEquals(
                    element,
                    expected[expectedIndex],
                    allowKnownAndroidCorrections))
            {
                expectedIndex++;
            }
            if (expectedIndex == expected.Count)
                return false;
            expectedIndex++;
        }
        return true;
    }

    static XElement ImporterSourceReference(SourceDocs docs)
    {
        var sourceName = docs.SourceKind == "android" ? "Android" : "Java";
        return XElement.Parse(
            $"<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{sourceName} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
            "</a></format></para>");
    }

    static string RenderImporterOwnedRemarks(
        IReadOnlyList<SourceParagraph> sourceParagraphs,
        SourceDocs docs,
        string newline,
        string remarksIndent,
        string paragraphIndent)
    {
        var paragraphs = sourceParagraphs
            .Select(paragraph => RenderDocumentationParagraph(paragraph, paragraphIndent))
            .ToList();
        paragraphs.Add(
            $"{paragraphIndent}{ImporterSourceReference(docs).ToString(SaveOptions.DisableFormatting)}");
        if (docs.SourceKind == "android")
            paragraphs.Add($"{paragraphIndent}<para>{AndroidAttribution}</para>");
        return $"<remarks>{newline}" +
            string.Join(newline, paragraphs) + newline +
            $"{remarksIndent}</remarks>";
    }

    static bool ImporterMarkupEquals(
        XElement actual,
        XElement expected,
        bool allowKnownAndroidCorrections = false)
    {
        if (actual.Name != expected.Name ||
            actual.Attributes().Count() != expected.Attributes().Count() ||
            actual.Attributes().OrderBy(attribute => attribute.Name.ToString(), StringComparer.Ordinal)
                .Zip(
                    expected.Attributes().OrderBy(
                        attribute => attribute.Name.ToString(),
                        StringComparer.Ordinal),
                    (left, right) =>
                        left.Name == right.Name &&
                        WebUtility.HtmlDecode(left.Value) == WebUtility.HtmlDecode(right.Value))
                .Any(equal => !equal))
        {
            return false;
        }

        var actualNodes = actual.Nodes()
            .Where(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value))
            .ToList();
        var expectedNodes = expected.Nodes()
            .Where(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value))
            .ToList();
        if (actualNodes.Any(node => node is XCData) ||
            expectedNodes.Any(node => node is XCData) ||
            actualNodes.Count != expectedNodes.Count)
            return false;

        for (var index = 0; index < actualNodes.Count; index++)
        {
            if (actualNodes[index] is XElement actualElement &&
                expectedNodes[index] is XElement expectedElement)
            {
                if (!ImporterMarkupEquals(
                        actualElement,
                        expectedElement,
                        allowKnownAndroidCorrections))
                    return false;
            }
            else if (actualNodes[index] is XText actualText &&
                     expectedNodes[index] is XText expectedText)
            {
                var actualValue = allowKnownAndroidCorrections
                    ? NormalizeText(SourcePage.NormalizeAndroidSourceText(actualText.Value))
                    : NormalizeText(actualText.Value);
                var expectedValue = allowKnownAndroidCorrections
                    ? NormalizeText(SourcePage.NormalizeAndroidSourceText(expectedText.Value))
                    : NormalizeText(expectedText.Value);
                if (!actualValue.Equals(
                    expectedValue,
                    StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    static bool HasAugmentedRemarksPlaceholder(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return TryParseDocsBlock(
                file.Text[block.Start..block.End],
                out var docs) &&
            IsAugmentedRemarksPlaceholder(docs.Element("remarks"));
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
        docs.UnsafeTargets?.ContainsKey("remarks") != true &&
        (!deferredRemarksPlaceholder || replacedRemarksPlaceholder);

    static bool HasCopiedDescriptionRepairCandidate(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return TryParseDocsBlock(
                file.Text[block.Start..block.End],
                out var docs) &&
            HasCopiedDescriptionRepairCandidate(docs);
    }

    static bool HasCopiedDescriptionSummaryRepairCandidate(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return TryParseDocsBlock(
                file.Text[block.Start..block.End],
                out var docs) &&
            HasCopiedDescriptionSummaryRepairCandidate(docs);
    }

    static bool HasCopiedDescriptionRemarksRepairCandidate(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return TryParseDocsBlock(
                file.Text[block.Start..block.End],
                out var docs) &&
            HasCopiedDescriptionRemarksRepairCandidate(docs);
    }

    static bool HasCopiedDescriptionRepairCandidate(XElement docs) =>
        HasCopiedDescriptionSummaryRepairCandidate(docs) ||
        HasCopiedDescriptionRemarksRepairCandidate(docs);

    static bool HasKnownIncorrectBooleanReturnRepairCandidate(
        LoadedFile file,
        DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return IsManagedBooleanReturn(owner.Member) &&
            TryParseDocsBlock(file.Text[block.Start..block.End], out var docs) &&
            XNode.DeepEquals(docs, owner.Docs) &&
            FindKnownBooleanReturnRepair(docs) is not null;
    }

    static BooleanReturnRepairResult RepairKnownBooleanReturn(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs) ||
            !IsManagedBooleanReturn(owner.Member) ||
            FindKnownBooleanReturnRepair(actualDocs) is not { } repair ||
            !UrlsEqual(docs.SourceUrl, repair.SourceUrl) ||
            !HasExactImporterSourceReference(actualDocs, docs) ||
            ReturnReplacement(docs).Text is not string replacement ||
            !replacement.Equals(repair.CorrectReturn, StringComparison.Ordinal))
        {
            return BooleanReturnRepairResult.NoChange(text);
        }

        var returns = actualDocs.Element("returns");
        if (returns is null || !TryGetElementSpan(blockText, returns, out var returnsSpan))
        {
            return BooleanReturnRepairResult.Failure(
                text,
                "boolean_return_target_not_located",
                "The parser-identified Boolean return description could not be located without scanning CDATA, comments, or processing instructions.");
        }

        var replacementElement = $"<returns>{XmlEscape(replacement)}</returns>";
        var updatedBlock = blockText[..returnsSpan.Start] + replacementElement +
            blockText[returnsSpan.End..];
        return BooleanReturnRepairResult.RepairedText(
            text[..block.Start] + updatedBlock + text[block.End..]);
    }

    static KnownBooleanReturnRepair? FindKnownBooleanReturnRepair(XElement docs)
    {
        var sourceUrls = docs
            .Descendants("para")
            .Select(paragraph => TryGetImporterSourceReferenceUrl(paragraph, out var sourceUrl)
                ? sourceUrl
                : null)
            .Where(sourceUrl => sourceUrl is not null)
            .Cast<string>()
            .ToList();
        if (sourceUrls.Count != 1 ||
            docs.Element("returns") is not XElement returns)
        {
            return null;
        }

        var repair = KnownBooleanReturnRepairs.SingleOrDefault(candidate =>
            UrlsEqual(candidate.SourceUrl, sourceUrls[0]));
        return repair is not null &&
            HasExactKnownBooleanReturnMarkup(returns, repair)
            ? repair
            : null;
    }

    static JavaExampleRepairResult RepairKnownJavaExample(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs) ||
            FindKnownJavaExampleRepair(actualDocs, sourceDocs, owner.Id) is not { } repair ||
            actualDocs.Element("remarks")?.Elements("code").SingleOrDefault(code =>
                IsKnownJavaExampleRepairCandidate(code, repair)) is not XElement code ||
            !TryGetElementSpan(blockText, code, out var codeSpan))
        {
            return JavaExampleRepairResult.NoChange(text);
        }

        var replacement = $"<code lang=\"text/java\">{new XText(repair.CorrectCode).ToString(SaveOptions.DisableFormatting)}</code>";
        var updatedBlock = blockText[..codeSpan.Start] + replacement +
            blockText[codeSpan.End..];
        return JavaExampleRepairResult.RepairedText(
            text[..block.Start] + updatedBlock + text[block.End..]);
    }

    static KnownJavaExampleRepair? FindKnownJavaExampleRepair(
        XElement docs,
        SourceDocs sourceDocs,
        string? memberId = null)
    {
        if (!HasExactImporterSourceReference(docs, sourceDocs))
            return null;

        var repair = KnownJavaExampleRepairs.SingleOrDefault(candidate =>
            UrlsEqual(candidate.SourceUrl, sourceDocs.SourceUrl));
        if (repair is null ||
            (repair.MemberId is not null &&
             (repair.MemberId != memberId ||
              docs.Element("remarks") is not XElement remarks ||
              !ImporterMarkupEquals(
                  remarks,
                  XElement.Parse(RenderImporterOwnedRemarks(
                      UsableRemarks(sourceDocs.Paragraphs),
                      sourceDocs,
                      "\n",
                      "",
                      ""))))) ||
            docs.Element("remarks")?.Elements("code").Where(code =>
                IsKnownJavaExampleRepairCandidate(code, repair)).Count() != 1)
        {
            return null;
        }

        return repair;
    }

    static bool IsKnownJavaExampleRepairCandidate(
        XElement code,
        KnownJavaExampleRepair repair)
    {
        if (code.Attributes().Count() != 1 ||
            (string?)code.Attribute("lang") != "text/java" ||
            !code.Nodes().All(node => node is XText && node is not XCData))
        {
            return false;
        }

        return code.Value.Equals(repair.IncompleteCode, StringComparison.Ordinal);
    }

    static JavaProseRepairResult RepairKnownJavaProse(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs) ||
            FindKnownJavaProseRepair(actualDocs, sourceDocs) is not { } repair ||
            actualDocs.Element("remarks")?.Elements("para").FirstOrDefault() is not XElement paragraph ||
            !TryGetElementSpan(blockText, paragraph, out var paragraphSpan))
        {
            return JavaProseRepairResult.NoChange(text);
        }

        var replacement = $"<para>{XmlEscape(repair.CorrectText)}</para>";
        var updatedBlock = blockText[..paragraphSpan.Start] + replacement +
            blockText[paragraphSpan.End..];
        return JavaProseRepairResult.RepairedText(
            text[..block.Start] + updatedBlock + text[block.End..]);
    }

    static UnsafeParameterRepairResult RepairKnownUnsafeParameter(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        const string unsafeDuration =
            "The duration for the new stroke. Must not be negative.";
        var zoneTransition = IsKnownUnsafeZoneTransitionTime(owner.Id, sourceDocs);
        if (!zoneTransition && !IsKnownUnsafeContinueStrokeDuration(sourceDocs.SourceUrl))
        {
            return UnsafeParameterRepairResult.NoChange(text);
        }
        if (zoneTransition &&
            (owner.Member?.Element("Parameters")?.Elements("Parameter").ElementAtOrDefault(3) is not XElement boundTime ||
             (string?)boundTime.Attribute("Name") != "time" ||
             (string?)boundTime.Attribute("Type") != "Java.Time.LocalTime"))
        {
            return UnsafeParameterRepairResult.NoChange(text);
        }

        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        var parameterName = zoneTransition ? "time" : "duration";
        var unsafeText = zoneTransition ? KnownUnsafeZoneTransitionTime : unsafeDuration;
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            (zoneTransition &&
             (actualDocs.HasAttributes ||
              !XNode.DeepEquals(actualDocs, owner.Docs) ||
              !HasExactImporterSourceReference(actualDocs, sourceDocs) ||
              actualDocs.Nodes().Any(node => node switch
              {
                  XElement => false,
                  XText whitespace when node is not XCData => !string.IsNullOrWhiteSpace(whitespace.Value),
                  _ => true,
              }) ||
              actualDocs.Element("remarks") is not XElement remarks ||
              !ImporterMarkupEquals(
                  remarks,
                  XElement.Parse(RenderImporterOwnedRemarks(
                      UsableRemarks(sourceDocs.Paragraphs), sourceDocs, "\n", "", ""))))) ||
            actualDocs.Elements("param").Where(parameter =>
                (string?)parameter.Attribute("name") == parameterName).ToList() is not [XElement parameter] ||
            parameter.Attributes().Count() != 1 ||
            !HasPlainTextContent(parameter, out var currentText) ||
            !currentText.Equals(unsafeText, StringComparison.Ordinal) ||
            !TryGetElementSpan(blockText, parameter, out var parameterSpan))
        {
            return UnsafeParameterRepairResult.NoChange(text);
        }

        var placeholder = $"<param name=\"{parameterName}\">To be added.</param>";
        var updatedBlock = blockText[..parameterSpan.Start] + placeholder +
            blockText[parameterSpan.End..];
        return UnsafeParameterRepairResult.RepairedText(
            text[..block.Start] + updatedBlock + text[block.End..],
            parameterName);
    }

    static KnownJavaProseRepair? FindKnownJavaProseRepair(
        XElement docs,
        SourceDocs sourceDocs)
    {
        if (!HasExactImporterSourceReference(docs, sourceDocs))
            return null;

        var repair = KnownJavaProseRepairs.SingleOrDefault(candidate =>
            candidate.SourceUrl.Equals(sourceDocs.SourceUrl, StringComparison.Ordinal));
        if (repair is null ||
            docs.Element("remarks") is not XElement remarks ||
            remarks.Nodes().Any(node => node switch
            {
                XElement => false,
                XText text => !string.IsNullOrWhiteSpace(text.Value),
                _ => true,
            }) ||
            remarks.Elements().ToList() is not [XElement paragraph, XElement sourceReference, XElement attribution] ||
            paragraph.Name != "para" ||
            paragraph.HasAttributes ||
            !HasPlainTextContent(paragraph, out var paragraphText) ||
            !paragraphText.Equals(repair.IncorrectText, StringComparison.Ordinal) ||
            !IsCanonicalImporterSourceReferenceParagraph(sourceReference) ||
            !IsImporterAttributionParagraph(attribution))
        {
            return null;
        }

        return repair;
    }

    static AndroidParameterRepairResult RepairKnownAndroidParameter(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs) ||
            FindKnownAndroidParameterRepair(owner.Id, actualDocs, sourceDocs) is not { } repair ||
            actualDocs.Elements("param").SingleOrDefault(parameter =>
                (string?)parameter.Attribute("name") == repair.ParameterName) is not XElement parameter ||
            !TryGetElementSpan(blockText, parameter, out var parameterSpan))
        {
            return AndroidParameterRepairResult.NoChange(text);
        }

        var replacement =
            $"<param name=\"{XmlAttributeEscape(repair.ParameterName)}\">{XmlEscape(repair.CorrectText)}</param>";
        var updatedBlock = blockText[..parameterSpan.Start] + replacement +
            blockText[parameterSpan.End..];
        return AndroidParameterRepairResult.RepairedText(
            text[..block.Start] + updatedBlock + text[block.End..],
            repair.ParameterName);
    }

    static KnownAndroidParameterRepair? FindKnownAndroidParameterRepair(
        string memberId,
        XElement docs,
        SourceDocs sourceDocs)
    {
        if (!HasExactImporterSourceReference(docs, sourceDocs))
            return null;

        var repair = KnownAndroidParameterRepairs.SingleOrDefault(candidate =>
            candidate.MemberId.Equals(memberId, StringComparison.Ordinal) &&
            candidate.SourceUrl.Equals(sourceDocs.SourceUrl, StringComparison.Ordinal));
        if (repair is null ||
            docs.Elements("param").Where(parameter =>
                (string?)parameter.Attribute("name") == repair.ParameterName).ToList() is not [XElement parameter] ||
            parameter.Attributes().Count() != 1 ||
            !HasPlainTextContent(parameter, out var parameterText) ||
            !parameterText.Equals(repair.IncorrectText, StringComparison.Ordinal))
        {
            return null;
        }

        return repair;
    }

    static bool HasKnownAndroidProseRepairCandidate(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return TryParseDocsBlock(file.Text[block.Start..block.End], out var docs) &&
            FindKnownAndroidProseRepair(owner.Id, docs, null) is not null;
    }

    static AndroidProseRepairResult RepairKnownAndroidProse(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs) ||
            FindKnownAndroidProseRepair(owner.Id, actualDocs, sourceDocs) is not { } repair ||
            actualDocs.Element("summary") is not XElement summary ||
            actualDocs.Element("remarks")?.Elements("para").FirstOrDefault() is not XElement paragraph ||
            !TryGetElementSpan(blockText, summary, out var summaryElementSpan) ||
            !TryGetElementSpan(blockText, paragraph, out var paragraphElementSpan) ||
            !TryGetDirectTextElementContentSpan(blockText, summaryElementSpan, out var summarySpan) ||
            !TryGetDirectTextElementContentSpan(blockText, paragraphElementSpan, out var paragraphSpan))
        {
            return AndroidProseRepairResult.NoChange(text);
        }

        var edits = new[]
        {
            new XmlSpanEdit(summarySpan, XmlEscape(repair.CorrectSummary)),
            new XmlSpanEdit(paragraphSpan, XmlEscape(repair.CorrectRemarks)),
        };
        foreach (var edit in edits.OrderByDescending(edit => edit.Span.Start))
        {
            blockText = blockText[..edit.Span.Start] + edit.Replacement +
                blockText[edit.Span.End..];
        }
        return AndroidProseRepairResult.RepairedText(
            text[..block.Start] + blockText + text[block.End..]);
    }

    static KnownAndroidProseRepair? FindKnownAndroidProseRepair(
        string memberId,
        XElement docs,
        SourceDocs? sourceDocs)
    {
        var sourceUrls = docs
            .Descendants("para")
            .Select(paragraph => TryGetImporterSourceReferenceUrl(paragraph, out var sourceUrl)
                ? sourceUrl
                : null)
            .Where(sourceUrl => sourceUrl is not null)
            .Cast<string>()
            .ToList();
        if (sourceUrls.Count != 1 ||
            docs.Element("summary") is not XElement summary ||
            summary.HasAttributes ||
            !HasPlainTextContent(summary, out var summaryText) ||
            docs.Element("remarks") is not XElement remarks ||
            remarks.HasAttributes ||
            remarks.Nodes().Any(node => node switch
            {
                XElement => false,
                XText text => !string.IsNullOrWhiteSpace(text.Value),
                _ => true,
            }) ||
            remarks.Elements().ToList() is not [XElement paragraph, XElement sourceReference, XElement attribution] ||
            paragraph.Name != "para" ||
            paragraph.HasAttributes ||
            !HasPlainTextContent(paragraph, out var paragraphText) ||
            !IsCanonicalImporterSourceReferenceParagraph(sourceReference) ||
            !IsImporterAttributionParagraph(attribution))
        {
            return null;
        }

        var repair = KnownAndroidProseRepairs.SingleOrDefault(candidate =>
            candidate.MemberId.Equals(memberId, StringComparison.Ordinal) &&
            candidate.SourceUrl.Equals(sourceUrls[0], StringComparison.Ordinal));
        if (repair is null ||
            !summaryText.Equals(repair.IncorrectSummary, StringComparison.Ordinal) ||
            !paragraphText.Equals(repair.IncorrectRemarks, StringComparison.Ordinal) ||
            (sourceDocs is not null &&
                (!sourceDocs.SourceUrl.Equals(repair.SourceUrl, StringComparison.Ordinal) ||
                 !HasExactImporterSourceReference(docs, sourceDocs))))
        {
            return null;
        }

        return repair;
    }

    static bool HasExactImporterSourceReference(XElement docs, SourceDocs sourceDocs)
    {
        var sourceReferences = docs
            .Descendants("para")
            .Where(paragraph => TryGetImporterSourceReferenceUrl(paragraph, out _))
            .ToList();
        return sourceReferences.Count == 1 &&
            ImporterMarkupEquals(
                sourceReferences[0],
                ImporterSourceReference(sourceDocs));
    }

    static bool HasKnownAndroidParagraphBoundaryRepairCandidate(DocsOwner owner) =>
        owner.Id.Equals(ControlTemplateMemberId, StringComparison.Ordinal) &&
        owner.Docs.Elements("summary").ToList() is [XElement summary] &&
        !summary.HasAttributes &&
        HasPlainTextContent(summary, out var summaryText) &&
        summaryText.Equals(LegacyControlTemplateSummary, StringComparison.Ordinal);

    static List<string> KnownControlsLifecycleRepairTargets(DocsOwner owner)
    {
        var repair = KnownControlsLifecycleRepairs.SingleOrDefault(candidate =>
            candidate.MemberId == owner.Id);
        if (repair is null)
            return [];

        var targets = new List<string>();
        if (owner.Docs.Elements("remarks").Any(remarks =>
                NormalizeText(remarks.Value).Contains(repair.OriginalParagraphs[0], StringComparison.Ordinal)))
            targets.Add("remarks");
        if (repair.IncorrectReturn is not null &&
            owner.Docs.Elements("returns").Any(returns =>
                NormalizeText(returns.Value) == repair.IncorrectReturn))
            targets.Add("returns");
        return targets;
    }

    sealed record ControlsLifecycleRepairResult(string Text, string? Reason, string? Detail);

    static ControlsLifecycleRepairResult RepairKnownControlsLifecycle(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        var targets = KnownControlsLifecycleRepairTargets(owner);
        if (targets.Count == 0)
            return new(text, null, null);
        var repair = KnownControlsLifecycleRepairs.Single(candidate => candidate.MemberId == owner.Id);
        if (sourceDocs.SourceKind != "android" ||
            sourceDocs.SourceUrl != repair.SourceUrl ||
            sourceDocs.Summary != repair.Summary ||
            !sourceDocs.Paragraphs.SequenceEqual(
                repair.SafeParagraphs.Select(paragraph => new SourceParagraph(paragraph, false))) ||
            (targets.Contains("returns") &&
                (sourceDocs.UnsafeTargets?.ContainsKey("returns") != true ||
                 RemoveLeadingJavaType(sourceDocs.Returns) != repair.IncorrectReturn)))
        {
            return new(text, "source_controls_lifecycle_mismatch",
                "The exact lifecycle source did not match the verified unsafe inherited contracts and retained reference fragments.");
        }

        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs))
            return new(text, "controls_lifecycle_target_not_located",
                "The parser-selected Docs block did not match the untouched owner.");

        var expectedRemarks = new XElement("remarks",
            repair.OriginalParagraphs.Select(paragraph => new XElement("para", paragraph)),
            ImporterSourceReference(sourceDocs),
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        var correctedRemarks = XElement.Parse(RenderImporterOwnedRemarks(
            sourceDocs.Paragraphs, sourceDocs, file.Newline, "", ""));
        if (actualDocs.Elements("summary").ToList() is not [XElement summary] ||
            summary.HasAttributes ||
            !HasPlainTextContent(summary, out var summaryText) ||
            summaryText != repair.Summary ||
            actualDocs.Elements("remarks").ToList() is not [XElement remarks] ||
            !ImporterMarkupEquals(remarks, targets.Contains("remarks") ? expectedRemarks : correctedRemarks) ||
            actualDocs.Elements("returns").ToList() is not [XElement returns] ||
            (targets.Contains("returns") &&
                (returns.HasAttributes ||
                 !HasPlainTextContent(returns, out var returnText) ||
                 returnText != repair.IncorrectReturn)))
        {
            return new(text, "existing_controls_lifecycle_not_importer_owned",
                "The complete lifecycle channels, exact source reference, and attribution did not match the known importer output.");
        }

        var edits = new List<XmlSpanEdit>();
        foreach (var target in targets)
        {
            var element = target == "remarks" ? remarks : returns;
            if (!TryGetElementSpan(blockText, element, out var span))
                return new(text, "controls_lifecycle_target_not_located",
                    "The verified lifecycle channel could not be located without scanning authored XML.");
            var indent = file.IndentAt(block.Start) + "  ";
            edits.Add(new(span, target == "remarks"
                ? RenderImporterOwnedRemarks(sourceDocs.Paragraphs, sourceDocs, file.Newline, indent, indent + "  ")
                : "<returns>To be added.</returns>"));
        }
        foreach (var edit in edits.OrderByDescending(edit => edit.Span.Start))
            blockText = blockText[..edit.Span.Start] + edit.Replacement + blockText[edit.Span.End..];
        return new(text[..block.Start] + blockText + text[block.End..], null, null);
    }

    sealed record ParagraphBoundaryRepairResult(string Text, string? Reason, string? Detail);

    static ParagraphBoundaryRepairResult RepairKnownAndroidParagraphBoundary(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        if (!HasKnownAndroidParagraphBoundaryRepairCandidate(owner))
            return new(text, null, null);

        if (sourceDocs.SourceKind != "android" ||
            !sourceDocs.SourceUrl.Equals(ControlTemplateSourceUrl, StringComparison.Ordinal) ||
            sourceDocs.Summary != ControlTemplateLead ||
            sourceDocs.Paragraphs.Any(paragraph => paragraph.IsCode) ||
            !sourceDocs.Paragraphs.Select(paragraph => paragraph.Text)
                .SequenceEqual([ControlTemplateLead, ControlTemplateDescription]))
        {
            return new(
                text,
                "source_paragraph_boundary_mismatch",
                "The exact known member did not provide the verified source paragraph boundary.");
        }

        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs))
        {
            return new(
                text,
                "paragraph_boundary_target_not_located",
                "The parser-selected Docs block did not match the untouched owner.");
        }

        var expectedRemarks = new XElement(
            "remarks",
            new XElement("para", LegacyControlTemplateParagraph),
            ImporterSourceReference(sourceDocs),
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        if (actualDocs.Elements("remarks").ToList() is not [XElement remarks] ||
            !ImporterMarkupEquals(remarks, expectedRemarks))
        {
            return new(
                text,
                "existing_paragraph_boundary_not_importer_owned",
                "The complete old remarks, exact source reference, and attribution did not match the known importer output.");
        }

        var summary = actualDocs.Element("summary")!;
        if (!TryGetElementSpan(blockText, summary, out var summarySpan) ||
            !TryGetElementSpan(blockText, remarks, out var remarksSpan))
        {
            return new(
                text,
                "paragraph_boundary_target_not_located",
                "The verified summary and remarks could not be located without scanning authored XML.");
        }

        var remarksIndent = file.IndentAt(block.Start) + "  ";
        var edits = new[]
        {
            new XmlSpanEdit(summarySpan, $"<summary>{XmlEscape(sourceDocs.Summary)}</summary>"),
            new XmlSpanEdit(
                remarksSpan,
                RenderImporterOwnedRemarks(
                    sourceDocs.Paragraphs,
                    sourceDocs,
                    file.Newline,
                    remarksIndent,
                    remarksIndent + "  ")),
        };
        foreach (var edit in edits.OrderByDescending(edit => edit.Span.Start))
            blockText = blockText[..edit.Span.Start] + edit.Replacement + blockText[edit.Span.End..];
        return new(text[..block.Start] + blockText + text[block.End..], null, null);
    }

    static List<string> KnownAndroidTextRepairCandidateTargets(DocsOwner owner) =>
        KnownAndroidTextRepairs
            .Where(repair => repair.MemberId == owner.Id)
            .Where(repair => owner.Docs.Elements(repair.Target.Split(':')[0]).Any(channel =>
                (!repair.Target.StartsWith("param:", StringComparison.Ordinal) ||
                 (string?)channel.Attribute("name") == repair.Target["param:".Length..]) &&
                (channel.Value == repair.IncorrectText ||
                 channel.Elements("para").Any(paragraph => paragraph.Value == repair.IncorrectText))))
            .Select(repair => repair.Target).ToList();

    static AndroidTextRepairResult RepairKnownAndroidText(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs sourceDocs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs) ||
            !XNode.DeepEquals(actualDocs, owner.Docs))
            return new AndroidTextRepairResult(text, []);

        var targets = FindKnownAndroidTextRepairTargets(owner.Id, actualDocs, sourceDocs);
        var replacements = new List<(XmlSpan Span, string Text)>();
        foreach (var target in targets)
        {
            if (!TryGetElementSpan(blockText, target.Element, out var span))
                return new AndroidTextRepairResult(text, []);
            var replacement = new XElement(
                target.Element.Name, target.Element.Attributes(), target.Repair.CorrectText);
            replacements.Add((span, replacement.ToString(SaveOptions.DisableFormatting)));
        }
        foreach (var replacement in replacements.OrderByDescending(item => item.Span.Start))
            blockText = blockText[..replacement.Span.Start] + replacement.Text + blockText[replacement.Span.End..];
        return new AndroidTextRepairResult(
            text[..block.Start] + blockText + text[block.End..],
            targets.Select(target => target.Repair.Target).ToList());
    }

    static List<AndroidTextRepairTarget> FindKnownAndroidTextRepairTargets(
        string memberId,
        XElement docs,
        SourceDocs sourceDocs)
    {
        var targets = new List<AndroidTextRepairTarget>();
        if (sourceDocs.SourceKind != "android" || !HasExactImporterSourceReference(docs, sourceDocs))
            return targets;

        var repairs = KnownAndroidTextRepairs.Where(repair =>
            repair.MemberId == memberId && repair.SourceUrl == sourceDocs.SourceUrl).ToList();
        foreach (var repair in repairs)
        {
            var parts = repair.Target.Split(':');
            if (docs.Elements(parts[0]).Where(channel =>
                    parts.Length == 1 || (string?)channel.Attribute("name") == parts[1])
                    .ToList() is not [XElement channel])
                continue;

            var isEnumSummary = repair.Target == "summary" &&
                memberId.StartsWith("F:", StringComparison.Ordinal);
            var containers = isEnumSummary ? [channel] : docs.Elements("remarks").ToList();
            if (containers is not [XElement originalContainer])
                continue;
            var container = isEnumSummary ? new XElement(originalContainer) : originalContainer;
            if (isEnumSummary)
                container.Name = "remarks";
            if (!IsPotentialImporterOwnedRemarks(container, "android"))
                continue;
            var elements = container.Elements().ToList();
            if (!ImporterMarkupEquals(elements[^2], ImporterSourceReference(sourceDocs)))
                continue;

            var expected = UsableRemarks(sourceDocs.Paragraphs).Select(DocumentationElement).ToList();
            var existing = elements.SkipLast(2).ToList();
            if (existing.Count != expected.Count ||
                !existing.Zip(expected, (actual, mapped) =>
                {
                    var correction = repairs.FirstOrDefault(candidate =>
                        !candidate.Target.StartsWith("param:", StringComparison.Ordinal) &&
                        actual.Name == "para" && actual.Value == candidate.IncorrectText);
                    return ImporterMarkupEquals(
                        correction is null ? actual : new XElement("para", correction.CorrectText),
                        mapped);
                }).All(equal => equal))
                continue;

            var element = repair.Target == "remarks" || isEnumSummary
                ? channel.Elements("para").FirstOrDefault() : channel;
            if (element is null ||
                (parts.Length == 2 ? element.Attributes().Count() != 1 : element.HasAttributes) ||
                !HasPlainTextContent(element, out var original) || original != repair.IncorrectText)
                continue;

            var mappedText = parts.Length == 2
                ? sourceDocs.Parameters.TryGetValue(parts[1], out var parameter) ? RemoveLeadingJavaType(parameter) : ""
                : repair.Target == "summary" && !isEnumSummary
                    ? sourceDocs.Summary
                    : sourceDocs.Paragraphs.FirstOrDefault()?.Text;
            if (mappedText == repair.CorrectText)
                targets.Add(new AndroidTextRepairTarget(repair, element));
        }
        return targets;
    }

    static bool HasExactKnownBooleanReturnMarkup(
        XElement returns,
        KnownBooleanReturnRepair repair)
    {
        var expected = XElement.Parse(
            $"<returns>{repair.IncorrectMarkup}</returns>",
            LoadOptions.PreserveWhitespace);
        return NormalizeText(returns.Value).Equals(
                repair.IncorrectReturn,
                StringComparison.Ordinal) &&
            ImporterMarkupEquals(returns, expected);
    }

    static bool IsManagedBooleanReturn(XElement? member) =>
        member?.Element("ReturnValue")?.Element("ReturnType")?.Value.Equals(
            "System.Boolean",
            StringComparison.Ordinal) == true;

    static bool HasCopiedDescriptionSummaryRepairCandidate(XElement docs) =>
        HasImporterSourceReference(docs) &&
        IsImporterCopiedDescriptionElement(docs.Element("summary"));

    static bool HasCopiedDescriptionRemarksRepairCandidate(XElement docs) =>
        HasImporterSourceReference(docs) &&
        docs.Element("remarks")?.Elements("para").Any(
            IsImporterCopiedDescriptionElement) == true;

    static CopiedDescriptionRepairResult RepairCopiedDescriptionLabels(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var actualDocs))
        {
            return CopiedDescriptionRepairResult.Skip(
                text,
                CopiedDescriptionRepairTargets(owner.Docs),
                "copied_description_target_not_located",
                "The copied-description target could not be parser-identified in its current <Docs> block.");
        }

        if (CountImporterSourceReferences(actualDocs, docs.SourceUrl) == 0)
            return new CopiedDescriptionRepairResult(text, [], []);

        var candidates = CopiedDescriptionRepairTargets(actualDocs);
        if (candidates.Count == 0)
            return new CopiedDescriptionRepairResult(text, [], []);

        if (!XNode.DeepEquals(actualDocs, owner.Docs))
        {
            return CopiedDescriptionRepairResult.Skip(
                text,
                candidates,
                "copied_description_target_not_located",
                "The parser-identified copied-description target no longer matches the owner's untouched <Docs> structure.");
        }

        var targets = new List<string>();
        var skips = new List<CopiedDescriptionRepairSkip>();
        var edits = new List<XmlSpanEdit>();
        foreach (var candidate in candidates)
        {
            if (!TryGetElementSpan(blockText, candidate.Element, out var elementSpan))
            {
                skips.Add(new CopiedDescriptionRepairSkip(
                    candidate.Target,
                    "copied_description_target_not_located",
                    "The parser-identified copied-description target could not be located without scanning CDATA, comments, or processing instructions."));
                continue;
            }

            if (candidate.Target == "summary")
            {
                if (ChannelValueOrSkip(
                        docs.Summary,
                        "summary",
                        "source_summary_missing").Text is not string replacement)
                {
                    continue;
                }
                if (!TryGetDirectTextElementContentSpan(blockText, elementSpan, out var contentSpan))
                {
                    skips.Add(new CopiedDescriptionRepairSkip(
                        candidate.Target,
                        "copied_description_target_not_located",
                        "The parser-identified copied-description summary could not be located without scanning CDATA, comments, or processing instructions."));
                    continue;
                }
                edits.Add(new XmlSpanEdit(
                    contentSpan,
                    XmlEscape(replacement)));
                targets.Add(candidate.Target);
            }
            else
            {
                if (!TryGetCopiedDescriptionRemovalSpan(
                        blockText,
                        candidate.Element,
                        elementSpan,
                        out var removalSpan))
                {
                    skips.Add(new CopiedDescriptionRepairSkip(
                        candidate.Target,
                        "copied_description_mixed_content",
                        "Removing the copied-description paragraph could collapse authored content, so it was preserved."));
                    continue;
                }
                edits.Add(new XmlSpanEdit(
                    removalSpan,
                    ""));
                targets.Add(candidate.Target);
            }
        }

        foreach (var edit in edits.OrderByDescending(edit => edit.Span.Start))
        {
            blockText = blockText[..edit.Span.Start] + edit.Replacement +
                blockText[edit.Span.End..];
        }
        return new CopiedDescriptionRepairResult(
            text[..block.Start] + blockText + text[block.End..],
            targets,
            skips);
    }

    static bool TryParseDocsBlock(string blockText, out XElement docs)
    {
        try
        {
            docs = XElement.Parse(
                blockText,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            return docs.Name.LocalName == "Docs";
        }
        catch (XmlException)
        {
            docs = new XElement("Docs");
            return false;
        }
    }

    static List<CopiedDescriptionRepairTarget> CopiedDescriptionRepairTargets(XElement docs)
    {
        var targets = new List<CopiedDescriptionRepairTarget>();
        var summary = docs.Element("summary");
        if (IsImporterCopiedDescriptionElement(summary))
            targets.Add(new CopiedDescriptionRepairTarget("summary", summary!));

        foreach (var paragraph in docs.Element("remarks")?.Elements("para") ??
            Enumerable.Empty<XElement>())
        {
            if (IsImporterCopiedDescriptionElement(paragraph))
                targets.Add(new CopiedDescriptionRepairTarget("remarks", paragraph));
        }
        return targets;
    }

    static bool IsImporterCopiedDescriptionElement(XElement? element)
    {
        if (element is null)
            return false;
        var nodes = element.Nodes().ToList();
        return nodes.Count == 1 &&
            nodes[0] is XText text &&
            text is not XCData &&
            IsImporterCopiedDescriptionLabel(text.Value);
    }

    static bool TryGetDirectTextElementContentSpan(
        string text,
        XmlSpan elementSpan,
        out XmlSpan contentSpan)
    {
        contentSpan = new XmlSpan(0, 0);
        if (!TryFindMarkupEnd(text, elementSpan.Start, out var openingEnd))
            return false;
        var closingStart = text.LastIndexOf('<', elementSpan.End - 1);
        if (closingStart <= openingEnd ||
            !text.AsSpan(closingStart, elementSpan.End - closingStart).StartsWith(
                "</",
                StringComparison.Ordinal))
        {
            return false;
        }
        contentSpan = new XmlSpan(openingEnd, closingStart);
        return true;
    }

    static void ReportCopiedDescriptionRepairSkips(
        ImportReport report,
        LoadedFile file,
        DocsOwner owner,
        string sourceUrl,
        IEnumerable<CopiedDescriptionRepairSkip> skips)
    {
        foreach (var skip in skips)
        {
            report.Entries.Add(ReportEntry.Skipped(
                file.RelativePath,
                owner.Id,
                skip.Target,
                skip.Reason,
                skip.Detail,
                sourceUrl));
        }
    }

    static void ReportBooleanReturnRepairSkip(
        ImportReport report,
        LoadedFile file,
        DocsOwner owner,
        string sourceUrl,
        BooleanReturnRepairSkip skip) =>
        report.Entries.Add(ReportEntry.Skipped(
            file.RelativePath,
            owner.Id,
            "returns",
            skip.Reason,
            skip.Detail,
            sourceUrl));

    static void ReportSourceReferenceCleanupSkip(
        ImportReport report,
        LoadedFile file,
        DocsOwner owner,
        string sourceUrl,
        SourceReferenceCleanupSkip skip) =>
        report.Entries.Add(ReportEntry.Skipped(
            file.RelativePath,
            owner.Id,
            "remarks",
            skip.Reason,
            skip.Detail,
            sourceUrl));

    static bool IsImporterCopiedDescriptionLabel(string? value) =>
        value is not null &&
        Regex.IsMatch(
            NormalizeText(value).Trim(),
            @"^Description copied from (?:class|interface):\s+[A-Za-z_$][\w.$]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static bool TryGetCopiedDescriptionRemovalSpan(
        string text,
        XElement element,
        XmlSpan elementSpan,
        out XmlSpan removalSpan)
    {
        removalSpan = elementSpan;
        if (HasAdjacentNonWhitespaceText(element) ||
            HasUnseparatedVisibleSiblingContent(element))
        {
            return false;
        }

        return TryGetElementRemovalSpan(text, elementSpan, out removalSpan);
    }

    static bool HasUnseparatedVisibleSiblingContent(XElement element)
    {
        var nodes = element.Parent?.Nodes().ToList();
        if (nodes is null)
            return true;

        var elementIndex = nodes.IndexOf(element);
        if (elementIndex < 0)
            return true;

        var before = -1;
        for (var index = elementIndex - 1; index >= 0; index--)
        {
            if (IsVisibleSiblingContent(nodes[index]))
            {
                before = index;
                break;
            }
        }

        var after = -1;
        for (var index = elementIndex + 1; index < nodes.Count; index++)
        {
            if (IsVisibleSiblingContent(nodes[index]))
            {
                after = index;
                break;
            }
        }

        if (before < 0 || after < 0)
            return false;

        return !nodes
            .Skip(before + 1)
            .Take(after - before - 1)
            .Where((_, index) => before + 1 + index != elementIndex)
            .OfType<XText>()
            .Any(text => text.Value.Any(char.IsWhiteSpace));
    }

    static bool IsVisibleSiblingContent(XNode node) =>
        node switch
        {
            XElement => true,
            XCData cdata => cdata.Value.Length > 0,
            XText text => !string.IsNullOrWhiteSpace(text.Value),
            _ => false,
        };

    static bool HasTruncatedImporterSummary(LoadedFile file, DocsOwner owner)
        // A plain-text summary has no durable importer provenance.
        => false;

    static RemarksRefreshResult RefreshIncompleteImporterRemarks(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var ownedRemarks = owner.Docs.Element("remarks");
        if (ownedRemarks is null || !HasPotentialImporterOwnedRemarksRefresh(file, owner))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing remarks did not have the strict importer source-reference structure.");
        }

        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        var document = XElement.Parse(
            blockText,
            LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var remarks = document.Element("remarks");
        if (remarks is null || !XNode.DeepEquals(remarks, ownedRemarks))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The parser-selected remarks no longer matched the structurally verified importer-owned remarks.");
        }
        if (remarks.HasAttributes ||
            remarks.Nodes().Any(node => node switch
            {
                XElement => false,
                XCData => true,
                XText text => !string.IsNullOrWhiteSpace(text.Value),
                _ => true,
            }))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing remarks contained extra nodes, markup, or authored prose outside importer-owned elements.");
        }
        if (!TryGetElementSpan(blockText, remarks, out var remarksSpan))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The structurally verified remarks could not be located without reformatting existing XML.");
        }

        var elements = remarks.Elements().ToList();
        var sourceReferenceIndexes = elements
            .Select((element, index) => (element, index))
            .Where(item => TryGetImporterSourceReferenceUrl(
                item.element.ToString(SaveOptions.DisableFormatting),
                out _))
            .Select(item => item.index)
            .ToList();
        var hasExactHybridMetadata = sourceReferenceIndexes.Count == 1 &&
            sourceReferenceIndexes[0] == elements.Count - 2 &&
            IsImporterAttributionParagraph(elements[^1]);
        var hasExactJavaMetadata = sourceReferenceIndexes.Count == 1 &&
            sourceReferenceIndexes[0] == elements.Count - 1;
        if (sourceReferenceIndexes.Count != 1 ||
            sourceReferenceIndexes[0] == 0 ||
            (!hasExactHybridMetadata && !hasExactJavaMetadata))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing remarks did not have exact importer source-reference metadata and, when present, Android attribution after source prose.");
        }

        var sourceReferenceIndex = sourceReferenceIndexes[0];
        if (!ImporterMarkupEquals(
                elements[sourceReferenceIndex],
                ImporterSourceReference(docs)))
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The existing importer source reference did not exactly match the mapped source member.");
        }

        var sourceFragments = ExpandRemarksFragments(docs.Paragraphs);
        if (sourceFragments.Count == 0)
        {
            return new RemarksRefreshResult(
                text,
                "source_remarks_missing",
                "The exact source member did not provide usable remarks to refresh.");
        }

        var existingSourceElements = elements.Take(sourceReferenceIndex).ToList();
        var represented = new HashSet<int>();
        var lastFragmentIndex = -1;
        var elementFragments = new List<List<int>>();
        var firstRetainedMetadataIndex = sourceReferenceIndex;
        var hasRetainedAnnotation = false;
        foreach (var element in existingSourceElements)
        {
            var matches = MatchingExactSourceFragmentSequences(element, sourceFragments);
            if (IsRetainedRemarksParagraph(element) && matches.Count == 0)
            {
                if (hasRetainedAnnotation)
                {
                    return new RemarksRefreshResult(
                        text,
                        "existing_remarks_not_importer_owned",
                        "Existing remarks contained more than one retained importer-supported annotation.");
                }
                hasRetainedAnnotation = true;
                firstRetainedMetadataIndex = elementFragments.Count;
                elementFragments.Add([]);
                continue;
            }

            if (!IsImporterRenderedSourceParagraph(element))
            {
                return new RemarksRefreshResult(
                    text,
                    "existing_remarks_not_importer_owned",
                    "Existing remarks source content contained non-text nodes, markup, or an unsupported element.");
            }

            if (hasRetainedAnnotation)
            {
                return new RemarksRefreshResult(
                    text,
                    "existing_remarks_not_importer_owned",
                    "A retained importer-supported annotation must follow all source prose.");
            }

            if (matches.Count != 1)
            {
                return new RemarksRefreshResult(
                    text,
                    "existing_remarks_not_importer_owned",
                    "Each existing remarks paragraph must exactly match one unambiguous consecutive sequence of source fragments.");
            }
            var fragments = matches[0];
            if (fragments[0] <= lastFragmentIndex ||
                fragments.Zip(fragments.Skip(1), (left, right) => right == left + 1)
                    .Any(isConsecutive => !isConsecutive))
            {
                return new RemarksRefreshResult(
                    text,
                    "existing_remarks_not_importer_owned",
                    "Existing remarks prose was not an ordered contiguous subset of the exact mapped source.");
            }
            represented.UnionWith(fragments);
            lastFragmentIndex = fragments[^1];
            elementFragments.Add(fragments);
        }

        var missing = Enumerable.Range(0, sourceFragments.Count)
            .Where(index => !represented.Contains(index))
            .ToList();
        if (missing.Count == 0)
            return new RemarksRefreshResult(text, null, null);

        var childSpans = new List<XmlSpan>();
        foreach (var element in elements)
        {
            if (!TryGetElementSpan(blockText, element, out var childSpan) ||
                childSpan.Start < remarksSpan.Start ||
                childSpan.End > remarksSpan.End)
            {
                return new RemarksRefreshResult(
                    text,
                    "existing_remarks_not_importer_owned",
                    "The structurally verified remarks children could not be located without reformatting existing XML.");
            }
            childSpans.Add(childSpan);
        }
        if (childSpans.Count != elements.Count)
        {
            return new RemarksRefreshResult(
                text,
                "existing_remarks_not_importer_owned",
                "The structurally verified remarks children could not be located without reformatting existing XML.");
        }

        var additions = new SortedDictionary<int, List<SourceParagraph>>();
        foreach (var missingIndex in missing)
        {
            var insertionIndex = elementFragments
                .Select((fragments, index) => (fragments, index))
                .FirstOrDefault(item => item.fragments.Count > 0 &&
                    item.fragments[0] > missingIndex);
            var targetIndex = insertionIndex.fragments is null
                ? firstRetainedMetadataIndex
                : insertionIndex.index;
            if (!additions.TryGetValue(targetIndex, out var paragraphs))
                additions.Add(targetIndex, paragraphs = []);
            paragraphs.Add(sourceFragments[missingIndex]);
        }

        var newline = file.Newline;
        foreach (var (insertionIndex, paragraphs) in additions.Reverse())
        {
            var childIndex = childSpans[insertionIndex].Start;
            if (TryGetLineWhitespaceIndent(
                    blockText,
                    childIndex,
                    out var lineStart,
                    out var indent))
            {
                var insertion = string.Join(
                    newline,
                    paragraphs.Select(paragraph => RenderDocumentationParagraph(paragraph, indent))) +
                    newline;
                blockText = blockText[..lineStart] + insertion + blockText[lineStart..];
            }
            else
            {
                var insertion = string.Concat(
                    paragraphs.Select(paragraph => RenderDocumentationParagraph(paragraph, "")));
                blockText = blockText[..childIndex] + insertion + blockText[childIndex..];
            }
        }

        return new RemarksRefreshResult(
            text[..block.Start] + blockText + text[block.End..],
            null,
            null);
    }

    static bool TryGetLineWhitespaceIndent(
        string text,
        int offset,
        out int lineStart,
        out string indent)
    {
        lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        indent = text[lineStart..offset];
        return indent.All(char.IsWhiteSpace);
    }

    static bool TryGetElementSpan(string text, XElement element, out XmlSpan span)
    {
        span = new XmlSpan(0, 0);
        if (element is not IXmlLineInfo lineInfo ||
            !lineInfo.HasLineInfo() ||
            !TryGetTextOffset(text, lineInfo.LineNumber, lineInfo.LinePosition - 1, out var start))
        {
            return false;
        }

        if (!TryFindMarkupEnd(text, start, out var openingEnd))
            return false;
        if (text[start..openingEnd].AsSpan().TrimEnd().EndsWith("/>", StringComparison.Ordinal))
        {
            span = new XmlSpan(start, openingEnd);
            return true;
        }

        var depth = 0;
        for (var index = start; index < text.Length;)
        {
            var tagStart = text.IndexOf('<', index);
            if (tagStart < 0)
                return false;
            if (text.AsSpan(tagStart).StartsWith("<!--", StringComparison.Ordinal))
            {
                var commentEnd = text.IndexOf("-->", tagStart + 4, StringComparison.Ordinal);
                if (commentEnd < 0)
                    return false;
                index = commentEnd + 3;
                continue;
            }
            if (text.AsSpan(tagStart).StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                var cdataEnd = text.IndexOf("]]>", tagStart + 9, StringComparison.Ordinal);
                if (cdataEnd < 0)
                    return false;
                index = cdataEnd + 3;
                continue;
            }
            if (text.AsSpan(tagStart).StartsWith("<?", StringComparison.Ordinal))
            {
                var instructionEnd = text.IndexOf("?>", tagStart + 2, StringComparison.Ordinal);
                if (instructionEnd < 0)
                    return false;
                index = instructionEnd + 2;
                continue;
            }
            if (!TryFindMarkupEnd(text, tagStart, out var tagEnd))
                return false;

            var tag = text[(tagStart + 1)..(tagEnd - 1)].TrimStart();
            if (tag.StartsWith('!'))
            {
                index = tagEnd;
                continue;
            }
            if (tag.StartsWith('/'))
            {
                depth--;
                if (depth == 0)
                {
                    span = new XmlSpan(start, tagEnd);
                    return true;
                }
            }
            else if (!tag.TrimEnd().EndsWith("/", StringComparison.Ordinal))
            {
                depth++;
            }
            index = tagEnd;
        }
        return false;
    }

    static bool TryGetTextOffset(
        string text,
        int lineNumber,
        int linePosition,
        out int offset)
    {
        offset = 0;
        if (lineNumber < 1 || linePosition < 1)
            return false;

        for (var line = 1; line < lineNumber; line++)
        {
            var newline = text.IndexOf('\n', offset);
            if (newline < 0)
                return false;
            offset = newline + 1;
        }
        offset += linePosition - 1;
        return offset < text.Length;
    }

    static bool TryFindMarkupEnd(string text, int start, out int end)
    {
        if (start >= text.Length || text[start] != '<')
        {
            end = 0;
            return false;
        }
        var quote = '\0';
        for (var index = start + 1; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                continue;
            }
            if (character is '"' or '\'')
            {
                quote = character;
                continue;
            }
            if (character == '>')
            {
                end = index + 1;
                return true;
            }
        }
        end = 0;
        return false;
    }

    static bool TryGetClosingElementStart(
        string text,
        XmlSpan elementSpan,
        out int closingStart)
    {
        closingStart = text.LastIndexOf('<', elementSpan.End - 1);
        return closingStart >= elementSpan.Start &&
            text.AsSpan(closingStart, elementSpan.End - closingStart).StartsWith(
                "</",
                StringComparison.Ordinal);
    }

    static List<SourceParagraph> ExpandRemarksFragments(
        IEnumerable<SourceParagraph> paragraphs)
    {
        var fragments = new List<SourceParagraph>();
        foreach (var paragraph in RemarksReplacementOrSkip(
                     paragraphs,
                     "source_remarks_missing").Remarks ?? [])
        {
            if (paragraph.IsCode)
                fragments.Add(paragraph);
            else
                fragments.AddRange(SplitSourceSentences(paragraph.Text)
                    .Select(sentence => new SourceParagraph(sentence, IsCode: false)));
        }
        return fragments;
    }

    static List<int> MatchingSourceFragmentIndexes(
        XElement element,
        IReadOnlyList<SourceParagraph> sourceFragments)
    {
        if (element.Name.LocalName == "code" &&
            (string?)element.Attribute("lang") == "text/java" &&
            HasPlainTextContent(element, out var codeText))
        {
            var code = NormalizeText(codeText);
            return sourceFragments
                .Select((fragment, index) => (fragment, index))
                .Where(item => item.fragment.IsCode &&
                    NormalizeText(item.fragment.Text).Equals(code, StringComparison.Ordinal))
                .Select(item => item.index)
                .ToList();
        }

        if (element.Name.LocalName != "para" ||
            element.HasAttributes ||
            !HasPlainTextOrInlineCodeContent(element))
            return [];

        var prose = NormalizeSourceProseForComparison(element.Value);
        return sourceFragments
            .Select((fragment, index) => (fragment, index))
            .Where(item => !item.fragment.IsCode &&
                prose.Contains(
                    NormalizeSourceProseForComparison(item.fragment.Text),
                    StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .ToList();
    }

    static List<List<int>> MatchingExactSourceFragmentSequences(
        XElement element,
        IReadOnlyList<SourceParagraph> sourceFragments)
    {
        if (element.Name.LocalName == "code" &&
            (string?)element.Attribute("lang") == "text/java" &&
            HasPlainTextContent(element, out var codeText))
        {
            var code = NormalizeNormalWhitespace(codeText);
            return sourceFragments
                .Select((fragment, index) => (fragment, index))
                .Where(item => item.fragment.IsCode &&
                    NormalizeNormalWhitespace(item.fragment.Text).Equals(
                        code,
                        StringComparison.Ordinal))
                .Select(item => new List<int> { item.index })
                .ToList();
        }

        if (element.Name.LocalName != "para" ||
            element.HasAttributes ||
            !HasPlainTextContent(element, out var proseText))
        {
            return [];
        }

        var prose = NormalizeNormalWhitespace(proseText);
        var matches = new List<List<int>>();
        for (var start = 0; start < sourceFragments.Count; start++)
        {
            if (sourceFragments[start].IsCode)
                continue;

            var combined = "";
            for (var end = start; end < sourceFragments.Count; end++)
            {
                var fragment = sourceFragments[end];
                if (fragment.IsCode)
                    break;

                combined = combined.Length == 0
                    ? NormalizeNormalWhitespace(fragment.Text)
                    : $"{combined} {NormalizeNormalWhitespace(fragment.Text)}";
                if (combined.Equals(prose, StringComparison.Ordinal))
                    matches.Add(Enumerable.Range(start, end - start + 1).ToList());
            }
        }
        return matches;
    }

    static bool IsRetainedRemarksParagraph(XElement element) =>
        element.Name.LocalName == "para" &&
        !element.HasAttributes &&
        HasPlainTextContent(element, out var text) &&
        Regex.IsMatch(
            NormalizeRemarksText(text),
            @"^Added in \d+(?:\.\d+)*\.$",
            RegexOptions.CultureInvariant);

    static bool HasIncompleteCodeExampleRemarks(LoadedFile file, DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = file.Text[block.Start..block.End];
        return HasIncompleteImporterJavaExample(blockText);
    }

    static bool HasIncompleteImporterJavaExample(string blockText)
    {
        if (!TryParseDocsBlock(blockText, out var docs) ||
            docs.Element("remarks") is not XElement remarks ||
            remarks.HasAttributes ||
            remarks.Nodes().Any(node => node switch
            {
                XElement => false,
                XCData => true,
                XText text => !string.IsNullOrWhiteSpace(text.Value),
                _ => true,
            }))
        {
            return false;
        }

        var elements = remarks.Elements().ToList();
        if (elements.Count is not (2 or 3) ||
            elements[0].Name != "code" ||
            !string.Equals((string?)elements[0].Attribute("lang"), "text/java", StringComparison.Ordinal) ||
            elements[0].Attributes().Count() != 1 ||
            !elements[0].Nodes().All(node => node is XText && node is not XCData) ||
            !Regex.IsMatch(
                elements[0].Value,
                @"^\s*(?:public|protected|private)\s+(?:(?:static|final|abstract|synchronized|native)\s+)*(?:[\w.$<>\[\]?]+\s+)?\w+\s*\([^<]*\)\s*(?:throws\s+[^<;]+)?;?\s*$",
                RegexOptions.Singleline | RegexOptions.CultureInvariant) ||
            !IsCanonicalImporterSourceReferenceParagraph(elements[1]) ||
            (elements.Count == 3 && !IsImporterOwnedEnumAttributionParagraph(elements[2])))
        {
            return false;
        }

        return true;
    }

    static bool IsCanonicalImporterSourceReferenceParagraph(XElement paragraph)
    {
        if (paragraph.Name != "para" || paragraph.HasAttributes ||
            paragraph.Nodes().Count() != 1 ||
            paragraph.Nodes().Single() is not XElement format ||
            format.Name != "format" ||
            format.Attributes().Count() != 1 ||
            (string?)format.Attribute("type") != "text/html" ||
            format.Nodes().Count() != 1 ||
            format.Nodes().Single() is not XElement anchor ||
            anchor.Name != "a" ||
            anchor.Attributes().Count() != 2 ||
            (string?)anchor.Attribute("title") != "Reference documentation" ||
            !IsCanonicalImporterSourceReferenceUrl(
                (string?)anchor.Attribute("href")))
        {
            return false;
        }

        var nodes = anchor.Nodes().ToList();
        return nodes.Count == 3 &&
            nodes[0] is XText leading &&
            (leading.Value == "Android reference for " ||
             leading.Value == "Java reference for ") &&
            nodes[1] is XElement code &&
            code.Name == "code" &&
            !code.HasAttributes &&
            code.Nodes().Count() == 1 &&
            code.Nodes().Single() is XText label &&
            label.Value.Length > 0 &&
            nodes[2] is XText trailing &&
            trailing.Value == ".";
    }

    static bool IsCanonicalImporterSourceReferenceUrl(string? url) =>
        url?.StartsWith(
            "https://developer.android.com/reference/",
            StringComparison.Ordinal) == true ||
        url?.StartsWith(
            JavaReference,
            StringComparison.Ordinal) == true;

    static bool IsExactCanonicalImporterSourceReferenceParagraph(
        XElement paragraph,
        SourceDocs docs)
    {
        if (!IsCanonicalImporterSourceReferenceParagraph(paragraph))
        {
            return false;
        }

        var expected = XElement.Parse(
            $"<para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(docs.SourceUrl)}\" " +
            $"title=\"Reference documentation\">{(docs.SourceKind == "android" ? "Android" : "Java")} reference for <code>{XmlEscape(docs.SourceLabel)}</code>." +
            "</a></format></para>");
        return XNode.DeepEquals(paragraph, expected);
    }

    static bool IsImporterSourceReferenceParagraph(XElement paragraph) =>
        IsImporterMetadataParagraph(paragraph) ||
        paragraph.Name == "para" &&
        paragraph.Nodes().All(node =>
            node is XText text && string.IsNullOrWhiteSpace(text.Value) ||
            node is XElement) &&
        paragraph.Elements().Count() == 1 &&
        paragraph.Elements().First() is XElement format &&
        format.Name == "format" &&
        format.Elements().Count() == 1 &&
        format.Elements().First() is XElement anchor &&
        anchor.Name == "a" &&
        string.Equals((string?)anchor.Attribute("title"), "Reference documentation",
            StringComparison.Ordinal);

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
        if (!TryParseDocsBlock(blockText, out var docs))
            return false;

        var remarks = docs.Element("remarks");
        return remarks is not null &&
            remarks.Nodes().All(node =>
                node is XText text &&
                text is not XCData &&
                string.IsNullOrWhiteSpace(text.Value) ||
                node is XElement paragraph &&
                IsImporterMetadataParagraph(paragraph));
    }

    static bool IsImporterMetadataParagraph(XElement paragraph) =>
        TryGetImporterSourceReferenceUrl(paragraph, out _) ||
        IsLegacyImporterSourceReference(paragraph) ||
        IsImporterAttributionParagraph(paragraph) ||
        IsLegacyImporterMetadataParagraph(paragraph);

    static bool IsLegacyImporterMetadataParagraph(XElement paragraph) =>
        IsExactImporterAttributionParagraph(paragraph, LegacyAndroidAttribution);

    static bool IsLegacyImporterSourceReference(XElement paragraph)
    {
        if (paragraph.Name != "para" ||
            paragraph.HasAttributes ||
            SignificantNodes(paragraph) is not [var formatNode] ||
            formatNode is not XElement format ||
            format.Name != "format" ||
            !HasExactAttributes(format, ("type", "text/html")) ||
            SignificantNodes(format) is not [var anchorNode] ||
            anchorNode is not XElement anchor ||
            anchor.Name != "a" ||
            !HasExactAttributes(
                anchor,
                ("href", ""),
                ("title", "Reference documentation")) ||
            !HasPlainTextContent(anchor, out var label) ||
            (!NormalizeText(label).Equals("Android reference.", StringComparison.Ordinal) &&
             !NormalizeText(label).Equals("Java reference.", StringComparison.Ordinal)))
        {
            return false;
        }

        return IsOfficialSourceReferenceUrl(
            WebUtility.HtmlDecode((string?)anchor.Attribute("href") ?? ""));
    }

    static bool IsImporterAttributionParagraph(XElement paragraph) =>
        IsExactImporterAttributionParagraph(paragraph, AndroidAttribution);

    static bool IsExactImporterAttributionParagraph(
        XElement paragraph,
        string attribution)
    {
        if (paragraph.Name != "para" || paragraph.HasAttributes)
            return false;

        var expected = XElement.Parse($"<para>{attribution}</para>");
        return NormalizeText(paragraph.Value).Equals(
                NormalizeText(expected.Value),
                StringComparison.Ordinal) &&
            ImporterMarkupEquals(paragraph, expected);
    }

    static bool IsLegacyImporterAttributionLookalike(XElement paragraph) =>
        paragraph.Name == "para" &&
        !paragraph.HasAttributes &&
        NormalizeText(paragraph.Value).StartsWith(
            "Portions of this page are modifications based on work created and shared by",
            StringComparison.Ordinal) &&
        paragraph.Descendants("a").Any(link =>
            UrlsEqual(
                (string?)link.Attribute("href") ?? "",
                "https://developers.google.com/terms/site-policies"));

    static bool IsOfficialSourceReferenceUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var source) &&
        (source.AbsoluteUri.StartsWith(
            AndroidReference,
            StringComparison.Ordinal) ||
         source.AbsoluteUri.StartsWith(
            JavaReference,
            StringComparison.Ordinal));

    static string ReplaceIncompleteCodeExampleRemarks(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        if (!HasIncompleteImporterJavaExample(blockText) ||
            !TryParseDocsBlock(blockText, out var parsedDocs) ||
            parsedDocs.Element("remarks") is not XElement remarks ||
            !IsExactCanonicalImporterSourceReferenceParagraph(
                remarks.Elements().ElementAt(1),
                docs) ||
            !TryGetElementSpan(blockText, remarks, out var remarksSpan))
        {
            return text;
        }

        var newline = file.Newline;
        var docsIndent = file.IndentAt(block.Start);
        var remarksIndent = docsIndent + "  ";
        var paragraphIndent = remarksIndent + "  ";
        var replacement = RenderImporterOwnedRemarks(
            docs.Paragraphs,
            docs,
            newline,
            remarksIndent,
            paragraphIndent);
        var updatedBlock = blockText[..remarksSpan.Start] + replacement +
            blockText[remarksSpan.End..];
        return text[..block.Start] + updatedBlock + text[block.End..];
    }

    static string RemoveAugmentedRemarksPlaceholder(string blockText)
    {
        if (!TryParseDocsBlock(blockText, out var docs) ||
            docs.Element("remarks") is not XElement remarks ||
            !IsAugmentedRemarksPlaceholder(remarks) ||
            !TryGetElementSpan(blockText, remarks, out var remarksSpan) ||
            !TryFindMarkupEnd(blockText, remarksSpan.Start, out var openingEnd))
        {
            return blockText;
        }

        var firstParagraph = remarks.Elements("para").First();
        if (!TryGetElementSpan(blockText, firstParagraph, out var paragraphSpan))
            return blockText;
        var directText = blockText[openingEnd..paragraphSpan.Start];
        var placeholder = Regex.Match(
            directText,
            @"To be added\.?",
            RegexOptions.CultureInvariant);
        return placeholder.Success &&
            NormalizeText(directText).Equals(
                NormalizeText(placeholder.Value),
                StringComparison.Ordinal)
            ? blockText[..(openingEnd + placeholder.Index)] +
              blockText[(openingEnd + placeholder.Index + placeholder.Length)..]
            : blockText;
    }

    static string RemoveStandaloneRemarksPlaceholder(string blockText)
    {
        if (!TryParseDocsBlock(blockText, out var docs) ||
            docs.Element("remarks") is not XElement remarks ||
            remarks.IsEmpty ||
            remarks.Nodes().Any(node =>
                node is not XText text ||
                text is XCData ||
                !string.IsNullOrWhiteSpace(text.Value) &&
                !NormalizeText(text.Value).Equals(
                    "To be added.",
                    StringComparison.Ordinal)) ||
            !NormalizeText(string.Concat(
                remarks.Nodes().OfType<XText>().Select(text => text.Value))).Equals(
                    "To be added.",
                    StringComparison.Ordinal) ||
            !TryGetElementSpan(blockText, remarks, out var remarksSpan) ||
            !TryFindMarkupEnd(blockText, remarksSpan.Start, out var openingEnd))
        {
            return blockText;
        }

        var openingTag = blockText[remarksSpan.Start..openingEnd];
        var attributes = openingTag["<remarks".Length..^1].TrimEnd();
        return blockText[..remarksSpan.Start] + $"<remarks{attributes} />" +
            blockText[remarksSpan.End..];
    }

    static bool IsAugmentedRemarksPlaceholder(XElement? remarks)
    {
        if (remarks is null)
            return false;
        var nodes = remarks.Nodes().ToList();
        var paragraphIndex = nodes.FindIndex(node => node is XElement element &&
            element.Name == "para");
        return paragraphIndex > 0 &&
            nodes.Take(paragraphIndex).All(node =>
                node is XText text &&
                text is not XCData) &&
            NormalizeText(string.Concat(
                nodes.Take(paragraphIndex).OfType<XText>().Select(text => text.Value)))
                .Equals("To be added.", StringComparison.Ordinal);
    }

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
        if (!TryParseDocsBlock(blockText, out var parsedDocs) ||
            parsedDocs.Element("summary") is not XElement summary ||
            !HasPlainTextContent(summary, out var summaryText) ||
            !TryGetElementSpan(blockText, summary, out var summarySpan) ||
            !TryGetDirectTextElementContentSpan(
                blockText,
                summarySpan,
                out var summaryContentSpan))
        {
            return text;
        }

        var existingSummary = NormalizeText(summaryText);
        var sourceSummary = NormalizeText(docs.Summary);
        if (sourceSummary.Length <= existingSummary.Length ||
            !sourceSummary.StartsWith(existingSummary, StringComparison.Ordinal))
        {
            return text;
        }

        var updatedBlock = blockText[..summaryContentSpan.Start] +
            XmlEscape(docs.Summary) + blockText[summaryContentSpan.End..];
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
        return HasImporterSourceReference(file.Text[block.Start..block.End]);
    }

    static bool HasImporterSourceReference(XElement docs) =>
        docs.Descendants("para").Any(paragraph =>
            TryGetImporterSourceReferenceUrl(paragraph, out _));

    static bool HasImporterSourceReference(string blockText) =>
        TryParseXmlElement(blockText, out var element) &&
        HasImporterSourceReference(element);

    static SourceReferenceCleanupResult RemoveStaleSourceLinks(
        string blockText,
        string sourceUrl,
        bool removeAll)
    {
        var expectedMember = SourceAnchorMember(sourceUrl);
        return RemoveImporterSourceReferenceParagraphs(
            blockText,
            references => references.Where(reference =>
                !UrlsEqual(reference.Url, sourceUrl) &&
                (removeAll ||
                 SourceAnchorMember(reference.Url).Equals(
                     expectedMember,
                     StringComparison.Ordinal))));
    }

    static SourceReferenceCleanupResult RemoveMatchingSourceLinks(
        string blockText,
        string sourceUrl) =>
        RemoveImporterSourceReferenceParagraphs(
            blockText,
            references => references.Where(reference =>
                UrlsEqual(reference.Url, sourceUrl)));

    static SourceReferenceCleanupResult RemoveDuplicateSourceLinks(
        string blockText,
        string sourceUrl) =>
        RemoveImporterSourceReferenceParagraphs(
            blockText,
            references => references
                .Where(reference => UrlsEqual(reference.Url, sourceUrl))
                .Skip(1));

    static SourceReferenceCleanupResult RemoveImporterRemarksMetadata(
        string blockText)
    {
        if (!TryParseDocsBlock(blockText, out var docs))
        {
            return SourceReferenceCleanupResult.Failure(
                blockText,
                SourceReferenceCleanupSkip.NotLocated(
                    "The current <Docs> block could not be parsed before removing importer metadata."));
        }
        var remarks = docs.Element("remarks");
        var paragraphs = remarks?.Elements("para").ToList() ?? [];
        if (HasUnownedLegacyAttribution(paragraphs))
        {
            return SourceReferenceCleanupResult.Failure(
                blockText,
                SourceReferenceCleanupSkip.UnownedLegacyAttribution(
                    "An attribution-like paragraph included additional text or markup, so it was preserved."));
        }
        return RemoveXmlElements(
            blockText,
            paragraphs.Where(IsImporterMetadataParagraph));
    }

    static SourceReferenceCleanupSkip? FindUnownedLegacyAttribution(
        LoadedFile file,
        DocsOwner owner)
    {
        var block = file.DocsBlocks[owner.Order];
        return TryParseDocsBlock(
                file.Text[block.Start..block.End],
                out var docs) &&
            HasUnownedLegacyAttribution(
                docs.Element("remarks")?.Elements("para") ??
                Enumerable.Empty<XElement>())
            ? SourceReferenceCleanupSkip.UnownedLegacyAttribution(
                "An attribution-like paragraph included additional text or markup, so it was preserved.")
            : null;
    }

    static bool HasUnownedLegacyAttribution(IEnumerable<XElement> paragraphs) =>
        paragraphs.Any(paragraph =>
            IsLegacyImporterAttributionLookalike(paragraph) &&
            !IsImporterAttributionParagraph(paragraph) &&
            !IsLegacyImporterMetadataParagraph(paragraph));

    static int CountImporterSourceReferences(string blockText, string sourceUrl) =>
        TryParseXmlElement(blockText, out var element)
            ? CountImporterSourceReferences(element, sourceUrl)
            : 0;

    static int CountImporterSourceReferences(XElement docs, string sourceUrl) =>
        docs.Descendants("para").Count(paragraph =>
            TryGetImporterSourceReferenceUrl(paragraph, out var importerSourceUrl) &&
            UrlsEqual(importerSourceUrl, sourceUrl));

    static bool TryGetImporterSourceReferenceUrl(string paragraph, out string sourceUrl)
    {
        sourceUrl = "";
        return TryParseXmlElement(paragraph, out var element) &&
            TryGetImporterSourceReferenceUrl(element, out sourceUrl);
    }

    static bool TryGetImporterSourceReferenceUrl(
        XElement paragraph,
        out string sourceUrl)
    {
        sourceUrl = "";
        if (paragraph.Name != "para" ||
            paragraph.HasAttributes ||
            SignificantNodes(paragraph) is not [var formatNode] ||
            formatNode is not XElement format ||
            format.Name != "format" ||
            !HasExactAttributes(format, ("type", "text/html")) ||
            SignificantNodes(format) is not [var anchorNode] ||
            anchorNode is not XElement anchor ||
            anchor.Name != "a" ||
            !HasExactAttributes(
                anchor,
                ("href", ""),
                ("title", "Reference documentation")))
        {
            return false;
        }

        var href = (string?)anchor.Attribute("href");
        if (string.IsNullOrWhiteSpace(href) ||
            SignificantNodes(anchor) is not [var prefixNode, var codeNode, var suffixNode] ||
            prefixNode is not XText prefix ||
            prefix is XCData ||
            codeNode is not XElement code ||
            code.Name != "code" ||
            code.HasAttributes ||
            !HasPlainTextContent(code, out var sourceLabel) ||
            string.IsNullOrWhiteSpace(sourceLabel) ||
            suffixNode is not XText suffix ||
            suffix is XCData ||
            !NormalizeText(prefix.Value).Equals(
                "Android reference for",
                StringComparison.Ordinal) &&
            !NormalizeText(prefix.Value).Equals(
                "Java reference for",
                StringComparison.Ordinal) ||
            !NormalizeText(suffix.Value).Equals(".", StringComparison.Ordinal))
        {
            return false;
        }

        sourceUrl = WebUtility.HtmlDecode(href);
        return IsOfficialSourceReferenceUrl(sourceUrl);
    }

    static SourceReferenceCleanupResult RemoveImporterSourceReferenceParagraphs(
        string blockText,
        Func<IReadOnlyList<ImporterSourceReferenceElement>, IEnumerable<ImporterSourceReferenceElement>>
            select)
    {
        if (!TryParseDocsBlock(blockText, out var docs))
        {
            return SourceReferenceCleanupResult.Failure(
                blockText,
                SourceReferenceCleanupSkip.NotLocated(
                    "The current <Docs> block could not be parsed before reconciling importer source references."));
        }
        var references = docs
            .Descendants("para")
            .Select(paragraph => TryGetImporterSourceReferenceUrl(
                paragraph,
                out var sourceUrl)
                ? new ImporterSourceReferenceElement(paragraph, sourceUrl)
                : null)
            .Where(reference => reference is not null)
            .Cast<ImporterSourceReferenceElement>()
            .ToList();
        return RemoveXmlElements(blockText, select(references));
    }

    static SourceReferenceCleanupResult RemoveXmlElements(
        string blockText,
        IEnumerable<XElement> elements) =>
        RemoveXmlElements(
            blockText,
            elements.Select(element => new ImporterSourceReferenceElement(element, "")));

    static SourceReferenceCleanupResult RemoveXmlElements(
        string blockText,
        IEnumerable<ImporterSourceReferenceElement> elements)
    {
        var removals = new List<XmlSpan>();
        foreach (var element in elements.Select(reference => reference.Element))
        {
            if (HasAdjacentNonWhitespaceText(element) ||
                HasUnseparatedVisibleSiblingContent(element))
            {
                return SourceReferenceCleanupResult.Failure(
                    blockText,
                    SourceReferenceCleanupSkip.MixedContent(
                        "The importer metadata paragraph is surrounded by authored mixed content, so it was preserved."));
            }
            if (!TryGetElementSpan(blockText, element, out var elementSpan) ||
                !TryGetElementRemovalSpan(blockText, elementSpan, out var removalSpan))
            {
                return SourceReferenceCleanupResult.Failure(
                    blockText,
                    SourceReferenceCleanupSkip.NotLocated(
                        "The parser-identified importer metadata paragraph could not be removed without scanning CDATA, comments, or processing instructions."));
            }
            removals.Add(removalSpan);
        }

        foreach (var span in removals.OrderByDescending(span => span.Start))
        {
            blockText = blockText[..span.Start] + blockText[span.End..];
        }
        return SourceReferenceCleanupResult.Success(blockText, removals.Count);
    }

    static bool HasAdjacentNonWhitespaceText(XElement element)
    {
        var nodes = element.Parent?.Nodes().ToList();
        if (nodes is null)
            return true;
        var index = nodes.IndexOf(element);
        if (index < 0)
            return true;
        var hasMeaningfulTextBefore = index > 0 &&
            nodes[index - 1] is XText before &&
            (before is XCData || !string.IsNullOrWhiteSpace(before.Value));
        var hasMeaningfulTextAfter = index + 1 < nodes.Count &&
            nodes[index + 1] is XText after &&
            (after is XCData || !string.IsNullOrWhiteSpace(after.Value));
        return hasMeaningfulTextBefore || hasMeaningfulTextAfter;
    }

    static bool TryGetElementRemovalSpan(
        string text,
        XmlSpan elementSpan,
        out XmlSpan removalSpan)
    {
        removalSpan = elementSpan;
        var lineStart = text.LastIndexOf('\n', Math.Max(0, elementSpan.Start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        if (!text[lineStart..elementSpan.Start].All(char.IsWhiteSpace))
            return true;

        var lineEnd = text.IndexOf('\n', elementSpan.End);
        if (lineEnd < 0 ||
            !text[elementSpan.End..lineEnd].All(character =>
                character is ' ' or '\t' or '\r'))
        {
            return true;
        }

        removalSpan = new XmlSpan(lineStart, lineEnd + 1);
        return true;
    }

    static bool TryParseXmlElement(string text, out XElement element)
    {
        try
        {
            element = XElement.Parse(
                text,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            return true;
        }
        catch (XmlException)
        {
            element = new XElement("invalid");
            return false;
        }
    }

    static List<XNode> SignificantNodes(XContainer container) =>
        container.Nodes()
            .Where(node => node is not XText text ||
                text is XCData ||
                !string.IsNullOrWhiteSpace(text.Value))
            .ToList();

    static bool HasExactAttributes(
        XElement element,
        params (string Name, string Value)[] expected) =>
        element.Attributes().Count() == expected.Length &&
        expected.All(expectedAttribute =>
            element.Attribute(expectedAttribute.Name) is XAttribute attribute &&
            (expectedAttribute.Value.Length == 0 ||
             attribute.Value.Equals(
                 expectedAttribute.Value,
                 StringComparison.Ordinal)));

    static bool HasPlainTextContent(XElement element, out string value)
    {
        value = "";
        if (element.Nodes().Any(node => node is not XText || node is XCData))
            return false;
        value = string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value));
        return true;
    }

    static bool HasPlainTextOrInlineCodeContent(XElement element) =>
        element.Nodes().All(node => node switch
        {
            XText text when text is not XCData => true,
            XElement child when child.Name.LocalName == "c" &&
                !child.HasAttributes &&
                HasPlainTextContent(child, out _) => true,
            _ => false,
        });

    static string NormalizeStaleNestedConstructorLinks(
        string text,
        DocsBlock block)
    {
        var blockText = text[block.Start..block.End];
        if (!TryParseDocsBlock(blockText, out var docs))
            return text;

        var edits = new List<XmlSpanEdit>();
        foreach (var paragraph in docs.Descendants("para"))
        {
            if (!TryGetImporterSourceReferenceUrl(paragraph, out var sourceUrl))
                continue;

            var normalizedUrl = Regex.Replace(
                sourceUrl,
                @"(?<=#)[A-Za-z_]\w*\$(?<constructor>[A-Za-z_]\w*)(?=\()",
                "${constructor}",
                RegexOptions.CultureInvariant);
            var anchor = paragraph.Descendants("a").Single();
            var code = anchor.Element("code")!;
            var normalizedLabel = Regex.Replace(
                code.Value,
                @"[A-Za-z_]\w*\$(?<constructor>[A-Za-z_]\w*)(?=\()",
                "${constructor}",
                RegexOptions.CultureInvariant);
            if (normalizedUrl.Equals(sourceUrl, StringComparison.Ordinal) &&
                normalizedLabel.Equals(code.Value, StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryGetElementSpan(blockText, anchor, out var anchorSpan) ||
                !TryGetElementSpan(blockText, code, out var codeSpan) ||
                !TryGetDirectTextElementContentSpan(
                    blockText,
                    codeSpan,
                    out var codeContentSpan))
            {
                return text;
            }

            var href = Regex.Match(
                blockText[anchorSpan.Start..anchorSpan.End],
                @"\bhref=""(?<value>[^""]*)""",
                RegexOptions.CultureInvariant);
            if (!href.Success)
                return text;
            var hrefValueSpan = new XmlSpan(
                anchorSpan.Start + href.Groups["value"].Index,
                anchorSpan.Start + href.Groups["value"].Index +
                    href.Groups["value"].Length);
            if (!normalizedUrl.Equals(sourceUrl, StringComparison.Ordinal))
            {
                edits.Add(new XmlSpanEdit(
                    hrefValueSpan,
                    XmlAttributeEscape(normalizedUrl)));
            }
            if (!normalizedLabel.Equals(code.Value, StringComparison.Ordinal))
            {
                edits.Add(new XmlSpanEdit(
                    codeContentSpan,
                    XmlEscape(normalizedLabel)));
            }
        }

        foreach (var edit in edits.OrderByDescending(edit => edit.Span.Start))
        {
            blockText = blockText[..edit.Span.Start] + edit.Replacement +
                blockText[edit.Span.End..];
        }
        return blockText.Equals(text[block.Start..block.End], StringComparison.Ordinal)
            ? text
            : text[..block.Start] + blockText + text[block.End..];
    }

    static SourceReferenceCleanupResult RemoveEnumDiscardedMetadata(
        string blockText,
        SourceDocs docs)
    {
        if (!TryParseDocsBlock(blockText, out var document))
        {
            return SourceReferenceCleanupResult.Failure(
                blockText,
                SourceReferenceCleanupSkip.NotLocated(
                    "The current <Docs> block could not be parsed before removing transferred enum metadata."));
        }
        var summary = document.Element("summary");
        if (summary is null)
            return SourceReferenceCleanupResult.Success(blockText);

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
            return SourceReferenceCleanupResult.Success(blockText);

        var remarks = document.Element("remarks");
        var removed = RemoveXmlElements(
            blockText,
            remarks?.Elements("para").Where(paragraph =>
                XNode.DeepEquals(paragraph, expectedSource) ||
                (expectedAttribution is not null &&
                 XNode.DeepEquals(paragraph, expectedAttribution))) ??
                Enumerable.Empty<XElement>());
        if (removed.Skip is not null ||
            removed.RemovedCount == 0 ||
            !TryParseDocsBlock(removed.Text, out var updatedDocument) ||
            updatedDocument.Element("remarks") is not XElement updatedRemarks ||
            updatedRemarks.Nodes().Any(node =>
                node is not XText text ||
                text is XCData ||
                !string.IsNullOrWhiteSpace(text.Value)) ||
            !TryGetElementSpan(removed.Text, updatedRemarks, out var remarksSpan) ||
            !TryFindMarkupEnd(removed.Text, remarksSpan.Start, out var openingEnd))
        {
            return removed;
        }

        var openingTag = removed.Text[remarksSpan.Start..openingEnd];
        if (!openingTag.EndsWith(">", StringComparison.Ordinal))
            return removed;
        var selfClosingTag = openingTag[..^1].TrimEnd() + " />";
        var selfClosed =
            removed.Text[..remarksSpan.Start] + selfClosingTag +
            removed.Text[remarksSpan.End..];
        return SourceReferenceCleanupResult.Success(
            selfClosed,
            removed.RemovedCount);
    }

    static bool HasEnumDiscardedMetadataCandidate(LoadedFile file, DocsOwner owner)
    {
        if (!owner.IsEnumField)
            return false;

        var block = file.Text[file.DocsBlocks[owner.Order].Start..file.DocsBlocks[owner.Order].End];
        try
        {
            var docs = XElement.Parse(block, LoadOptions.PreserveWhitespace);
            return docs.Element("summary")?.Descendants("a").Any(anchor =>
                       string.Equals((string?)anchor.Attribute("title"),
                           "Reference documentation", StringComparison.Ordinal)) == true &&
                docs.Element("remarks")?.Descendants("a").Any(anchor =>
                    ((string?)anchor.Attribute("href"))?.Contains(
                        "developers.google.com/terms/site-policies",
                        StringComparison.Ordinal) == true) == true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    static bool HasImporterOwnedEnumListGap(DocsOwner owner, SourceDocs docs)
    {
        if (!owner.IsEnumField ||
            owner.Docs.Element("summary") is not XElement summary)
            return false;
        return HasImporterOwnedEnumListGap(summary, docs);
    }

    static bool HasImporterOwnedEnumListGap(XElement summary, SourceDocs docs)
    {
        if (docs.SourceKind != "android" ||
            !HasImporterOwnedEnumMetadata(summary, docs))
        {
            return false;
        }

        var content = summary.Nodes()
            .Where(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value))
            .ToList();
        if (content.Any(node => node is not XElement element ||
                (element.Name != "para" && element.Name != "code") ||
                (element.Name == "para" &&
                 !IsImporterOwnedEnumMetadataParagraph(element, docs) &&
                 element.HasElements) ||
                (element.Name == "code" &&
                 !string.Equals((string?)element.Attribute("lang"), "text/java",
                     StringComparison.Ordinal)) ||
                (element.Name == "code" && element.HasElements)))
        {
            return false;
        }

        var current = content
            .Cast<XElement>()
            .Where(element => element.Name != "para" ||
                !IsImporterOwnedEnumMetadataParagraph(element, docs))
            .Select(element => new SourceParagraph(
                element.Name == "code" ? element.Value : CleanSourceText(element.Value),
                element.Name == "code"))
            .ToList();
        var source = docs.Paragraphs
            .Select(paragraph => new SourceParagraph(
                paragraph.IsCode ? paragraph.Text : CleanSourceText(paragraph.Text),
                paragraph.IsCode))
            .Where(paragraph => paragraph.IsCode ||
                IsMeaningfulChannel(paragraph.Text, "remarks"))
            .ToList();
        var hasMissingListParagraph = source.Any(paragraph =>
            !paragraph.IsCode &&
            paragraph.Text.Contains("; ", StringComparison.Ordinal) &&
            !current.Contains(paragraph));
        return current.Count > 0 &&
            hasMissingListParagraph &&
            current.All(existing => source.Any(candidate =>
                candidate.IsCode == existing.IsCode &&
                (candidate.Text.Equals(existing.Text, StringComparison.Ordinal) ||
                 candidate.Text.StartsWith(existing.Text, StringComparison.Ordinal))));
    }

    static bool HasImporterOwnedSummarySourceTextRefresh(DocsOwner owner, SourceDocs docs)
    {
        if (owner.Docs.Element("summary") is not XElement summary)
        {
            return false;
        }

        return HasImporterOwnedSummarySourceTextRefresh(summary, docs);
    }

    static bool HasImporterOwnedSummarySourceTextRefresh(XElement summary, SourceDocs docs)
    {
        if (docs.SourceKind != "android" ||
            !HasImporterOwnedEnumMetadata(summary, docs))
        {
            return false;
        }

        var content = summary.Nodes()
            .Where(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value))
            .ToList();
        if (content.Any(node => node is not XElement element ||
                (element.Name != "para" && element.Name != "code") ||
                (element.Name == "para" &&
                 !IsImporterOwnedEnumMetadataParagraph(element, docs) &&
                 element.HasElements) ||
                (element.Name == "code" &&
                 (!string.Equals((string?)element.Attribute("lang"), "text/java",
                     StringComparison.Ordinal) ||
                  element.HasElements))))
        {
            return false;
        }

        var current = content
            .Cast<XElement>()
            .Where(element => element.Name != "para" ||
                !IsImporterOwnedEnumMetadataParagraph(element, docs))
            .Select(element => new SourceParagraph(
                element.Name == "code" ? element.Value : CleanSourceText(element.Value),
                element.Name == "code"))
            .ToList();
        var source = docs.Paragraphs
            .Select(paragraph => new SourceParagraph(
                paragraph.IsCode ? paragraph.Text : CleanSourceText(paragraph.Text),
                paragraph.IsCode))
            .Where(paragraph => paragraph.IsCode ||
                IsMeaningfulChannel(paragraph.Text, "remarks"))
            .ToList();

        if (current.Count != source.Count)
            return false;

        var foundKnownArtifact = false;
        foreach (var (existing, candidate) in current.Zip(source))
        {
            if (existing.IsCode != candidate.IsCode)
                return false;
            if (existing.Text.Equals(candidate.Text, StringComparison.Ordinal))
                continue;
            if (!existing.Text.Contains(
                    "AccessibilityServiceAccessibilityService.getWindows()",
                    StringComparison.Ordinal) ||
                !SourcePage.NormalizeAndroidSourceText(existing.Text).Equals(
                    candidate.Text,
                    StringComparison.Ordinal))
            {
                return false;
            }
            foundKnownArtifact = true;
        }

        return foundKnownArtifact;
    }

    static string RefreshImporterOwnedSummarySourceText(
        string text,
        LoadedFile file,
        DocsOwner owner,
        SourceDocs docs)
    {
        var block = file.DocsBlocks[owner.Order];
        var blockText = text[block.Start..block.End];
        var refreshedBlock = AddEnumSummaryMetadata(
            blockText,
            file,
            owner,
            docs,
            allowCreation: false);
        return refreshedBlock.Equals(blockText, StringComparison.Ordinal)
            ? text
            : text[..block.Start] + refreshedBlock + text[block.End..];
    }

    static bool HasPotentialEnumListRepair(LoadedFile file, DocsOwner owner) =>
        owner.IsEnumField &&
        HasImporterSourceReference(file, owner);

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
        if (!TryParseDocsBlock(blockText, out var document) ||
            document.Element("summary") is not XElement summaryElement ||
            !TryGetElementSpan(blockText, summaryElement, out var summarySpan) ||
            !TryGetDirectTextElementContentSpan(
                blockText,
                summarySpan,
                out var summaryContentSpan))
            return blockText;
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
        var hasCorrectSource = ContainsSourceUrl(summaryElement, docs.SourceUrl);
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
        var sourceTextRepairEligible = HasImporterOwnedSummarySourceTextRefresh(owner, docs);
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
            if (alreadyComplete ||
                (!repairEligible && !listRepairEligible && !sourceTextRepairEligible))
                return blockText;
        }
        else if (!allowCreation)
        {
            return blockText;
        }

        if (sourceDocumentation.Count == 0 && existingProse.Count == 0)
            return blockText;

        var newline = file.Newline;
        var lineStart = blockText.LastIndexOf(
            newline,
            summarySpan.Start,
            StringComparison.Ordinal);
        lineStart = lineStart < 0 ? 0 : lineStart + newline.Length;
        var candidateIndent = blockText[lineStart..summarySpan.Start];
        var summaryIndent = candidateIndent.All(char.IsWhiteSpace) ? candidateIndent : "";
        var paraIndent = summaryIndent + "  ";
        var sourceLabel = docs.SourceKind == "android" ? "Android" : "Java";
        var additions = repairEligible || creationEligible || listRepairEligible || sourceTextRepairEligible
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
        return blockText[..summaryContentSpan.Start] + replacement +
            blockText[summaryContentSpan.End..];
    }

    static bool IsDeprecationParagraph(string value) =>
        Regex.IsMatch(
            NormalizeText(value),
            @"^(?:This\s+)?(?:constant|field|member|method|class|interface)\s+(?:is|was)\s+deprecated\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static bool ContainsSourceUrl(string text, string sourceUrl) =>
        TryParseXmlElement(text, out var element) &&
        ContainsSourceUrl(element, sourceUrl);

    static bool ContainsSourceUrl(XContainer container, string sourceUrl) =>
        container.Descendants("a").Any(anchor =>
            UrlsEqual(
                (string?)anchor.Attribute("href") ?? "",
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

    static XElement DocumentationElement(SourceParagraph paragraph) =>
        XElement.Parse(RenderDocumentationParagraph(paragraph, ""));

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
        text = Regex.Replace(text, @"\{(?:@)?(?:link|linkplain|code|literal|value)\s+([^}]+)\}", "$1");
        text = Regex.Replace(text, @"\{@\w+(?:\s+[^}]*)?\}", "");
        text = text.Replace(
            "CharSequence.subsequence()",
            "CharSequence.subSequence()",
            StringComparison.Ordinal);
        text = Regex.Replace(
            text,
            @"(?<permission>Requires android\.Manifest\.permission\.[A-Z_]+)(?:\s+\k<permission>)+",
            "${permission}",
            RegexOptions.CultureInvariant);
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
        var legacyChineseRoc16k = string.Concat("Chinese R", "OC 16K media size");
        var legacyChineseRoc8k = string.Concat("Chinese R", "OC 8K media size");
        text = text.Replace(
            $"Mix of metric and imperial units used in {legacyGreatBritain}.",
            "Mix of metric and imperial units used in United Kingdom.",
            StringComparison.Ordinal);
        text = text.Replace(
            $"The {legacyLocaleLabel} value in the Locale created by the Builder is always normalized to upper case.",
            "The region value in the Locale created by the Builder is always normalized to upper case.",
            StringComparison.Ordinal);
        text = text.Replace(
            legacyChineseRoc16k,
            "Taiwan 16K media size",
            StringComparison.Ordinal);
        text = text.Replace(
            legacyChineseRoc8k,
            "Taiwan 8K media size",
            StringComparison.Ordinal);
        text = text.Replace(
            "like a notpad.",
            "like a notepad.",
            StringComparison.Ordinal);
        text = text.Replace(
            "orientation, which is the height is the lesser dimension.",
            "orientation, where the height is the lesser dimension.",
            StringComparison.Ordinal);
        text = text.Replace(
            "orientation, which is the height is the greater dimension.",
            "orientation, where the height is the greater dimension.",
            StringComparison.Ordinal);
        text = text.Replace(
            "New instance in landscape orientation if this one is in landscape, otherwise this instance.",
            "New instance in portrait orientation if this one is in landscape, otherwise this instance.",
            StringComparison.Ordinal);
        text = text.Replace(
            "Color mode: Color color scheme,",
            "Color mode: Color scheme,",
            StringComparison.Ordinal);
        text = text.Replace(
            "The print jobs is created,",
            "The print job is created,",
            StringComparison.Ordinal);
        text = text.Replace(
            "Kitkat",
            "KitKat",
            StringComparison.Ordinal);
        text = text.Replace(
            "PrintAttributes#COLOR_MODE_COLOR..",
            "PrintAttributes#COLOR_MODE_COLOR.",
            StringComparison.Ordinal);
        text = text.Replace(
            "North America Letter media size: 8.5\" x 11\" (279mm x 216mm)",
            "North America Letter media size: 8.5\" x 11\" (216mm x 279mm)",
            StringComparison.Ordinal);
        text = text.Replace(
            "The default color mode. Value is either 0 or a combination of the following: PrintAttributes.COLOR_MODE_MONOCHROME; PrintAttributes.COLOR_MODE_COLOR",
            "The default color mode. Must be exactly one of the following: PrintAttributes.COLOR_MODE_MONOCHROME; PrintAttributes.COLOR_MODE_COLOR",
            StringComparison.Ordinal);
        text = text.Replace(
            "The default duplex mode. Value is either 0 or a combination of the following: PrintAttributes.DUPLEX_MODE_NONE; PrintAttributes.DUPLEX_MODE_LONG_EDGE; PrintAttributes.DUPLEX_MODE_SHORT_EDGE",
            "The default duplex mode. Must be exactly one of the following: PrintAttributes.DUPLEX_MODE_NONE; PrintAttributes.DUPLEX_MODE_LONG_EDGE; PrintAttributes.DUPLEX_MODE_SHORT_EDGE",
            StringComparison.Ordinal);
        text = text.Replace(
            "Value is milliseconds since January 1, 2001.",
            "Value is seconds since January 1, 2001.",
            StringComparison.Ordinal);
        text = text.Replace(
            "Federated Compute Server documentation..",
            "Federated Compute Server documentation.",
            StringComparison.Ordinal);
        text = text.Replace(
            "groups (delimited by ( and ()",
            "groups (delimited by ( and ))",
            StringComparison.Ordinal);
        text = text.Replace(
            "selected (getChangedFields();",
            "selected (getChangedFields());",
            StringComparison.Ordinal);
        text = text.Replace(
            "before before autofilling",
            "before autofilling",
            StringComparison.Ordinal);
        text = text.Replace(
            "Altough similiarly",
            "Although similarly",
            StringComparison.Ordinal);
        text = text.Replace(
            "a combination of FillResponse.FLAG_TRACK_CONTEXT_COMMITED and FillResponse.FLAG_DISABLE_ACTIVITY_ONLY, or 0. Value is either 0 or a combination of the following:",
            "a combination of FillResponse.FLAG_TRACK_CONTEXT_COMMITED, FillResponse.FLAG_DISABLE_ACTIVITY_ONLY, and FillResponse.FLAG_DELAY_FILL, or 0. Value is either 0 or a combination of the following:",
            StringComparison.Ordinal);
        text = text.Replace(
            "Resoure Id",
            "Resource Id",
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

    static string NormalizeSourceProseForComparison(string value) =>
        Regex.Replace(
            NormalizeText(value),
            @"(?<type>[A-Za-z_]\w*)\.(?<member>[A-Za-z_]\w*)\(",
            "${type}#${member}(",
            RegexOptions.CultureInvariant);

    static string NormalizeNormalWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ").Trim();

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

    static void TestControlTemplateParagraphBoundary(string repositoryRoot, string fixtureRoot)
    {
        var html = File.ReadAllText(Path.Combine(fixtureRoot, "controls-template-android-reference.html"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var request = SourceRequest.Create("android/service/controls/Control$StatefulBuilder")!;
        var sourceDocs = SourcePage.Parse(request, html).Members.Single().Docs!;
        Assert(
            sourceDocs.Summary == ControlTemplateLead &&
            sourceDocs.Paragraphs.Select(paragraph => paragraph.Text)
                .SequenceEqual([ControlTemplateLead, ControlTemplateDescription]) &&
            sourceDocs.Paragraphs.All(paragraph => !paragraph.IsCode),
            "ControlTemplate official blank line separates the unpunctuated lead-in without inventing punctuation");
        var crlfDocs = SourcePage.Parse(request, html.Replace("\n", "\r\n", StringComparison.Ordinal))
            .Members.Single().Docs!;
        Assert(
            crlfDocs.Paragraphs.SequenceEqual(sourceDocs.Paragraphs),
            "ControlTemplate source paragraph boundary handles CRLF");
        foreach (var (candidate, url) in new[]
        {
            (html, ControlTemplateSourceUrl + ".Altered"),
            (html.Replace("primary user interaction", "authored user interaction", StringComparison.Ordinal),
                ControlTemplateSourceUrl),
            (html.Replace("Devices may support", "Devices can support", StringComparison.Ordinal),
                ControlTemplateSourceUrl),
            (html.Replace("interaction\n\nDevices", "interaction\nDevices", StringComparison.Ordinal),
                ControlTemplateSourceUrl),
            (html.Replace("interaction\n\nDevices", "interaction.\n\nDevices", StringComparison.Ordinal),
                ControlTemplateSourceUrl),
            ("<p>Ordinary text\n\nMore ordinary text.</p>", ControlTemplateSourceUrl),
            ("<pre>" + LegacyControlTemplateParagraph + "</pre>", ControlTemplateSourceUrl),
        })
        {
            Assert(
                SourcePage.NormalizeKnownAndroidParagraphBoundary(candidate, url) == candidate,
                "known paragraph boundary does not reinterpret other sources, line wrapping, completed sentences, or code");
        }

        var file = LoadedFile.Load(
            repositoryRoot,
            Path.Combine(fixtureRoot, "controls-template-source.xml"));
        file.SelectOwners("SetControlTemplate");
        var owner = file.Owners.Single();
        var blockStart = file.DocsBlocks[owner.Order].Start;
        var blockEnd = file.DocsBlocks[owner.Order].End;
        var fixtureText = file.Text;
        var originalDocs = new XElement(owner.Docs);
        var mapped = MapOwner(
            owner,
            new Dictionary<string, SourceLoadResult>
            {
                [request.Url] = SourceLoadResult.Success(SourcePage.Parse(request, html)),
            });
        Assert(
            mapped.ErrorReason is null && mapped.Docs?.Summary == ControlTemplateLead,
            "ControlTemplate fixture maps the exact JNI overload");

        (string Before, ParagraphBoundaryRepairResult Repair) RepairCase(
            XElement docs,
            SourceDocs? source = null,
            string? memberId = null)
        {
            var docsText = docs.ToString(SaveOptions.DisableFormatting);
            var text = fixtureText[..blockStart] + docsText + fixtureText[blockEnd..];
            file.UpdateBlockOffsets(owner.Order, text);
            var candidate = owner with
            {
                Id = memberId ?? owner.Id,
                Docs = XElement.Parse(docsText, LoadOptions.PreserveWhitespace),
            };
            return (text, RepairKnownAndroidParagraphBoundary(text, file, candidate, source ?? sourceDocs));
        }

        var repaired = RepairCase(new XElement(originalDocs));
        var repairedDocs = XElement.Parse(repaired.Repair.Text)
            .Element("Members")!.Element("Member")!.Element("Docs")!;
        Assert(
            repaired.Repair.Reason is null &&
            repaired.Repair.Text != repaired.Before &&
            repairedDocs.Element("summary")!.Value == ControlTemplateLead &&
            repairedDocs.Element("remarks")!.Elements("para").Take(2)
                .Select(paragraph => paragraph.Value)
                .SequenceEqual([ControlTemplateLead, ControlTemplateDescription]) &&
            HasExactImporterSourceReference(repairedDocs, sourceDocs) &&
            ImporterMarkupEquals(
                repairedDocs.Element("remarks")!.Elements("para").Last(),
                XElement.Parse($"<para>{AndroidAttribution}</para>")) &&
            XNode.DeepEquals(repairedDocs.Element("param"), originalDocs.Element("param")) &&
            XNode.DeepEquals(repairedDocs.Element("returns"), originalDocs.Element("returns")),
            "guarded ControlTemplate regeneration repairs both channels and preserves authored parameters, returns, reference, and attribution");
        var repeated = RepairCase(repairedDocs);
        Assert(
            repeated.Repair.Text == repeated.Before && repeated.Repair.Reason is null,
            "ControlTemplate paragraph repair is idempotent");
        var wrongMember = RepairCase(originalDocs, memberId: ControlTemplateMemberId + ".Altered");
        Assert(
            wrongMember.Repair.Text == wrongMember.Before,
            "ControlTemplate paragraph repair requires exact managed identity");
        foreach (var source in new[]
        {
            sourceDocs with { SourceUrl = ControlTemplateSourceUrl + ".Altered" },
            sourceDocs with { SourceKind = "java" },
            sourceDocs with { Summary = ControlTemplateLead + "." },
            sourceDocs with { Paragraphs = [new SourceParagraph(ControlTemplateLead, true)] },
            sourceDocs with { Paragraphs = [new SourceParagraph(LegacyControlTemplateParagraph, false)] },
        })
        {
            var preserved = RepairCase(originalDocs, source);
            Assert(
                preserved.Repair.Text == preserved.Before &&
                preserved.Repair.Reason == "source_paragraph_boundary_mismatch",
                "ControlTemplate paragraph repair reports and preserves changed source contracts");
        }

        var authoredVariants = new List<XElement>();
        void AuthoredVariant(Action<XElement> alter)
        {
            var docs = new XElement(originalDocs);
            alter(docs);
            authoredVariants.Add(docs);
        }
        AuthoredVariant(docs => docs.Element("summary")!.Add(new XElement("c", "Authored content.")));
        AuthoredVariant(docs => docs.Element("summary")!.ReplaceWith(
            new XElement("summary", new XCData(LegacyControlTemplateSummary))));
        AuthoredVariant(docs => docs.Add(new XElement(docs.Element("summary")!)));
        AuthoredVariant(docs => docs.Element("remarks")!.AddFirst(new XElement("para", "Authored content.")));
        AuthoredVariant(docs => docs.Element("remarks")!.Element("para")!.Add(new XElement("c", "")));
        AuthoredVariant(docs => docs.Element("remarks")!.AddFirst(new XComment("Authored content.")));
        AuthoredVariant(docs => docs.Element("remarks")!.AddFirst(new XProcessingInstruction("authored", "keep")));
        AuthoredVariant(docs => docs.Element("remarks")!.Element("para")!.ReplaceWith(
            new XElement("para", new XCData(LegacyControlTemplateParagraph))));
        AuthoredVariant(docs => docs.Element("remarks")!.Elements("para").ElementAt(1)
            .Descendants("a").Single().SetAttributeValue("href", ControlTemplateSourceUrl + ".Altered"));
        AuthoredVariant(docs => docs.Element("remarks")!.Elements("para").ElementAt(1)
            .Descendants("a").Single().Add(new XElement("c", "")));
        AuthoredVariant(docs => docs.Element("remarks")!.Elements("para").Last().Add(" Authored content."));
        AuthoredVariant(docs => docs.Element("remarks")!.Elements("para").Last().Remove());
        AuthoredVariant(docs => docs.Add(new XElement(docs.Element("remarks")!)));
        foreach (var docs in authoredVariants)
        {
            var preserved = RepairCase(docs);
            Assert(
                preserved.Repair.Text == preserved.Before,
                "ControlTemplate paragraph repair preserves authored, mixed, duplicate, CDATA, comment, and processing-instruction nodes");
        }

        file.UpdateBlockOffsets(owner.Order, repaired.Before);
        var mismatchedDocs = new XElement(originalDocs);
        mismatchedDocs.Element("param")!.Value = "Changed owner snapshot.";
        var mismatch = RepairKnownAndroidParagraphBoundary(
            repaired.Before,
            file,
            owner with { Docs = mismatchedDocs },
            sourceDocs);
        Assert(
            mismatch.Text == repaired.Before &&
            mismatch.Reason == "paragraph_boundary_target_not_located",
            "ControlTemplate paragraph repair preserves parser-correspondence mismatches");
    }

    static void TestControlsLifecycle(string repositoryRoot, string fixtureRoot)
    {
        var html = File.ReadAllText(Path.Combine(fixtureRoot, "controls-lifecycle-android-reference.html"));
        var request = SourceRequest.Create("android/service/controls/ControlsProviderService")!;
        var page = SourcePage.Parse(request, html);
        var file = LoadedFile.Load(repositoryRoot, Path.Combine(fixtureRoot, "controls-lifecycle-source.xml"));
        file.SelectOwners(null);
        var fixtureText = file.Text;
        var pages = new Dictionary<string, SourceLoadResult>
        {
            [request.Url] = SourceLoadResult.Success(page),
        };
        foreach (var owner in file.Owners.Where(owner => owner.Member is not null))
        {
            file.UpdateBlockOffsets(owner.Order, fixtureText);
            var start = file.DocsBlocks[owner.Order].Start;
            var end = file.DocsBlocks[owner.Order].End;
            var originalDocs = new XElement(owner.Docs);
            var rule = KnownControlsLifecycleRepairs.Single(repair => repair.MemberId == owner.Id);
            var mapping = MapOwner(owner, pages);
            var source = mapping.Docs!;
            var rawSource = page.Members.Single(member => member.Name == owner.MemberRegistration!.Name).Docs!;
            Assert(mapping.ErrorReason is null &&
                rawSource.Paragraphs.Select(paragraph => paragraph.Text).SequenceEqual(rule.OriginalParagraphs) &&
                source.Summary == rule.Summary &&
                source.Paragraphs.Select(paragraph => paragraph.Text).SequenceEqual(rule.SafeParagraphs),
                "registered Controls lifecycle mapping retains only exact safe reference sentences");
            Assert(rule.IncorrectReturn is null ||
                ReplacementFor(new Placeholder(0, "returns", "", "returns"), source).Reason == "source_channel_ambiguous",
                "Controls OnUnbind caller-choice return is suppressed before first fill");
            foreach (var (memberId, mismatched) in new[]
            {
                (owner.Id + ".Other", rawSource),
                (owner.Id, rawSource with { SourceUrl = rawSource.SourceUrl + ".Other" }),
                (owner.Id, rawSource with { SourceKind = "java" }),
            })
            {
                Assert(ReferenceEquals(
                    WithoutKnownUnsafeControlsLifecycleChannels(memberId, mismatched), mismatched),
                    "Controls lifecycle filtering requires the exact member, URL, and Android provenance");
            }
            var changedProse = rawSource with
            {
                Paragraphs = [new SourceParagraph(rule.OriginalParagraphs[0] + " Changed source.", false)],
            };
            Assert(WithoutKnownUnsafeControlsLifecycleChannels(owner.Id, changedProse)
                .Paragraphs.SequenceEqual(changedProse.Paragraphs),
                "Controls lifecycle filtering does not truncate changed source prose");

            (string Before, ControlsLifecycleRepairResult Result) RepairCase(
                XElement docs, SourceDocs? candidateSource = null, string? memberId = null)
            {
                var docsText = docs.ToString(SaveOptions.DisableFormatting);
                var text = fixtureText[..start] + docsText + fixtureText[end..];
                file.UpdateBlockOffsets(owner.Order, text);
                var candidate = owner with
                {
                    Id = memberId ?? owner.Id,
                    Docs = XElement.Parse(docsText, LoadOptions.PreserveWhitespace),
                };
                return (text, RepairKnownControlsLifecycle(text, file, candidate, candidateSource ?? source));
            }

            var repaired = RepairCase(originalDocs);
            var repairedDocs = XElement.Parse(repaired.Result.Text).Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == owner.Member!.Attribute("MemberName")!.Value)
                .Element("Docs")!;
            Assert(repaired.Result.Reason is null && repaired.Result.Text != repaired.Before &&
                XNode.DeepEquals(repairedDocs.Element("summary"), originalDocs.Element("summary")) &&
                XNode.DeepEquals(repairedDocs.Element("param"), originalDocs.Element("param")) &&
                repairedDocs.Element("remarks")!.Elements("para").Take(rule.SafeParagraphs.Count())
                    .Select(paragraph => paragraph.Value).SequenceEqual(rule.SafeParagraphs) &&
                HasExactImporterSourceReference(repairedDocs, source) &&
                ImporterMarkupEquals(repairedDocs.Element("remarks")!.Elements("para").Last(),
                    XElement.Parse($"<para>{AndroidAttribution}</para>")) &&
                repairedDocs.Element("returns")!.Value == "To be added.",
                "Controls lifecycle old-import repair preserves correct summary, authored parameter, source reference, and attribution");
            var repeated = RepairCase(repairedDocs);
            Assert(repeated.Result.Text == repeated.Before && repeated.Result.Reason is null,
                "Controls lifecycle repair is idempotent");
            var wrongMember = RepairCase(originalDocs, memberId: owner.Id + ".Other");
            Assert(wrongMember.Result.Text == wrongMember.Before,
                "Controls lifecycle repair requires the exact managed identity");
            var mismatchSources = new List<SourceDocs>
            {
                source with { SourceUrl = source.SourceUrl + ".Other" },
                source with { SourceKind = "java" },
                source with { Summary = source.Summary + " Changed." },
                source with { Paragraphs = [new SourceParagraph(rule.Summary, true)] },
                source with { Paragraphs = rawSource.Paragraphs },
            };
            if (rule.IncorrectReturn is not null)
                mismatchSources.AddRange([
                    source with { Returns = source.Returns + " Changed." },
                    source with { UnsafeTargets = null },
                ]);
            foreach (var mismatched in mismatchSources)
            {
                var preserved = RepairCase(originalDocs, mismatched);
                Assert(preserved.Result.Text == preserved.Before &&
                    preserved.Result.Reason == "source_controls_lifecycle_mismatch",
                    "Controls lifecycle repair reports changed source text and provenance without edits");
            }

            var authoredVariants = new List<XElement>();
            void Variant(Action<XElement> alter)
            {
                var docs = new XElement(originalDocs);
                alter(docs);
                authoredVariants.Add(docs);
            }
            Variant(docs => docs.Element("summary")!.Add(" Authored addition."));
            Variant(docs => docs.Element("summary")!.ReplaceNodes(new XCData(rule.Summary)));
            Variant(docs => docs.Add(new XElement(docs.Element("summary")!)));
            Variant(docs => docs.Element("remarks")!.Element("para")!.Add(" Authored addition."));
            Variant(docs => docs.Element("remarks")!.Element("para")!.Add(new XElement("c", "")));
            Variant(docs => docs.Element("remarks")!.Element("para")!.ReplaceNodes(
                new XCData(rule.OriginalParagraphs[0])));
            Variant(docs => docs.Element("remarks")!.AddFirst(new XComment("Authored comment.")));
            Variant(docs => docs.Element("remarks")!.AddFirst(new XProcessingInstruction("authored", "keep")));
            Variant(docs => docs.Element("remarks")!.Add(new XElement("para", "Authored paragraph.")));
            Variant(docs => docs.Add(new XElement(docs.Element("remarks")!)));
            Variant(docs => docs.Element("remarks")!.Elements("para").Single(paragraph =>
                TryGetImporterSourceReferenceUrl(paragraph, out _))
                .Descendants("a").Single().SetAttributeValue("href", rule.SourceUrl + ".Other"));
            Variant(docs => docs.Element("remarks")!.Elements("para").Single(paragraph =>
                TryGetImporterSourceReferenceUrl(paragraph, out _))
                .Descendants("a").Single().Add(new XElement("c", "")));
            Variant(docs => docs.Element("remarks")!.Elements("para").Last().Add(" Authored attribution."));
            Variant(docs => docs.Element("remarks")!.Elements("para").Last().Remove());
            Variant(docs => docs.Add(new XElement(docs.Element("returns")!)));
            if (rule.IncorrectReturn is not null)
            {
                Variant(docs => docs.Element("returns")!.Add(new XElement("c", "")));
                Variant(docs => docs.Element("returns")!.ReplaceNodes(new XCData(rule.IncorrectReturn)));
                Variant(docs => docs.Element("returns")!.Add(new XComment("Authored return.")));
                Variant(docs => docs.Element("returns")!.Add(new XProcessingInstruction("authored", "keep")));
            }
            foreach (var docs in authoredVariants)
            {
                var preserved = RepairCase(docs);
                Assert(preserved.Result.Text == preserved.Before &&
                    preserved.Result.Reason == "existing_controls_lifecycle_not_importer_owned",
                    "Controls lifecycle repair preserves and reports authored/full-text, mixed, CDATA, comment, PI, duplicate, and metadata mismatches");
            }
            file.UpdateBlockOffsets(owner.Order, repaired.Before);
            var mismatchedDocs = new XElement(originalDocs);
            mismatchedDocs.Element("param")!.Value = "Changed owner snapshot.";
            var mismatch = RepairKnownControlsLifecycle(repaired.Before, file,
                owner with { Docs = mismatchedDocs }, source);
            Assert(mismatch.Text == repaired.Before && mismatch.Reason == "controls_lifecycle_target_not_located",
                "Controls lifecycle repair preserves parser-correspondence mismatches");
        }

        var token = $"controls-lifecycle-self-test-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var tempDirectory = Path.Combine(repositoryRoot, "tools", token);
        var pipelinePath = Path.Combine(repositoryRoot, "docs", "xml", "Android.Service.Controls", token + ".xml");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var cachePath = Path.Combine(tempDirectory,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Url))).ToLowerInvariant() + ".html");
            File.WriteAllText(cachePath, html, new UTF8Encoding(false));
            File.WriteAllText(pipelinePath, fixtureText.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
            var originalBytes = File.ReadAllBytes(pipelinePath);
            JsonDocument RunPipeline(bool apply, int limit, string? member = null)
            {
                var reportPath = Path.Combine(tempDirectory, "report");
                var args = new List<string>
                {
                    "--path", pipelinePath, "--namespace", "Android.Service.Controls",
                    "--offline", "--cache", tempDirectory,
                    "--max-changes", limit.ToString(), "--report", reportPath,
                };
                if (apply)
                    args.Add("--apply");
                if (member is not null)
                    args.AddRange(["--member", member]);
                Assert(RunAsync(args.ToArray()).GetAwaiter().GetResult() == 0,
                    "Controls lifecycle registered pipeline succeeds");
                var report = JsonDocument.Parse(File.ReadAllText(reportPath + ".json"));
                Assert(report.RootElement.GetProperty("errorCount").GetInt32() == 0,
                    "Controls lifecycle pipeline reports zero errors");
                return report;
            }
            using (var limited = RunPipeline(true, 1, "OnUnbind"))
            {
                Assert(limited.RootElement.GetProperty("appliedCount").GetInt32() == 0 &&
                    File.ReadAllBytes(pipelinePath).SequenceEqual(originalBytes),
                    "Controls OnUnbind two-channel repair cannot partially apply with max-one");
            }
            using (var measured = RunPipeline(false, 3))
                Assert(measured.RootElement.GetProperty("wouldApplyCount").GetInt32() == 3,
                    "Controls lifecycle dry run measures all three old-import channels");
            using (var applied = RunPipeline(true, 3))
                Assert(applied.RootElement.GetProperty("appliedCount").GetInt32() == 3,
                    "Controls lifecycle pipeline repairs exactly three channels");
            var repairedBytes = File.ReadAllBytes(pipelinePath);
            using (var repeated = RunPipeline(true, 10))
                Assert(repeated.RootElement.GetProperty("appliedCount").GetInt32() == 0 &&
                    File.ReadAllBytes(pipelinePath).SequenceEqual(repairedBytes),
                    "Controls lifecycle old-import rerun has zero operations and is byte-identical");

            var firstFill = XElement.Parse(fixtureText, LoadOptions.PreserveWhitespace);
            foreach (var docs in firstFill.Element("Members")!.Elements("Member").Select(member => member.Element("Docs")!))
                foreach (var target in new[] { "summary", "returns", "remarks" })
                    docs.Element(target)!.ReplaceNodes("To be added.");
            File.WriteAllText(pipelinePath, firstFill.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
            using (var filled = RunPipeline(true, 10))
                Assert(filled.RootElement.GetProperty("appliedCount").GetInt32() == 4,
                    "registered Controls first-fill imports safe summaries and remarks, not the unsafe return");
            var filledText = File.ReadAllText(pipelinePath);
            var filledXml = XElement.Parse(filledText);
            Assert(!filledText.Contains("May return null", StringComparison.Ordinal) &&
                !filledText.Contains("The default implementation does nothing", StringComparison.Ordinal) &&
                !filledText.Contains("Return true if you would like", StringComparison.Ordinal) &&
                filledXml.Element("Members")!.Elements("Member").All(member =>
                    member.Element("Docs")!.Element("returns")!.Value == "To be added." &&
                    member.Element("Docs")!.Element("param")!.Value == "Existing parameter documentation."),
                "actual registered first-fill never emits unsafe inherited prose or overwrites authored parameters");
            var filledBytes = File.ReadAllBytes(pipelinePath);
            using (var repeated = RunPipeline(true, 10))
                Assert(repeated.RootElement.GetProperty("appliedCount").GetInt32() == 0 &&
                    File.ReadAllBytes(pipelinePath).SequenceEqual(filledBytes),
                    "Controls lifecycle first-fill rerun has zero operations and is byte-identical");
        }
        finally
        {
            File.Delete(pipelinePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    static int RunSelfTest(string repositoryRoot)
    {
        TestKnownAndroidTextRepairs();
        TestKnownEapChannelCorrections(repositoryRoot);
        var fixtureRoot = Path.Combine(repositoryRoot, "tools", "importer-fixtures");
        TestControlTemplateParagraphBoundary(repositoryRoot, fixtureRoot);
        TestControlsLifecycle(repositoryRoot, fixtureRoot);
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
                Directory.EnumerateFiles(docsRoot, "*.xml", SearchOption.AllDirectories).Count() -
                Directory.EnumerateFiles(
                    Path.Combine(docsRoot, "FrameworksIndex"),
                    "*.xml",
                    SearchOption.AllDirectories).Count() - 2,
            "path-only repository-wide scope excludes root metadata and framework indexes");
        var sourcePath = Path.Combine(fixtureRoot, "source.xml");
        var androidHtml = File.ReadAllText(Path.Combine(fixtureRoot, "android-reference.html"));
        var javaHtml = File.ReadAllText(Path.Combine(fixtureRoot, "java-reference.html"));
        var unsafeHardwareBufferDocs = new SourceDocs(
            "",
            [
                new SourceParagraph(
                    "Calling this method will throw an IllegalStateException if format is not a supported Format type.",
                    false),
                new SourceParagraph("Retained source prose.", false),
            ],
            new Dictionary<string, string>(),
            "",
            new Dictionary<string, string>(),
            "https://developer.android.com/reference/android/hardware/HardwareBuffer#create(int,%20int,%20int,%20int,%20long)",
            "Android reference",
            "Android");
        var filteredHardwareBufferDocs = WithoutKnownUnsafeHardwareBufferCreateRemark(
            "M:Android.Hardware.HardwareBuffer.Create(System.Int32,System.Int32,Android.Hardware.HardwareBufferFormat,System.Int32,Android.Hardware.HardwareBufferUsage)",
            unsafeHardwareBufferDocs);
        Assert(
            filteredHardwareBufferDocs.Paragraphs.Count == 1 &&
            filteredHardwareBufferDocs.Paragraphs[0].Text == "Retained source prose.",
            "HardwareBuffer Create omits the contradicted IllegalStateException remark");
        Assert(
            WithoutKnownUnsafeHardwareBufferCreateRemark(
                "M:Android.Hardware.HardwareBuffer.Create(System.Int32,System.Int32,Android.Hardware.HardwareBufferFormat,System.Int32,Android.Hardware.HardwareBufferUsage)",
                unsafeHardwareBufferDocs with
                {
                    SourceUrl = "https://developer.android.com/reference/android/hardware/HardwareBuffer",
                }).Paragraphs.Count == 2,
            "HardwareBuffer Create filter requires the exact source channel");
        var unsafeRemoteGetDocs = WithoutKnownUnsafeRemoteEntryGuidance(
            "M:Android.Service.Credentials.BeginGetCredentialResponse.Builder.SetRemoteCredentialEntry(Android.Service.Credentials.RemoteEntry)",
            new SourceDocs(
                "",
                [
                    new SourceParagraph(
                        "When constructing the CredentialEntry object, the pendingIntent must be set such that it leads to an activity that can provide UI to fulfill the request on a remote device. When user selects this remoteCredentialEntry, the system will invoke the pendingIntent set on the CredentialEntry.",
                        false),
                    new SourceParagraph(
                        "Once the remote credential flow is complete, the Activity result should be set to Activity.RESULT_OK and an extra with the CredentialProviderService.EXTRA_GET_CREDENTIAL_RESPONSE key should be populated with a Credential object.",
                        false),
                    new SourceParagraph("Retained remote credential guidance.", false),
                ],
                new Dictionary<string, string>(),
                "",
                new Dictionary<string, string>(),
                "https://developer.android.com/reference/android/service/credentials/BeginGetCredentialResponse.Builder#setRemoteCredentialEntry(android.service.credentials.RemoteEntry)",
                "android.service.credentials.BeginGetCredentialResponse.Builder.setRemoteCredentialEntry",
                "Android"));
        Assert(
            unsafeRemoteGetDocs.Paragraphs.Count == 1 &&
            unsafeRemoteGetDocs.Paragraphs[0].Text == "Retained remote credential guidance.",
            "remote credential guidance omits contradicted entry and response payload instructions");
        var unsafeRemoteCreateDocs = WithoutKnownUnsafeRemoteEntryGuidance(
            "M:Android.Service.Credentials.BeginCreateCredentialResponse.Builder.SetRemoteCreateEntry(Android.Service.Credentials.RemoteEntry)",
            new SourceDocs(
                "",
                [
                    new SourceParagraph(
                        "When constructing the CreateEntry object, the pendingIntent must be set such that it leads to an activity that can provide UI to fulfill the request on a remote device. When user selects this remoteCreateEntry, the system will invoke the pendingIntent set on the CreateEntry.",
                        false),
                    new SourceParagraph("Retained remote create guidance.", false),
                ],
                new Dictionary<string, string>(),
                "",
                new Dictionary<string, string>(),
                "https://developer.android.com/reference/android/service/credentials/BeginCreateCredentialResponse.Builder#setRemoteCreateEntry(android.service.credentials.RemoteEntry)",
                "android.service.credentials.BeginCreateCredentialResponse.Builder.setRemoteCreateEntry",
                "Android"));
        Assert(
            unsafeRemoteCreateDocs.Paragraphs.Count == 1 &&
            unsafeRemoteCreateDocs.Paragraphs[0].Text == "Retained remote create guidance.",
            "remote create guidance omits contradicted entry instruction");
        var file = LoadedFile.Load(repositoryRoot, sourcePath);
        var fixtureText = file.Text;
        file.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
        Assert(file.Owners.Count == 15, "fixture owner count");
        var apiSinceFile = LoadedFile.Load(repositoryRoot, sourcePath);
        apiSinceFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot), apiSince: 1);
        Assert(
            apiSinceFile.Owners.Any(owner =>
                owner.Id == "P:Android.Example.Widget.FavoriteProperty"),
            "API level filter selects ApiSince owners");
        var platformSinceFile = LoadedFile.Load(repositoryRoot, sourcePath);
        var setCount = platformSinceFile.Root
            .Element("Members")?
            .Elements("Member")
            .SingleOrDefault(member => (string?)member.Attribute("MemberName") == "SetCount") ??
            throw new InvalidOperationException("SELF-TEST FAIL: SetCount fixture member");
        var setCountAttributes = setCount.Element("Attributes") ??
            throw new InvalidOperationException("SELF-TEST FAIL: SetCount fixture attributes");
        setCountAttributes.Add(
            new XElement(
                "Attribute",
                new XElement(
                    "AttributeName",
                    new XAttribute("Language", "C#"),
                    """[System.Runtime.Versioning.SupportedOSPlatform("android2.0")]""")));
        platformSinceFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot), apiSince: 2);
        Assert(
            platformSinceFile.Owners is
            [
                { Id: "M:Android.Example.Widget.SetCount(System.Int32)" },
            ],
            "API level filter selects SupportedOSPlatform owners");
        var inheritedSinceFile = LoadedFile.Load(repositoryRoot, sourcePath);
        var typeAttributes = inheritedSinceFile.Root.Element("Attributes") ??
            throw new InvalidOperationException("SELF-TEST FAIL: type fixture attributes");
        typeAttributes.Add(
            new XElement(
                "Attribute",
                new XElement(
                    "AttributeName",
                    new XAttribute("Language", "C#"),
                    """[System.Runtime.Versioning.SupportedOSPlatform("android2.0")]""")));
        inheritedSinceFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot), apiSince: 2);
        Assert(
            inheritedSinceFile.Owners.Any(owner => owner.Id == "T:Android.Example.Widget") &&
            inheritedSinceFile.Owners.Any(owner =>
                owner.Id == "M:Android.Example.Widget.SetTitle(Java.Lang.ICharSequence)") &&
            inheritedSinceFile.Owners.All(owner =>
                owner.Id != "P:Android.Example.Widget.FavoriteProperty"),
            "API level filter inherits type availability for unversioned members");
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
                "The region value in the Locale created by the Builder is always normalized to upper case." &&
            CleanSourceText($"{string.Concat("Chinese R", "OC 16K media size")}: 195mm x 270mm") ==
                "Taiwan 16K media size: 195mm x 270mm" &&
            CleanSourceText($"{string.Concat("Chinese R", "OC 8K media size")}: 270mm x 390mm") ==
                "Taiwan 8K media size: 270mm x 390mm",
            "PolicyCheck geopolitical terminology normalization");
        Assert(
            CleanSourceText("Duplex mode: Pages are turned upwards along the short edge - like a notpad.") ==
                "Duplex mode: Pages are turned upwards along the short edge - like a notepad." &&
            CleanSourceText(
                "Returns a new media size instance in a landscape orientation, which is the height is the lesser dimension.") ==
                "Returns a new media size instance in a landscape orientation, where the height is the lesser dimension." &&
            CleanSourceText(
                "New instance in landscape orientation if this one is in landscape, otherwise this instance.") ==
                "New instance in portrait orientation if this one is in landscape, otherwise this instance." &&
            CleanSourceText("Color mode: Color color scheme, for example many colors are used.") ==
                "Color mode: Color scheme, for example many colors are used." &&
            CleanSourceText("The print jobs is created, it is ready to be printed and should be processed.") ==
                "The print job is created, it is ready to be printed and should be processed." &&
            CleanSourceText(
                "On platform version 19 (Kitkat) specify PrintAttributes#COLOR_MODE_COLOR..") ==
                "On platform version 19 (KitKat) specify PrintAttributes#COLOR_MODE_COLOR.",
            "Android Print source text corrections");
        Assert(
            CleanSourceText("North America Letter media size: 8.5\" x 11\" (279mm x 216mm)") ==
                "North America Letter media size: 8.5\" x 11\" (216mm x 279mm)" &&
            CleanSourceText(
                "The default color mode. Value is either 0 or a combination of the following: PrintAttributes.COLOR_MODE_MONOCHROME; PrintAttributes.COLOR_MODE_COLOR") ==
                "The default color mode. Must be exactly one of the following: PrintAttributes.COLOR_MODE_MONOCHROME; PrintAttributes.COLOR_MODE_COLOR" &&
            CleanSourceText(
                "The default duplex mode. Value is either 0 or a combination of the following: PrintAttributes.DUPLEX_MODE_NONE; PrintAttributes.DUPLEX_MODE_LONG_EDGE; PrintAttributes.DUPLEX_MODE_SHORT_EDGE") ==
                "The default duplex mode. Must be exactly one of the following: PrintAttributes.DUPLEX_MODE_NONE; PrintAttributes.DUPLEX_MODE_LONG_EDGE; PrintAttributes.DUPLEX_MODE_SHORT_EDGE",
            "Android Print media and default-mode corrections");
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

        var autofillResidualSourcePath = Path.Combine(
            fixtureRoot,
            "autofill-residual-source.xml");
        var autofillResidualHtml = File.ReadAllText(Path.Combine(
            fixtureRoot,
            "autofill-residual-android-reference.html"));
        var autofillResidualFile = LoadedFile.Load(repositoryRoot, autofillResidualSourcePath);
        autofillResidualFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
        var autofillResidualOwners = autofillResidualFile.Owners
            .Where(owner => owner.Placeholders.Count > 0)
            .ToList();
        Assert(autofillResidualOwners.Count == 6, "Autofill residual fixture owner count");
        var expectedAutofillMappings = new Dictionary<string, (string JavaPath, string Name, string Descriptor, bool UseFirstMeaningfulSummary)>(StringComparer.Ordinal)
        {
            ["M:Android.Service.Autofill.ImageTransformation.Builder.AddOption(Java.Util.Regex.Pattern,System.Int32,System.String)"] =
                ("android/service/autofill/ImageTransformation$Builder", "addOption", "(Ljava/util/regex/Pattern;ILjava/lang/CharSequence;)Landroid/service/autofill/ImageTransformation$Builder;", false),
            ["M:Android.Service.Autofill.SaveInfo.Builder.SetDescription(System.String)"] =
                ("android/service/autofill/SaveInfo$Builder", "setDescription", "(Ljava/lang/CharSequence;)Landroid/service/autofill/SaveInfo$Builder;", false),
            ["M:Android.Service.Autofill.ImageTransformation.Builder.AddOption(Java.Util.Regex.Pattern,System.Int32)"] =
                ("android/service/autofill/ImageTransformation$Builder", "addOption", "(Ljava/util/regex/Pattern;I)Landroid/service/autofill/ImageTransformation$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetInlinePresentation(Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/Dataset$Builder", "setInlinePresentation", "(Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetInlinePresentation(Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/Dataset$Builder", "setInlinePresentation", "(Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Android.Widget.RemoteViews)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Landroid/widget/RemoteViews;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern,Android.Widget.RemoteViews)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;Landroid/widget/RemoteViews;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;", true),
            ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews)"] =
                ("android/service/autofill/FillResponse$Builder", "setAuthentication", "([Landroid/view/autofill/AutofillId;Landroid/content/IntentSender;Landroid/widget/RemoteViews;)Landroid/service/autofill/FillResponse$Builder;", true),
            ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/FillResponse$Builder", "setAuthentication", "([Landroid/view/autofill/AutofillId;Landroid/content/IntentSender;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/FillResponse$Builder;", true),
            ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                ("android/service/autofill/FillResponse$Builder", "setAuthentication", "([Landroid/view/autofill/AutofillId;Landroid/content/IntentSender;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/FillResponse$Builder;", true),
        };
        Assert(
            expectedAutofillMappings.All(item =>
            {
                var mapping = SourceVerifiedMemberMappings.Resolve(item.Key);
                return mapping is not null &&
                    mapping.SourceRequest.JavaPath == item.Value.JavaPath &&
                    mapping.Registration.Name == item.Value.Name &&
                    mapping.Registration.Descriptor == item.Value.Descriptor &&
                    mapping.UseFirstMeaningfulSummary == item.Value.UseFirstMeaningfulSummary;
            }) &&
            SourceVerifiedMemberMappings.Resolve(
                "M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Service.Autofill.Presentations)") is null,
            "Autofill aliases and deprecated-summary mappings are exact");
        var autofillResidualPages = new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal);
        foreach (var sourceRequest in autofillResidualOwners
            .Select(owner => owner.SourceRequest)
            .Where(request => request is not null)
            .Cast<SourceRequest>())
        {
            autofillResidualPages[sourceRequest.Url] = SourceLoadResult.Success(
                SourcePage.Parse(sourceRequest, autofillResidualHtml));
        }
        var expectedAutofillSummaries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["M:Android.Service.Autofill.ImageTransformation.Builder.AddOption(Java.Util.Regex.Pattern,System.Int32,System.String)"] =
                "Adds an image option with a content description.",
            ["M:Android.Service.Autofill.SaveInfo.Builder.SetDescription(System.String)"] =
                "Sets the description displayed in the save UI.",
            ["M:Android.Service.Autofill.ImageTransformation.Builder.AddOption(Java.Util.Regex.Pattern,System.Int32)"] =
                "Adds an image option when the regular expression matches.",
            ["M:Android.Service.Autofill.Dataset.Builder.SetInlinePresentation(Android.Service.Autofill.InlinePresentation)"] =
                "Sets an inline presentation for the dataset.",
            ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue)"] =
                "Sets the value for an autofill field.",
            ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews)"] =
                "Sets authentication for the response.",
        };
        Assert(
            autofillResidualOwners.All(owner =>
                MapOwner(owner, autofillResidualPages).Docs?.Summary ==
                expectedAutofillSummaries[owner.Id]),
            "Autofill aliases and deprecated overloads import exact semantic summaries");
        var autofillAuthenticationOwner = autofillResidualOwners.Single(owner =>
            owner.Id == "M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(" +
                "Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews)");
        var autofillAuthenticationDocs = MapOwner(
            autofillAuthenticationOwner,
            autofillResidualPages).Docs!;
        var autofillAuthenticationRemarks = autofillAuthenticationOwner.Placeholders.Single(
            placeholder => placeholder.Name == "remarks");
        var autofillAuthenticationReplacement = ReplacementFor(
            autofillAuthenticationRemarks,
            autofillAuthenticationDocs);
        Assert(
            autofillAuthenticationReplacement.Remarks?.Select(paragraph => paragraph.Text).SequenceEqual(
                [
                    "This method was deprecated in API level 31.",
                    "Sets authentication for the response.",
                    "The authentication activity must return RESULT_OK with EXTRA_AUTHENTICATION_RESULT.",
                    "return authenticationResult;",
                    "Do not use an immutable pending intent.",
                ]) == true &&
            autofillAuthenticationReplacement.Remarks[3].IsCode,
            "Autofill fixture retains every source contract paragraph and code block");
        Assert(
            TryReplacePlaceholder(
                autofillResidualFile.Text,
                autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order],
                autofillAuthenticationRemarks,
                autofillAuthenticationReplacement,
                out var completedAutofillAuthenticationText,
                out _),
            "Autofill remarks fixture replacement succeeds");
        autofillResidualFile.UpdateBlockOffsets(
            autofillAuthenticationOwner.Order,
            completedAutofillAuthenticationText);
        var completedAutofillAuthenticationDocs = XElement.Parse(
            completedAutofillAuthenticationText[
                autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order].Start..
                autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order].End],
            LoadOptions.PreserveWhitespace);
        Assert(
            completedAutofillAuthenticationDocs.Element("remarks")!.Elements()
                .Select(element => element.Name.LocalName)
                .SequenceEqual(["para", "para", "para", "code", "para"]) &&
            completedAutofillAuthenticationDocs.Element("remarks")!.Element("code")?.Value ==
                "return authenticationResult;",
            "Autofill remarks fixture preserves paragraph and code order");
        var importerOwnedRefreshRemarks = new XElement("remarks");
        importerOwnedRefreshRemarks.Add(
            UsableRemarks(autofillAuthenticationDocs.Paragraphs)
                .Take(2)
                .Select(DocumentationElement));
        importerOwnedRefreshRemarks.Add(ImporterSourceReference(autofillAuthenticationDocs));
        importerOwnedRefreshRemarks.Add(
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        var importerOwnedRefreshBlock = new XElement(
            "Docs",
            new XElement("summary", "Existing fixture summary."),
            importerOwnedRefreshRemarks).ToString(SaveOptions.DisableFormatting);
        var completedAuthenticationBlock =
            autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order];
        var importerOwnedRefreshText =
            completedAutofillAuthenticationText[..completedAuthenticationBlock.Start] +
            importerOwnedRefreshBlock +
            completedAutofillAuthenticationText[completedAuthenticationBlock.End..];
        autofillResidualFile.UpdateBlockOffsets(
            autofillAuthenticationOwner.Order,
            importerOwnedRefreshText);
        var refreshedImporterOwnedRemarks = RefreshImporterOwnedRemarks(
            importerOwnedRefreshText,
            autofillResidualFile,
            autofillAuthenticationOwner,
            autofillAuthenticationDocs);
        autofillResidualFile.UpdateBlockOffsets(
            autofillAuthenticationOwner.Order,
            refreshedImporterOwnedRemarks.Text);
        var refreshedImporterOwnedBlock = refreshedImporterOwnedRemarks.Text[
            autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order].Start..
            autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order].End];
        var refreshedImporterOwnedElements = XElement.Parse(
            refreshedImporterOwnedBlock,
            LoadOptions.PreserveWhitespace).Element("remarks")!.Elements().ToList();
        Assert(
            IsPotentialImporterOwnedRemarks(
                importerOwnedRefreshRemarks,
                autofillAuthenticationDocs.SourceKind) &&
            refreshedImporterOwnedRemarks.Reason is null &&
            refreshedImporterOwnedElements
                .Take(refreshedImporterOwnedElements.Count - 2)
                .Select(element => element.Name.LocalName + ":" + element.Value)
                .SequenceEqual(
                    [
                        "para:This method was deprecated in API level 31.",
                        "para:Sets authentication for the response.",
                        "para:The authentication activity must return RESULT_OK with EXTRA_AUTHENTICATION_RESULT.",
                        "code:return authenticationResult;",
                        "para:Do not use an immutable pending intent.",
                    ]),
            "strict importer-owned remarks refreshes complete parsed source prose and code");
        var renderedImporterOwnedRemarks = RenderImporterOwnedRemarks(
            UsableRemarks(autofillAuthenticationDocs.Paragraphs),
            autofillAuthenticationDocs,
            "\n",
            "  ",
            "    ");
        var reloadedImporterOwnedRemarks = XElement.Parse(
            renderedImporterOwnedRemarks,
            LoadOptions.PreserveWhitespace);
        var renderedSourceReference = reloadedImporterOwnedRemarks.Elements()
            .ElementAt(reloadedImporterOwnedRemarks.Elements().Count() - 2)
            .ToString(SaveOptions.DisableFormatting);
        Assert(
            renderedImporterOwnedRemarks.Contains(
                "<format type=\"text/html\"><a href=",
                StringComparison.Ordinal) &&
            TryGetImporterSourceReferenceUrl(
                renderedSourceReference,
                out var renderedSourceUrl) &&
            UrlsEqual(renderedSourceUrl, autofillAuthenticationDocs.SourceUrl) &&
            IsPotentialImporterOwnedRemarks(
                reloadedImporterOwnedRemarks,
                autofillAuthenticationDocs.SourceKind),
            "rendered importer-owned remarks round-trip through structural ownership recognition");
        var legacyFormattedImporterOwnedRemarks = new XElement("remarks");
        foreach (var paragraph in UsableRemarks(autofillAuthenticationDocs.Paragraphs))
        {
            legacyFormattedImporterOwnedRemarks.Add(DocumentationElement(paragraph));
            if (paragraph.IsCode)
                legacyFormattedImporterOwnedRemarks.Add(DocumentationElement(paragraph));
        }
        var legacySourceReference = XElement.Parse(
            ImporterSourceReference(autofillAuthenticationDocs).ToString(),
            LoadOptions.PreserveWhitespace);
        legacyFormattedImporterOwnedRemarks.Add(legacySourceReference);
        legacyFormattedImporterOwnedRemarks.Add(
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        var legacyFormattedImporterOwnedBlock = new XElement(
            "Docs",
            new XElement("summary", "Existing fixture summary."),
            legacyFormattedImporterOwnedRemarks).ToString(SaveOptions.DisableFormatting);
        var legacyFormattedImporterOwnedText =
            completedAutofillAuthenticationText[..completedAuthenticationBlock.Start] +
            legacyFormattedImporterOwnedBlock +
            completedAutofillAuthenticationText[completedAuthenticationBlock.End..];
        autofillResidualFile.UpdateBlockOffsets(
            autofillAuthenticationOwner.Order,
            legacyFormattedImporterOwnedText);
        var refreshedLegacyFormattedImporterOwnedRemarks = RefreshImporterOwnedRemarks(
            legacyFormattedImporterOwnedText,
            autofillResidualFile,
            autofillAuthenticationOwner,
            autofillAuthenticationDocs);
        autofillResidualFile.UpdateBlockOffsets(
            autofillAuthenticationOwner.Order,
            refreshedLegacyFormattedImporterOwnedRemarks.Text);
        var refreshedLegacyFormattedRemarks = XElement.Parse(
            refreshedLegacyFormattedImporterOwnedRemarks.Text[
                autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order].Start..
                autofillResidualFile.DocsBlocks[autofillAuthenticationOwner.Order].End],
            LoadOptions.PreserveWhitespace).Element("remarks")!;
        Assert(
            IsPotentialImporterOwnedRemarks(
                legacyFormattedImporterOwnedRemarks,
                autofillAuthenticationDocs.SourceKind) &&
            HasLegacyFormattedSourceReference(legacySourceReference) &&
            refreshedLegacyFormattedImporterOwnedRemarks.Reason is null &&
            refreshedLegacyFormattedRemarks.Elements("code").Count() == 1 &&
            refreshedLegacyFormattedRemarks.Elements()
                .Any(element => TryGetImporterSourceReferenceUrl(
                    element.ToString(SaveOptions.DisableFormatting),
                    out _)),
            "legacy formatted importer-owned remarks regenerate nested code containers once");
        var authoredRefreshText = importerOwnedRefreshText.Replace(
            "Sets authentication for the response.",
            "Author-authored remarks are preserved.",
            StringComparison.Ordinal);
        autofillResidualFile.UpdateBlockOffsets(
            autofillAuthenticationOwner.Order,
            authoredRefreshText);
        var preservedAuthoredRemarks = RefreshImporterOwnedRemarks(
            authoredRefreshText,
            autofillResidualFile,
            autofillAuthenticationOwner,
            autofillAuthenticationDocs);
        Assert(
            preservedAuthoredRemarks.Reason == "existing_remarks_not_importer_owned" &&
            preservedAuthoredRemarks.Text.Equals(authoredRefreshText, StringComparison.Ordinal),
            "strict importer-owned remarks refresh preserves authored remarks");
        var autofillBuilderRequest = SourceRequest.Create(
            "android/service/autofill/CharSequenceTransformation$Builder") ??
            throw new InvalidOperationException("SELF-TEST FAIL: Autofill builder source request");
        var autofillBuilderPage = SourcePage.Parse(autofillBuilderRequest, autofillResidualHtml);
        Assert(
            autofillBuilderPage.Members.Single(member => member.Name == "addField")
                .Docs?.Parameters["regex"] ==
                "regular expression with groups (delimited by ( and )) that are used to substitute parts of the value.",
            "Autofill regex source repair");
        var eventRequest = SourceRequest.Create(
            "android/service/autofill/FillEventHistory$Event") ??
            throw new InvalidOperationException("SELF-TEST FAIL: Autofill event source request");
        var eventPage = SourcePage.Parse(eventRequest, autofillResidualHtml);
        var eventSourceText = eventPage.Members.Single(member =>
            member.Name == "TYPE_CONTEXT_COMMITTED").Docs?.Paragraphs.Single().Text;
        Assert(
            eventSourceText ==
                "The selected dataset was selected (getChangedFields());",
            "Autofill event source repair");
        Assert(
            IsMeaningfulChannel("The dataset field can be configured for a hint, and so on.", "remarks") &&
            IsMeaningfulChannel(
                "The id of the fill request this context corresponds to.",
                "value") &&
            !IsMeaningfulChannel(
                "The capability is intended for",
                "summary") &&
            IsMeaningfulChannel(
                "The capability is intended for.",
                "summary") &&
            !IsMeaningfulChannel(
                "The credential provider is populated in",
                "summary"),
            "complete punctuated terminal prepositions are retained while unpunctuated fragments skip");

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
        var fetchUriRequest = new SourceRequest(
            "android/adservices/customaudience/FetchAndJoinCustomAudienceRequest$Builder",
            "https://developer.android.com/reference/android/adservices/customaudience/FetchAndJoinCustomAudienceRequest.Builder",
            "android");
        var malformedFetchUriDocs = SourcePage.Parse(fetchUriRequest, androidHtml)
            .Members.Single(member => member.Name == "setFetchUri").Docs!;
        Assert(
            malformedFetchUriDocs.HasMalformedSourceMarkup &&
            ReplacementFor(
                new Placeholder(0, "summary", "", "summary"),
                malformedFetchUriDocs).Reason == "source_documentation_malformed",
            "malformed FetchAndJoinCustomAudienceRequest fetch URI source skip");
        var correctedFetchUriDocs = SourcePage.Parse(
            fetchUriRequest,
            androidHtml.Replace(" ()}", "", StringComparison.Ordinal))
            .Members.Single(member => member.Name == "setFetchUri").Docs!;
        Assert(
            !correctedFetchUriDocs.HasMalformedSourceMarkup &&
            ReplacementFor(
                new Placeholder(0, "summary", "", "summary"),
                correctedFetchUriDocs).Text ==
                "Sets the Uri from which the custom audience is to be fetched.",
            "corrected FetchAndJoinCustomAudienceRequest fetch URI source imports normally");
        var repeatedAndroidPage = SourcePage.Parse(
            request,
            File.ReadAllText(Path.Combine(
                fixtureRoot,
                "repeated-blocks-android-reference.html")));
        Assert(
            repeatedAndroidPage.Members.Single(member => member.Name == "setTitle").Docs?.Paragraphs
                .SequenceEqual(
                    [
                        new SourceParagraph("Repeated visible prose.", IsCode: false),
                        new SourceParagraph("Repeated visible prose.", IsCode: false),
                        new SourceParagraph("widget.setTitle(title);", IsCode: true),
                        new SourceParagraph("widget.setTitle(title);", IsCode: true),
                    ]) == true,
            "parsed Android source retains repeated visible prose and code blocks in order");
        var nestedCodeContainersAndroidPage = SourcePage.Parse(
            request,
            File.ReadAllText(Path.Combine(
                fixtureRoot,
                "nested-code-containers-android-reference.html")));
        Assert(
            nestedCodeContainersAndroidPage.Members.Single(member => member.Name == "setTitle").Docs?.Paragraphs
                .SequenceEqual(
                    [
                        new SourceParagraph("Nested code containers remain one sample.", IsCode: false),
                        new SourceParagraph("widget.setTitle(title);", IsCode: true),
                    ]) == true,
            "parsed Android source coalesces nested devsite and pre code containers");
        var repeatedJavaPage = SourcePage.Parse(
            new SourceRequest(
                "java/lang/String",
                JavaReference + "java.base/java/lang/String.html",
                "java"),
            File.ReadAllText(Path.Combine(
                fixtureRoot,
                "repeated-blocks-java-reference.html")));
        Assert(
            repeatedJavaPage.Members.Single(member => member.Name == "setTitle").Docs?.Paragraphs
                .Select(paragraph => paragraph.Text)
                .SequenceEqual(
                    [
                        "Repeated visible prose.",
                        "Repeated visible prose.",
                        "widget.setTitle(title);",
                        "widget.setTitle(title);",
                    ]) == true,
            "parsed Java source retains repeated visible blocks in order");
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
        const string cddlLeadIn =
            "If the implementation is feature version 202101 or later, " +
            "each X.509 certificate contains an X.509 extension at OID 1.3.6.1.4.1.11129.2.1.26 which " +
            "contains a DER encoded OCTET STRING with the bytes of the CBOR with the following CDDL:";
        var cddlParagraphs = SourcePage.ExtractParagraphs(
            "<p>" + cddlLeadIn +
            "<div></div><devsite-code><pre>ProofOfBinding = [\"ProofOfBinding\", bstr]</pre></devsite-code>" +
            "<p>This CBOR binds the issuer data to the credential.</p>");
        Assert(
            cddlParagraphs.SequenceEqual(
                [
                    new SourceParagraph(cddlLeadIn, IsCode: false),
                    new SourceParagraph("ProofOfBinding = [\"ProofOfBinding\", bstr]", IsCode: true),
                    new SourceParagraph("This CBOR binds the issuer data to the credential.", IsCode: false),
                ]) &&
                UsableRemarks(cddlParagraphs).SequenceEqual(cddlParagraphs),
            "Android CDDL code lead-ins retain their certificate metadata and trailing colon");
        var siblingCddlParagraphs = SourcePage.ExtractParagraphs(
            "<p>" + cddlLeadIn + "</p>" +
            "<pre>ProofOfBinding = [\"ProofOfBinding\", bstr]</pre>" +
            "<p>This CBOR binds the issuer data to the credential.</p>");
        Assert(
            siblingCddlParagraphs.SequenceEqual(cddlParagraphs) &&
                UsableRemarks(siblingCddlParagraphs).SequenceEqual(cddlParagraphs),
            "Android CDDL lead-ins precede sibling code blocks in source order");
        Assert(
            SourcePage.ExtractParagraphs("<p>" + cddlLeadIn + "</p>").Count == 0 &&
                SourcePage.ExtractParagraphs(
                    "<p>" + cddlLeadIn + "<pre> </pre></p>").Count == 0 &&
                SourcePage.ExtractParagraphs(
                    "<p>" + cddlLeadIn + "<p>Separate prose.</p><pre>schema = bstr</pre>")
                    .All(paragraph => paragraph.Text != cddlLeadIn) &&
                UsableRemarks([new SourceParagraph(cddlLeadIn, IsCode: false)]).Count == 0,
            "Android CDDL lead-ins require an immediately following nonempty code block");
        foreach (var separator in new[]
        {
            "<p>Separate prose.</p>",
            "<p>Unrelated incomplete prose:</p>",
            "<p></p>",
            "<pre> </pre>",
            "<devsite-code><pre> </pre></devsite-code>",
        })
        {
            Assert(
                SourcePage.ExtractParagraphs(
                    "<p>" + cddlLeadIn + "</p>" + separator + "<pre>schema = bstr</pre>")
                    .SequenceEqual(
                        separator == "<p>Separate prose.</p>"
                            ? [
                                new SourceParagraph("Separate prose.", IsCode: false),
                                new SourceParagraph("schema = bstr", IsCode: true),
                            ]
                            : [new SourceParagraph("schema = bstr", IsCode: true)]),
                "Android CDDL lead-ins cannot cross intervening parsed blocks: " + separator);
        }
        Assert(
            SourcePage.ExtractParagraphs("<p>" + cddlLeadIn + "</p><pre> </pre>").Count == 0 &&
                SourcePage.ExtractParagraphs(
                    "<p>" + cddlLeadIn + "</p><p><pre>schema = bstr</pre></p>")
                    .SequenceEqual(
                        [
                            new SourceParagraph(cddlLeadIn, IsCode: false),
                            new SourceParagraph("schema = bstr", IsCode: true),
                        ]),
            "Android CDDL sibling guards reject empty code but allow a code-only paragraph wrapper");
        Assert(
            SourcePage.ExtractParagraphs(
                "<p>This ordinary incomplete prose ends with a colon:<pre>schema = bstr</pre></p>")
                .SequenceEqual([new SourceParagraph("schema = bstr", IsCode: true)]),
            "ordinary incomplete Android prose before code blocks remains excluded");
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
        var cddlRefreshDocs = nestedExampleDocs with
        {
            Paragraphs =
            [
                new SourceParagraph("Sets the widget title.", IsCode: false),
                .. cddlParagraphs,
            ],
        };
        RemarksRefreshResult RefreshCddlRemarks(XElement remarks)
        {
            var text = $"<Docs>{remarks.ToString(SaveOptions.DisableFormatting)}</Docs>";
            var docs = XElement.Parse(text, LoadOptions.PreserveWhitespace);
            var owner = setTitle with { Order = 0, Docs = docs, Placeholders = [] };
            var refreshFile = new LoadedFile
            {
                Path = "Widget.Cddl.refresh.xml",
                RelativePath = "Widget.Cddl.refresh.xml",
                Text = text,
                Newline = "\n",
                HasUtf8Bom = false,
                Root = docs,
            };
            refreshFile.UpdateBlockOffsets(0, text);
            return RefreshImporterOwnedRemarks(text, refreshFile, owner, cddlRefreshDocs);
        }
        var partialCddlRemarks = new XElement(
            "remarks",
            cddlRefreshDocs.Paragraphs
                .Where(paragraph => paragraph.Text != cddlLeadIn)
                .Select(DocumentationElement),
            ImporterSourceReference(cddlRefreshDocs),
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        var refreshedCddlRemarks = RefreshCddlRemarks(partialCddlRemarks);
        var completeCddlRemarks = XElement.Parse(
            refreshedCddlRemarks.Text,
            LoadOptions.PreserveWhitespace).Element("remarks")!;
        Assert(
            refreshedCddlRemarks.Reason is null &&
                completeCddlRemarks.Elements().Take(4).Select(element => element.Value)
                    .SequenceEqual(cddlRefreshDocs.Paragraphs.Select(paragraph => paragraph.Text)),
            "importer-owned CDDL remarks restore the exact lead-in before the source sample");
        var repeatedCddlRefresh = RefreshCddlRemarks(completeCddlRemarks);
        var completeCddlText = $"<Docs>{completeCddlRemarks.ToString(SaveOptions.DisableFormatting)}</Docs>";
        Assert(
            repeatedCddlRefresh.Reason == "source_remarks_current" &&
                repeatedCddlRefresh.Text == completeCddlText,
            "complete source-ordered CDDL remarks refresh is idempotent");
        foreach (var authored in new XNode[]
        {
            new XText("Authored fixture guidance."),
            new XElement("c", "Sets the widget title."),
            new XCData("Sets the widget title."),
            new XComment("Authored fixture annotation."),
        })
        {
            var authoredRemarks = new XElement(partialCddlRemarks);
            authoredRemarks.Elements("para").First().ReplaceNodes(authored);
            var originalText = $"<Docs>{authoredRemarks.ToString(SaveOptions.DisableFormatting)}</Docs>";
            var preserved = RefreshCddlRemarks(authoredRemarks);
            Assert(
                preserved.Reason == "existing_remarks_not_importer_owned" &&
                    preserved.Text == originalText,
                "CDDL refresh preserves authored or mixed-content paragraphs: " + authored.NodeType);
        }
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
        var overlappingRemarks = XElement.Parse(
            "<remarks><para>To be added.</para><para>The exact JNI overload is required.</para></remarks>");
        var overlappingPlaceholder = Placeholder.Create(
            overlappingRemarks.Elements("para").First(),
            0);
        var overlappingRemarksReplacement = LimitOverlappingRemarksReplacement(
            overlappingPlaceholder,
            mappedDocs,
            ReplacementFor(
                overlappingPlaceholder,
                mappedDocs),
            overlappingRemarks);
        Assert(
            overlappingRemarksReplacement.Remarks?.Select(paragraph => paragraph.Text)
                .SequenceEqual([mappedDocs.Summary]) == true,
            "remarks placeholders omit only existing source prose");
        var existingSummaryRemarks = XElement.Parse(
            "<remarks><para>Sets the widget title.</para><para>To be added.</para></remarks>");
        var existingSummaryPlaceholder = Placeholder.Create(
            existingSummaryRemarks.Elements("para").Last(),
            0);
        var duplicateSummaryReplacement = LimitOverlappingRemarksReplacement(
            existingSummaryPlaceholder,
            mappedDocs,
            ReplacementFor(
                existingSummaryPlaceholder,
                mappedDocs),
            existingSummaryRemarks);
        Assert(
            duplicateSummaryReplacement.Remarks?.Select(paragraph => paragraph.Text)
                .SequenceEqual(["The exact JNI overload is required."]) == true,
            "remarks placeholders retain source prose not already documented");
        var inlineMarkupRemarks = XElement.Parse(
            "<remarks><para>Sets the widget <c>title</c>.</para><para>To be added.</para></remarks>");
        var inlineMarkupPlaceholder = Placeholder.Create(
            inlineMarkupRemarks.Elements("para").Last(),
            0);
        var inlineMarkupReplacement = LimitOverlappingRemarksReplacement(
            inlineMarkupPlaceholder,
            mappedDocs,
            ReplacementFor(
                inlineMarkupPlaceholder,
                mappedDocs),
            inlineMarkupRemarks);
        Assert(
            inlineMarkupReplacement.Remarks?.Select(paragraph => paragraph.Text)
                .SequenceEqual(["The exact JNI overload is required."]) == true,
            "remarks overlap recognizes punctuation-adjacent inline markup");
        var methodReferenceDocs = mappedDocs with
        {
            Paragraphs =
            [
                new SourceParagraph(
                    "Calls InputMethodService.onBindInput() when done. This method must be called from the main thread of your app.",
                    IsCode: false),
            ],
        };
        var methodReferenceRemarks = XElement.Parse(
            "<remarks><para>Calls <c>InputMethodService#onBindInput()</c> when done.</para><para>To be added.</para></remarks>");
        var methodReferencePlaceholder = Placeholder.Create(
            methodReferenceRemarks.Elements("para").Last(),
            0);
        var methodReferenceReplacement = LimitOverlappingRemarksReplacement(
            methodReferencePlaceholder,
            methodReferenceDocs,
            ReplacementFor(
                methodReferencePlaceholder,
                methodReferenceDocs),
            methodReferenceRemarks);
        Assert(
            methodReferenceReplacement.Remarks?.Select(paragraph => paragraph.Text)
                .SequenceEqual(
                    ["This method must be called from the main thread of your app."]) == true,
            "remarks overlap retains source guidance not represented by inline method references");
        const string methodReferenceDocsText =
            "<Docs><remarks><para>Calls <c>InputMethodService#onBindInput()</c> when done.</para><para>To be added.</para></remarks></Docs>";
        Assert(
            TryReplacePlaceholder(
                methodReferenceDocsText,
                new DocsBlock(0, 0, methodReferenceDocsText.Length),
                methodReferencePlaceholder,
                methodReferenceReplacement,
                out var appliedMethodReferenceText,
                out _) &&
            XDocument.Parse(appliedMethodReferenceText)
                .Root!
                .Element("remarks")!
                .Elements("para")
                .ToList() is [var preservedMethodReference, var importedThreadRequirement] &&
            preservedMethodReference.Value == "Calls InputMethodService#onBindInput() when done." &&
            preservedMethodReference.Element("c")?.Value == "InputMethodService#onBindInput()" &&
            importedThreadRequirement.Value ==
                "This method must be called from the main thread of your app.",
            "remarks overlap preserves inline method markup and imports independent source guidance");
        const string multilineMethodReferenceDocsText =
            "<Docs>\n  <remarks>\n    <para>Calls <c>InputMethodService#onBindInput()</c> when done.</para>\n    <para>To be added.</para>\n  </remarks>\n</Docs>";
        var multilineMethodReferenceRemarks = XElement.Parse(
            multilineMethodReferenceDocsText).Element("remarks")!;
        var multilineMethodReferencePlaceholder = Placeholder.Create(
            multilineMethodReferenceRemarks.Elements("para").Last(),
            0);
        var multilineMethodReferenceReplacement = LimitOverlappingRemarksReplacement(
            multilineMethodReferencePlaceholder,
            methodReferenceDocs,
            ReplacementFor(
                multilineMethodReferencePlaceholder,
                methodReferenceDocs),
            multilineMethodReferenceRemarks);
        Assert(
            TryReplacePlaceholder(
                multilineMethodReferenceDocsText,
                new DocsBlock(0, 0, multilineMethodReferenceDocsText.Length),
                multilineMethodReferencePlaceholder,
                multilineMethodReferenceReplacement,
                out var appliedMultilineMethodReferenceText,
                out _) &&
            appliedMultilineMethodReferenceText.Contains(
                "\n    <para>This method must be called from the main thread of your app.</para>",
                StringComparison.Ordinal) &&
            !appliedMultilineMethodReferenceText.Contains(
                "\n        <para>This method must be called from the main thread of your app.</para>",
                StringComparison.Ordinal),
            "multiline remarks replacements preserve the placeholder indentation");
        Assert(
            SourcePage.ExtractParagraphs(
                "<p><p>Calls <code>InputMethodService.onBindInput()</code> when done.</p>.<br>This method must be called from the main thread of your app.</p></p>")
                .Select(paragraph => paragraph.Text)
                .SequenceEqual(
                    [
                        "Calls InputMethodService.onBindInput() when done.",
                        "This method must be called from the main thread of your app.",
                    ]),
            "malformed Android method markup retains independent main-thread guidance");
        var unsafeInlineMarkup = XElement.Parse(
            "<para>Sets the <c>widget</c> title.<!-- authored comment --></para>");
        Assert(
            MatchingSourceFragmentIndexes(
                unsafeInlineMarkup,
                ExpandRemarksFragments(mappedDocs.Paragraphs)).Count == 0,
            "remarks overlap does not treat comments as source prose");
        var partialOverlapDocs = mappedDocs with
        {
            Paragraphs =
            [
                new SourceParagraph(
                    "The first contract sentence. The second contract sentence. The third contract sentence.",
                    IsCode: false),
            ],
        };
        var partialOverlapRemarks = XElement.Parse(
            "<remarks><para>The first contract sentence.</para><para>To be added.</para></remarks>");
        var partialOverlapPlaceholder = Placeholder.Create(
            partialOverlapRemarks.Elements("para").Last(),
            0);
        var partialOverlapReplacement = LimitOverlappingRemarksReplacement(
            partialOverlapPlaceholder,
            partialOverlapDocs,
            ReplacementFor(
                partialOverlapPlaceholder,
                partialOverlapDocs),
            partialOverlapRemarks);
        Assert(
            partialOverlapReplacement.Remarks?.Select(paragraph => paragraph.Text)
                .SequenceEqual(
                    ["The second contract sentence.", "The third contract sentence."]) == true,
            "remarks overlap retains later source sentences after existing leading prose");
        var noPeriodPlaceholderRemarks = XElement.Parse(
            "<remarks><para>To be added</para></remarks>");
        var noPeriodPlaceholder = Placeholder.Create(
            noPeriodPlaceholderRemarks.Element("para")!,
            0);
        var noPeriodPlaceholderDocs = mappedDocs with
        {
            Summary = "This documentation is to be added when the fixture is ready.",
            Paragraphs =
            [
                new SourceParagraph(
                    "This documentation is to be added when the fixture is ready.",
                    IsCode: false),
            ],
        };
        var noPeriodPlaceholderReplacement = LimitOverlappingRemarksReplacement(
            noPeriodPlaceholder,
            noPeriodPlaceholderDocs,
            ReplacementFor(
                noPeriodPlaceholder,
                noPeriodPlaceholderDocs),
            noPeriodPlaceholderRemarks);
        Assert(
            noPeriodPlaceholderReplacement.Text == noPeriodPlaceholderDocs.Summary,
            "no-period remarks placeholders do not trigger overlap detection");
        var copiedDescriptionRepairDocs = mappedDocs with
        {
            SourceUrl =
                "https://developer.android.com/reference/android/example/Widget#setTitle(java.lang.String)",
        };
        const string copiedDescriptionCdata =
            "<![CDATA[Example XML: <para>Description copied from interface: Fixture</para>]]>";
        const string copiedDescriptionComment =
            "<!-- Example XML: <para>Description copied from interface: Fixture</para> -->";
        const string copiedDescriptionProcessingInstruction =
            "<?fixture Example XML: <para>Description copied from interface: Fixture</para> ?>";
        var copiedDescriptionRepairText = file.Text.Replace(
            "<summary>To be added.</summary>",
            "<summary>Description copied from interface: Fixture</summary>",
            StringComparison.Ordinal).Replace(
            $"<remarks>{file.Newline}          <para>Keep this existing prose.</para>",
            $"<remarks>{file.Newline}          {copiedDescriptionCdata}{file.Newline}          {copiedDescriptionComment}{file.Newline}          {copiedDescriptionProcessingInstruction}{file.Newline}          <para>Description copied from interface: Fixture</para>{file.Newline}          <para>Keep this existing prose.</para>{file.Newline}          <para><format type=\"text/html\"><a href=\"{copiedDescriptionRepairDocs.SourceUrl}\" title=\"Reference documentation\">Android reference for <code>android.example.Widget.setTitle</code>.</a></format></para>",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, copiedDescriptionRepairText);
        var copiedDescriptionRepairOwner = setTitle with
        {
            Docs = XElement.Parse(
                copiedDescriptionRepairText[
                    file.DocsBlocks[setTitle.Order].Start..
                    file.DocsBlocks[setTitle.Order].End],
                LoadOptions.PreserveWhitespace),
        };
        Assert(
            IsImporterCopiedDescriptionLabel("Description copied from interface: Fixture"),
            "copied-description repair label is detected");
        var mismatchedCopiedDescriptionRepair = RepairCopiedDescriptionLabels(
            copiedDescriptionRepairText,
            file,
            copiedDescriptionRepairOwner,
            copiedDescriptionRepairDocs with { SourceUrl = "https://example.invalid/Widget" });
        Assert(
            mismatchedCopiedDescriptionRepair.Targets.Count == 0 &&
                mismatchedCopiedDescriptionRepair.Skips.Count == 0 &&
                mismatchedCopiedDescriptionRepair.Text.Equals(
                    copiedDescriptionRepairText,
                    StringComparison.Ordinal),
            "copied-description repairs require the exact resolved source URL");
        var authoredSourceReferenceText = copiedDescriptionRepairText.Replace(
            "Android reference for <code>android.example.Widget.setTitle</code>.",
            "Authored reference for <code>android.example.Widget.setTitle</code>.",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, authoredSourceReferenceText);
        var authoredSourceReferenceRepair = RepairCopiedDescriptionLabels(
            authoredSourceReferenceText,
            file,
            copiedDescriptionRepairOwner,
            copiedDescriptionRepairDocs);
        Assert(
            authoredSourceReferenceRepair.Targets.Count == 0 &&
                authoredSourceReferenceRepair.Skips.Count == 0 &&
                authoredSourceReferenceRepair.Text.Equals(
                    authoredSourceReferenceText,
                    StringComparison.Ordinal),
            "copied-description repairs require an importer-owned source reference");
        var authoredCopiedDescriptionText = copiedDescriptionRepairText.Replace(
            "Description copied from interface: Fixture",
            "Description copied from interface: Fixture — keep this authored prose.",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, authoredCopiedDescriptionText);
        var authoredCopiedDescriptionRepair = RepairCopiedDescriptionLabels(
            authoredCopiedDescriptionText,
            file,
            copiedDescriptionRepairOwner,
            copiedDescriptionRepairDocs);
        Assert(
            authoredCopiedDescriptionRepair.Targets.Count == 0 &&
                authoredCopiedDescriptionRepair.Skips.Count == 0 &&
                authoredCopiedDescriptionRepair.Text.Equals(
                    authoredCopiedDescriptionText,
                    StringComparison.Ordinal),
            "copied-description repairs preserve labels with authored prose");
        file.UpdateBlockOffsets(setTitle.Order, copiedDescriptionRepairText);
        var correspondenceMismatchOwner = copiedDescriptionRepairOwner with
        {
            Docs = XElement.Parse(
                copiedDescriptionRepairOwner.Docs
                    .ToString(SaveOptions.DisableFormatting)
                    .Replace(
                        "Keep this existing prose.",
                        "Different parser ownership structure.",
                        StringComparison.Ordinal),
                LoadOptions.PreserveWhitespace),
        };
        var correspondenceMismatchRepair = RepairCopiedDescriptionLabels(
            copiedDescriptionRepairText,
            file,
            correspondenceMismatchOwner,
            copiedDescriptionRepairDocs);
        var correspondenceMismatchReport = new ImportReport
        {
            Mode = "dry-run",
            Offline = true,
            MaxChanges = 2,
        };
        ReportCopiedDescriptionRepairSkips(
            correspondenceMismatchReport,
            file,
            correspondenceMismatchOwner,
            copiedDescriptionRepairDocs.SourceUrl,
            correspondenceMismatchRepair.Skips);
        Assert(
            correspondenceMismatchRepair.Targets.Count == 0 &&
                correspondenceMismatchRepair.Text.Equals(
                    copiedDescriptionRepairText,
                    StringComparison.Ordinal) &&
                correspondenceMismatchReport.Entries.Count == 2 &&
                correspondenceMismatchReport.Entries.All(entry =>
                    entry.Reason == "copied_description_target_not_located" &&
                    entry.SourceUrl == copiedDescriptionRepairDocs.SourceUrl) &&
                correspondenceMismatchReport.Entries.Select(entry => entry.Target)
                    .SequenceEqual(["summary", "remarks"]),
            "copied-description correspondence mismatches safely skip and report each target");
        var repairedCopiedDescriptionText = RepairCopiedDescriptionLabels(
            copiedDescriptionRepairText,
            file,
            copiedDescriptionRepairOwner,
            copiedDescriptionRepairDocs);
        var repairedCopiedDescriptionDocs = XDocument.Parse(
            repairedCopiedDescriptionText.Text,
            LoadOptions.PreserveWhitespace).Root!
            .Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "SetTitle")
            .Element("Docs")!;
        var copiedDescriptionCount = Regex.Matches(
            copiedDescriptionRepairText,
            "Description copied from interface:",
            RegexOptions.CultureInvariant).Count;
        var repairedCopiedDescriptionCount = Regex.Matches(
            repairedCopiedDescriptionText.Text,
            "Description copied from interface:",
            RegexOptions.CultureInvariant).Count;
        var repairedCopiedDescriptionSummary =
            repairedCopiedDescriptionDocs.Element("summary")?.Value ==
            copiedDescriptionRepairDocs.Summary;
        var repairedCopiedDescriptionRemarks =
            !repairedCopiedDescriptionDocs.Element("remarks")!.Elements("para")
                .Any(IsImporterCopiedDescriptionElement);
        var preservedCopiedDescriptionCdata = repairedCopiedDescriptionText.Text.Contains(
            copiedDescriptionCdata,
            StringComparison.Ordinal);
        var preservedCopiedDescriptionComment = repairedCopiedDescriptionText.Text.Contains(
            copiedDescriptionComment,
            StringComparison.Ordinal);
        var preservedCopiedDescriptionProcessingInstruction =
            repairedCopiedDescriptionText.Text.Contains(
                copiedDescriptionProcessingInstruction,
                StringComparison.Ordinal);
        Assert(
            repairedCopiedDescriptionText.Targets.Count == 2 &&
                repairedCopiedDescriptionText.Skips.Count == 0 &&
                repairedCopiedDescriptionSummary &&
                repairedCopiedDescriptionRemarks &&
                preservedCopiedDescriptionCdata &&
                preservedCopiedDescriptionComment &&
                preservedCopiedDescriptionProcessingInstruction &&
                repairedCopiedDescriptionCount == copiedDescriptionCount - 2,
            $"parser-identified copied-description summary and remarks targets are repaired while CDATA, comments, and processing instructions are preserved (targets={repairedCopiedDescriptionText.Targets.Count}, summary={repairedCopiedDescriptionSummary} [{repairedCopiedDescriptionDocs.Element("summary")?.Value}/{copiedDescriptionRepairDocs.Summary}], remarks={repairedCopiedDescriptionRemarks}, cdata={preservedCopiedDescriptionCdata}, comment={preservedCopiedDescriptionComment}, processingInstruction={preservedCopiedDescriptionProcessingInstruction}, labels={copiedDescriptionCount}/{repairedCopiedDescriptionCount})");
        var copiedDescriptionParagraphs = $"{file.Newline}          " +
            "<para>Description copied from interface: Fixture</para>" +
            $"{file.Newline}          <para>Keep this existing prose.</para>";
        void AssertMixedCopiedDescriptionRepair(
            string replacement,
            string collapsedText,
            string description)
        {
            var mixedText = copiedDescriptionRepairText.Replace(
                copiedDescriptionParagraphs,
                $"{file.Newline}          {replacement}{file.Newline}          " +
                "<para>Keep this existing prose.</para>",
                StringComparison.Ordinal);
            file.UpdateBlockOffsets(setTitle.Order, mixedText);
            var mixedOwner = copiedDescriptionRepairOwner with
            {
                Docs = XElement.Parse(
                    mixedText[
                        file.DocsBlocks[setTitle.Order].Start..
                        file.DocsBlocks[setTitle.Order].End],
                    LoadOptions.PreserveWhitespace),
            };
            var repair = RepairCopiedDescriptionLabels(
                mixedText,
                file,
                mixedOwner,
                copiedDescriptionRepairDocs);
            var report = new ImportReport
            {
                Mode = "apply",
                Offline = true,
                MaxChanges = 2,
            };
            ReportCopiedDescriptionRepairSkips(
                report,
                file,
                mixedOwner,
                copiedDescriptionRepairDocs.SourceUrl,
                repair.Skips);
            var remarks = XDocument.Parse(
                repair.Text,
                LoadOptions.PreserveWhitespace).Root!
                .Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "SetTitle")
                .Element("Docs")!.Element("remarks")!;
            Assert(
                repair.Targets.SequenceEqual(["summary"]) &&
                    repair.Skips.Count == 1 &&
                    repair.Skips[0].Target == "remarks" &&
                    repair.Skips[0].Reason == "copied_description_mixed_content" &&
                    report.Entries.Count == 1 &&
                    report.Entries[0] is
                    {
                        Status: "skipped",
                        Target: "remarks",
                        Reason: "copied_description_mixed_content",
                    } &&
                    repair.Text.Contains(replacement, StringComparison.Ordinal) &&
                    !repair.Text.Contains(collapsedText, StringComparison.Ordinal) &&
                    remarks.Value.Contains(
                        "Description copied from interface: Fixture",
                        StringComparison.Ordinal),
                description);
        }
        AssertMixedCopiedDescriptionRepair(
            "Before <para>Description copied from interface: Fixture</para>After",
            "BeforeAfter",
            "two-sided mixed copied-description paragraphs are reported and preserved");
        AssertMixedCopiedDescriptionRepair(
            "Before <para>Description copied from interface: Fixture</para><para>After</para>",
            "Before<para>After</para>",
            "left-only mixed copied-description paragraphs preserve rendered separators");
        AssertMixedCopiedDescriptionRepair(
            "<para>Before</para><para>Description copied from interface: Fixture</para>After",
            "<para>Before</para>After",
            "right-only mixed copied-description paragraphs preserve rendered separators");
        void AssertSeparatedCopiedDescriptionRepair(
            string replacement,
            bool repairsRemarks,
            string description,
            string? preservedMarkup = null)
        {
            var mixedText = copiedDescriptionRepairText.Replace(
                copiedDescriptionParagraphs,
                $"{file.Newline}          {replacement}{file.Newline}          " +
                "<para>Keep this existing prose.</para>",
                StringComparison.Ordinal);
            file.UpdateBlockOffsets(setTitle.Order, mixedText);
            var mixedOwner = copiedDescriptionRepairOwner with
            {
                Docs = XElement.Parse(
                    mixedText[
                        file.DocsBlocks[setTitle.Order].Start..
                        file.DocsBlocks[setTitle.Order].End],
                    LoadOptions.PreserveWhitespace),
            };
            var originalRemarks = mixedOwner.Docs.Element("remarks")!;
            var repair = RepairCopiedDescriptionLabels(
                mixedText,
                file,
                mixedOwner,
                copiedDescriptionRepairDocs);
            var report = new ImportReport
            {
                Mode = "apply",
                Offline = true,
                MaxChanges = 2,
            };
            foreach (var target in repair.Targets)
            {
                report.Entries.Add(ReportEntry.Changed(
                    "would_apply",
                    file.RelativePath,
                    mixedOwner.Id,
                    target,
                    copiedDescriptionRepairDocs.SourceUrl,
                    "importer_copied_description_repair",
                    "Replaced an exact importer-generated Javadoc copied-description label."));
            }
            ReportCopiedDescriptionRepairSkips(
                report,
                file,
                mixedOwner,
                copiedDescriptionRepairDocs.SourceUrl,
                repair.Skips);
            var repairedRemarks = XDocument.Parse(
                repair.Text,
                LoadOptions.PreserveWhitespace).Root!
                .Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "SetTitle")
                .Element("Docs")!.Element("remarks")!;
            var repairMatches = repairsRemarks
                ? repair.Targets.SequenceEqual(["summary", "remarks"]) &&
                  repair.Skips.Count == 0 &&
                  report.Entries.Count == 2 &&
                  report.Entries.All(entry => entry.Status == "would_apply") &&
                  !repairedRemarks.Elements("para")
                      .Any(IsImporterCopiedDescriptionElement) &&
                  repairedRemarks.Value.Contains("Before After", StringComparison.Ordinal)
                : repair.Targets.SequenceEqual(["summary"]) &&
                  repair.Skips is [{ Target: "remarks", Reason: "copied_description_mixed_content" }] &&
                  report.Entries.Count == 2 &&
                  report.Entries[0].Status == "would_apply" &&
                  report.Entries[1] is
                  {
                      Status: "skipped",
                      Target: "remarks",
                      Reason: "copied_description_mixed_content",
                  } &&
                  XNode.DeepEquals(originalRemarks, repairedRemarks);
            Assert(
                repairMatches &&
                    (preservedMarkup is null ||
                     repair.Text.Contains(preservedMarkup, StringComparison.Ordinal)),
                description);
        }
        AssertSeparatedCopiedDescriptionRepair(
            "<c>Before</c> <para>Description copied from interface: Fixture</para><c>After</c>",
            repairsRemarks: true,
            "copied-description repairs preserve a left inline-element word separator and report both repairs");
        AssertSeparatedCopiedDescriptionRepair(
            "<c>Before</c><para>Description copied from interface: Fixture</para> <c>After</c>",
            repairsRemarks: true,
            "copied-description repairs preserve a right inline-element word separator and report both repairs");
        AssertSeparatedCopiedDescriptionRepair(
            "<c>Before</c><!-- before copied label --> <para>Description copied from interface: Fixture</para><c>After</c>",
            repairsRemarks: true,
            "copied-description repairs preserve a comment-left word separator and report both repairs",
            "<!-- before copied label -->");
        AssertSeparatedCopiedDescriptionRepair(
            "<c>Before</c><para>Description copied from interface: Fixture</para><!-- after copied label --> <c>After</c>",
            repairsRemarks: true,
            "copied-description repairs preserve a comment-right word separator and report both repairs",
            "<!-- after copied label -->");
        AssertSeparatedCopiedDescriptionRepair(
            "<c>Before</c><!-- no separator --><para>Description copied from interface: Fixture</para><c>After</c>",
            repairsRemarks: false,
            "copied-description repairs report and preserve unseparated inline content",
            "<!-- no separator -->");
        var repairOnlyOwner = copiedDescriptionRepairOwner with { Placeholders = [] };
        Assert(
            RequiresSourceLoad(file, repairOnlyOwner),
            "copied-description repair-only owners load their source page");
        Assert(
            !HasCopiedDescriptionRepairCandidate(
                XElement.Parse(
                    "<Docs><summary>Description copied from interface: Fixture</summary></Docs>")),
            "copied-description repairs require importer source metadata");
        Assert(
            !IsImporterCopiedDescriptionLabel(
                "Description copied from interface: Fixture — keep this note."),
            "copied-description repair preserves labels with authored prose");
        var summaryOnlyRepairFailure = new ImportReport
        {
            Mode = "dry-run",
            Offline = true,
            MaxChanges = 1,
        };
        var summaryOnlyCopiedDescriptionText = copiedDescriptionRepairText.Replace(
            $"          <para>Description copied from interface: Fixture</para>{file.Newline}          <para>Keep this existing prose.</para>",
            $"          <para>Keep this existing prose.</para>",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, summaryOnlyCopiedDescriptionText);
        var summaryOnlyRepairOwner = setTitle with
        {
            Docs = XElement.Parse(
                summaryOnlyCopiedDescriptionText[
                    file.DocsBlocks[setTitle.Order].Start..
                    file.DocsBlocks[setTitle.Order].End],
                LoadOptions.PreserveWhitespace),
            Placeholders = [],
        };
        Assert(
            ReportMappingFailure(
                summaryOnlyRepairFailure,
                file,
                summaryOnlyRepairOwner,
                MappingResult.Skip("source_not_loaded", "fixture mapping failure")) &&
                summaryOnlyRepairFailure.Entries.Select(entry => entry.Target).SequenceEqual(["summary"]),
            "summary-only copied-description failures report only summary");
        var remarksOnlyCopiedDescriptionText = copiedDescriptionRepairText.Replace(
            "<summary>Description copied from interface: Fixture</summary>",
            "<summary>Existing fixture prose.</summary>",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, remarksOnlyCopiedDescriptionText);
        var remarksOnlyRepairOwner = setTitle with
        {
            Docs = XElement.Parse(
                remarksOnlyCopiedDescriptionText[
                    file.DocsBlocks[setTitle.Order].Start..
                    file.DocsBlocks[setTitle.Order].End],
                LoadOptions.PreserveWhitespace),
            Placeholders = [],
        };
        var remarksOnlyRepairFailure = new ImportReport
        {
            Mode = "dry-run",
            Offline = true,
            MaxChanges = 1,
        };
        Assert(
            ReportMappingFailure(
                remarksOnlyRepairFailure,
                file,
                remarksOnlyRepairOwner,
                MappingResult.Skip("source_not_loaded", "fixture mapping failure")) &&
                remarksOnlyRepairFailure.Entries.Select(entry => entry.Target).SequenceEqual(["remarks"]),
            "remarks-only copied-description failures report only remarks");
        var javaExampleDocs = mappedDocs with
        {
            SourceUrl = "https://developer.android.com/reference/android/hardware/camera2/CaptureRequest.Builder#setTitle(java.lang.String)",
            SourceLabel = "android.hardware.camera2.CaptureRequest.Builder.setTitle",
            SourceKind = "android",
        };
        var mappedSourceKind = mappedDocs.SourceKind == "android" ? "Android" : "Java";
        var knownJavaExampleDocs = javaExampleDocs with
        {
            SourceUrl = KnownJavaExampleRepairs[0].SourceUrl,
            SourceLabel = "java.time.temporal.TemporalField.adjustInto",
            SourceKind = "java",
        };
        var knownJavaExampleMarkup = new XElement(
            "Docs",
            new XElement(
                "remarks",
                new XElement(
                    "code",
                    new XAttribute("lang", "text/java"),
                    KnownJavaExampleRepairs[0].IncompleteCode),
                ImporterSourceReference(knownJavaExampleDocs)));
        Assert(
            FindKnownJavaExampleRepair(
                knownJavaExampleMarkup,
                knownJavaExampleDocs) == KnownJavaExampleRepairs[0],
            "exact known Java examples are eligible for parameter repairs");
        knownJavaExampleMarkup.Element("remarks")!.Element("code")!.Value =
            KnownJavaExampleRepairs[0].IncompleteCode.Replace(
                "temporal.with(thisField);",
                "temporal.with(otherField);",
                StringComparison.Ordinal);
        Assert(
            FindKnownJavaExampleRepair(
                knownJavaExampleMarkup,
                knownJavaExampleDocs) is null,
            "Java example repairs preserve altered importer-like code");
        knownJavaExampleMarkup.Element("remarks")!.Element("code")!.Value =
            KnownJavaExampleRepairs[0].IncompleteCode.Replace(
                "\n",
                "\n  ",
                StringComparison.Ordinal);
        Assert(
            FindKnownJavaExampleRepair(
                knownJavaExampleMarkup,
                knownJavaExampleDocs) is null,
            "Java example repairs preserve whitespace-modified code");
        knownJavaExampleMarkup.Element("remarks")!.Element("code")!.Value =
            KnownJavaExampleRepairs[0].CorrectCode;
        Assert(
            FindKnownJavaExampleRepair(
                knownJavaExampleMarkup,
                knownJavaExampleDocs) is null,
            "correct Java examples remain idempotent");
        var zoneTransitionRepair = KnownJavaExampleRepairs.Single(repair =>
            repair.MemberId == "M:Java.Time.Zone.ZoneRules.GetTransition(Java.Time.LocalDateTime)");
        var zoneTransitionDocs = javaExampleDocs with
        {
            SourceUrl = zoneTransitionRepair.SourceUrl,
            SourceLabel = "java.time.zone.ZoneRules.getTransition",
            SourceKind = "java",
            Paragraphs =
            [
                new SourceParagraph("One technique, using this method, would be:", IsCode: false),
                new SourceParagraph(zoneTransitionRepair.IncompleteCode, IsCode: true),
            ],
        };
        var zoneTransitionMarkup = new XElement(
            "Docs",
            XElement.Parse(RenderImporterOwnedRemarks(
                UsableRemarks(zoneTransitionDocs.Paragraphs),
                zoneTransitionDocs,
                "\n",
                "",
                "")));
        Assert(
            FindKnownJavaExampleRepair(
                zoneTransitionMarkup,
                zoneTransitionDocs,
                zoneTransitionRepair.MemberId) == zoneTransitionRepair,
            "the exact ZoneRules member, source, and complete importer-owned example allow the receiver typo repair");
        Assert(
            FindKnownJavaExampleRepair(zoneTransitionMarkup, zoneTransitionDocs) is null &&
            FindKnownJavaExampleRepair(
                zoneTransitionMarkup,
                zoneTransitionDocs,
                zoneTransitionRepair.MemberId + ".Altered") is null &&
            FindKnownJavaExampleRepair(
                zoneTransitionMarkup,
                zoneTransitionDocs with { SourceUrl = zoneTransitionDocs.SourceUrl + ".Altered" },
                zoneTransitionRepair.MemberId) is null &&
            FindKnownJavaExampleRepair(
                zoneTransitionMarkup,
                zoneTransitionDocs with
                {
                    Paragraphs = [new SourceParagraph("Different source prose.", IsCode: false)],
                },
                zoneTransitionRepair.MemberId) is null,
            "the ZoneRules repair rejects missing or mismatched members, URLs, and source structure");
        Action<XElement>[] authoredZoneTransitionChanges =
        [
            docs => docs.Element("remarks")!.AddFirst(new XElement("para", "Authored prose.")),
            docs => docs.Element("remarks")!.AddFirst(new XComment("Authored comment.")),
            docs => docs.Element("remarks")!.Element("code")!.Add(new XElement("c", "authored")),
            docs => docs.Element("remarks")!.Element("code")!.ReplaceNodes(
                new XCData(zoneTransitionRepair.IncompleteCode)),
            docs => docs.Element("remarks")!.Element("code")!.SetAttributeValue("authored", "true"),
            docs => docs.Element("remarks")!.Element("code")!.Value =
                zoneTransitionRepair.IncompleteCode.Replace("rule.getOffset", "other.getOffset", StringComparison.Ordinal),
            docs => docs.Element("remarks")!.Element("code")!.Value =
                zoneTransitionRepair.IncompleteCode.Replace("\n", "\n  ", StringComparison.Ordinal),
            docs => docs.Element("remarks")!.Add(ImporterSourceReference(zoneTransitionDocs)),
        ];
        Assert(
            authoredZoneTransitionChanges.All(change =>
            {
                var authored = new XElement(zoneTransitionMarkup);
                change(authored);
                var before = new XElement(authored);
                return FindKnownJavaExampleRepair(
                    authored,
                    zoneTransitionDocs,
                    zoneTransitionRepair.MemberId) is null &&
                    XNode.DeepEquals(authored, before);
            }),
            "the ZoneRules repair preserves authored prose, comments, markup, CDATA, code changes, whitespace, and duplicate references");
        zoneTransitionMarkup.Element("remarks")!.Element("code")!.Value =
            zoneTransitionRepair.CorrectCode;
        Assert(
            FindKnownJavaExampleRepair(
                zoneTransitionMarkup,
                zoneTransitionDocs,
                zoneTransitionRepair.MemberId) is null,
            "the corrected ZoneRules example is idempotent");
        var knownJavaProseDocs = javaExampleDocs with
        {
            SourceUrl = KnownJavaProseRepairs[0].SourceUrl,
            SourceLabel = "java.time.temporal.ChronoUnit.ERAS",
            SourceKind = "java",
        };
        var knownJavaProseMarkup = new XElement(
            "Docs",
            new XElement(
                "remarks",
                new XElement("para", KnownJavaProseRepairs[0].IncorrectText),
                ImporterSourceReference(knownJavaProseDocs),
                XElement.Parse($"<para>{AndroidAttribution}</para>")));
        Assert(
            FindKnownJavaProseRepair(
                knownJavaProseMarkup,
                knownJavaProseDocs) == KnownJavaProseRepairs[0],
            "exact known Java prose is eligible for Android-verified correction");
        var authoredJavaProseMarkup = new XElement(knownJavaProseMarkup);
        authoredJavaProseMarkup.Element("remarks")!.AddFirst(
            new XText("Keep this authored prose."));
        Assert(
            FindKnownJavaProseRepair(
                authoredJavaProseMarkup,
                knownJavaProseDocs) is null,
            "Java prose repairs preserve direct authored prose");
        var encodedJavaProseDocs = knownJavaProseDocs with
        {
            SourceUrl = KnownJavaProseRepairs[0].SourceUrl.Replace(
                "#",
                "%23",
                StringComparison.Ordinal),
        };
        var encodedJavaProseMarkup = new XElement(
            "Docs",
            new XElement(
                "remarks",
                new XElement("para", KnownJavaProseRepairs[0].IncorrectText),
                ImporterSourceReference(encodedJavaProseDocs),
                XElement.Parse($"<para>{AndroidAttribution}</para>")));
        Assert(
            FindKnownJavaProseRepair(
                encodedJavaProseMarkup,
                encodedJavaProseDocs) is null,
            "Java prose repairs require an exact source URL");
        knownJavaProseMarkup.Element("remarks")!.Element("para")!.Value =
            KnownJavaProseRepairs[0].CorrectText;
        Assert(
            FindKnownJavaProseRepair(
                knownJavaProseMarkup,
                knownJavaProseDocs) is null,
            "corrected Java prose remains idempotent");
        var dreamFocusRequest = SourceRequest.Create("android/service/dreams/DreamService")!;
        var dreamFocusHtml =
            "<h3 class=\"api-name\" id=\"onWindowFocusChanged(boolean)\">onWindowFocusChanged</h3>" +
            "<p>This hook is called whenever the window focus changes. See " +
            $"<code>{StaleDreamFocusSourceLink}</code> for more information.</p>";
        var dreamFocusDocs = SourcePage.Parse(dreamFocusRequest, dreamFocusHtml)
            .Members.Single().Docs!;
        Assert(
            dreamFocusDocs.Paragraphs is [{ Text: CorrectDreamFocusRemark }] &&
            ReplacementFor(new Placeholder(0, "remarks", "", "remarks"), dreamFocusDocs)
                .Remarks is [{ Text: CorrectDreamFocusRemark }],
            "DreamService focus imports the exact official link target instead of its stale label");
        Assert(
            SourcePage.Parse(
                SourceRequest.Create("android/example/Widget")!,
                dreamFocusHtml).Members.Single().Docs!.Paragraphs[0].Text ==
                IncorrectDreamFocusRemark &&
            SourcePage.Parse(
                dreamFocusRequest,
                dreamFocusHtml.Replace(
                    "#onWindowFocusChanged(boolean)\">",
                    "#other(boolean)\">",
                    StringComparison.Ordinal)).Members.Single().Docs!.Paragraphs[0].Text ==
                IncorrectDreamFocusRemark,
            "focus source-label correction requires the exact declaring source and hyperlink target");
        var dreamFocusMarkup = new XElement(
            "Docs",
            new XElement("summary", "Retain this authored summary."),
            new XElement(
                "remarks",
                new XElement("para", IncorrectDreamFocusRemark),
                ImporterSourceReference(dreamFocusDocs),
                XElement.Parse($"<para>{AndroidAttribution}</para>")));
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var dreamFocusBlock = file.DocsBlocks[setTitle.Order];
        var dreamFocusText =
            fixtureText[..dreamFocusBlock.Start] +
            dreamFocusMarkup.ToString(SaveOptions.DisableFormatting) +
            fixtureText[dreamFocusBlock.End..];
        var dreamFocusOwner = setTitle with { Id = DreamFocusMemberId };
        file.UpdateBlockOffsets(setTitle.Order, dreamFocusText);
        var correctedDreamFocus = RefreshImporterOwnedRemarks(
            dreamFocusText, file, dreamFocusOwner, dreamFocusDocs);
        Assert(
            correctedDreamFocus.Reason is null &&
            correctedDreamFocus.Text == dreamFocusText.Replace(
                IncorrectDreamFocusRemark, CorrectDreamFocusRemark, StringComparison.Ordinal),
            $"focus repair changes only the exact paragraph and retains authored summary and metadata bytes ({correctedDreamFocus.Reason}: {correctedDreamFocus.Detail})");
        file.UpdateBlockOffsets(setTitle.Order, correctedDreamFocus.Text);
        Assert(
            RefreshImporterOwnedRemarks(
                correctedDreamFocus.Text, file, dreamFocusOwner, dreamFocusDocs).Reason ==
                "source_remarks_current",
            "focus reference repair is idempotent");
        foreach (var authoredFocusParagraph in new[]
        {
            $"<para>{IncorrectDreamFocusRemark} Additional authored guidance.</para>",
            $"<para><c>{IncorrectDreamFocusRemark}</c></para>",
            $"<para><![CDATA[{IncorrectDreamFocusRemark}]]></para>",
            $"<para><!--Keep-->{IncorrectDreamFocusRemark}</para>",
            $"<para><?keep guidance?>{IncorrectDreamFocusRemark}</para>",
            $"<para>{IncorrectDreamFocusRemark}</para><para>Additional authored guidance.</para>",
        })
        {
            var authoredFocusText = dreamFocusText.Replace(
                $"<para>{IncorrectDreamFocusRemark}</para>",
                authoredFocusParagraph,
                StringComparison.Ordinal);
            file.UpdateBlockOffsets(setTitle.Order, authoredFocusText);
            var authoredFocusResult = RefreshImporterOwnedRemarks(
                authoredFocusText, file, dreamFocusOwner, dreamFocusDocs);
            Assert(
                authoredFocusResult.Text == authoredFocusText &&
                authoredFocusResult.Reason == "existing_remarks_not_importer_owned",
                "focus repair preserves authored prose, mixed content, CDATA, comments, and processing instructions");
        }
        file.UpdateBlockOffsets(setTitle.Order, dreamFocusText);
        Assert(
            RefreshImporterOwnedRemarks(
                dreamFocusText, file, setTitle, dreamFocusDocs).Text == dreamFocusText &&
            RefreshImporterOwnedRemarks(
                dreamFocusText, file, dreamFocusOwner,
                dreamFocusDocs with { SourceUrl = DreamFocusSourceUrl + ".Altered" }).Text ==
                dreamFocusText,
            "focus repair requires the exact managed owner and mapped source reference");
        var authoredFocusAttribution = dreamFocusText.Replace(
            "Portions of this page", "Authored portions of this page", StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, authoredFocusAttribution);
        Assert(
            RefreshImporterOwnedRemarks(
                authoredFocusAttribution, file, dreamFocusOwner, dreamFocusDocs).Text ==
                authoredFocusAttribution,
            "focus repair preserves authored attribution");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var knownAndroidParameterDocs = javaExampleDocs with
        {
            SourceUrl = KnownAndroidParameterRepairs[0].SourceUrl,
            SourceLabel = "android.net.vcn.VcnCellUnderlyingNetworkTemplate.Builder.setOperatorPlmnIds",
            SourceKind = "android",
        };
        var knownAndroidParameterMarkup = new XElement(
            "Docs",
            new XElement(
                "param",
                new XAttribute("name", KnownAndroidParameterRepairs[0].ParameterName),
                KnownAndroidParameterRepairs[0].IncorrectText),
            new XElement(
                "remarks",
                ImporterSourceReference(knownAndroidParameterDocs),
                XElement.Parse($"<para>{AndroidAttribution}</para>")));
        Assert(
            FindKnownAndroidParameterRepair(
                KnownAndroidParameterRepairs[0].MemberId,
                knownAndroidParameterMarkup,
                knownAndroidParameterDocs) == KnownAndroidParameterRepairs[0],
            "exact known Android parameter prose is eligible for correction");
        Assert(
            FindKnownAndroidParameterRepair(
                KnownAndroidParameterRepairs[0].MemberId + ".Altered",
                knownAndroidParameterMarkup,
                knownAndroidParameterDocs) is null,
            "Android parameter repairs require the exact managed member");
        knownAndroidParameterMarkup.Element("param")!.Value =
            KnownAndroidParameterRepairs[0].CorrectText;
        Assert(
            FindKnownAndroidParameterRepair(
                KnownAndroidParameterRepairs[0].MemberId,
                knownAndroidParameterMarkup,
                knownAndroidParameterDocs) is null,
            "corrected Android parameter prose remains idempotent");
        var knownAndroidProseDocs = javaExampleDocs with
        {
            SourceUrl = KnownAndroidProseRepairs[0].SourceUrl,
            SourceLabel = "android.adservices.measurement.DeletionRequest.Builder.setDeletionMode",
            SourceKind = "android",
        };
        var knownAndroidProseMarkup = new XElement(
            "Docs",
            new XElement(
                "param",
                new XAttribute("name", "deletionMode"),
                "Value is one of the following: DeletionRequest.DELETION_MODE_ALL; DeletionRequest.DELETION_MODE_EXCLUDE_INTERNAL_DATA"),
            new XElement("summary", KnownAndroidProseRepairs[0].IncorrectSummary),
            new XElement("returns", "To be added."),
            new XElement(
                "remarks",
                new XElement("para", KnownAndroidProseRepairs[0].IncorrectRemarks),
                ImporterSourceReference(knownAndroidProseDocs),
                XElement.Parse($"<para>{AndroidAttribution}</para>")));
        Assert(
            FindKnownAndroidProseRepair(
                KnownAndroidProseRepairs[0].MemberId,
                knownAndroidProseMarkup,
                knownAndroidProseDocs) == KnownAndroidProseRepairs[0],
            "exact known Android deletion mode prose is eligible for correction");
        Assert(
            FindKnownAndroidProseRepair(
                KnownAndroidProseRepairs[0].MemberId + ".Altered",
                knownAndroidProseMarkup,
                knownAndroidProseDocs) is null,
            "Android deletion mode prose repair requires the exact managed member");
        var attributedAndroidSummaryMarkup = new XElement(knownAndroidProseMarkup);
        attributedAndroidSummaryMarkup.Element("summary")!.SetAttributeValue(
            XNamespace.Xml + "lang",
            "en");
        Assert(
            FindKnownAndroidProseRepair(
                KnownAndroidProseRepairs[0].MemberId,
                attributedAndroidSummaryMarkup,
                knownAndroidProseDocs) is null,
            "Android deletion mode prose repair preserves attributed summaries");
        var attributedAndroidRemarksMarkup = new XElement(knownAndroidProseMarkup);
        attributedAndroidRemarksMarkup.Element("remarks")!.SetAttributeValue(
            XNamespace.Xml + "space",
            "preserve");
        Assert(
            FindKnownAndroidProseRepair(
                KnownAndroidProseRepairs[0].MemberId,
                attributedAndroidRemarksMarkup,
                knownAndroidProseDocs) is null,
            "Android deletion mode prose repair preserves attributed remarks");
        var alteredAndroidProseMarkup = new XElement(knownAndroidProseMarkup);
        alteredAndroidProseMarkup.Element("summary")!.Value =
            KnownAndroidProseRepairs[0].IncorrectSummary + " Authored.";
        Assert(
            FindKnownAndroidProseRepair(
                KnownAndroidProseRepairs[0].MemberId,
                alteredAndroidProseMarkup,
                knownAndroidProseDocs) is null,
            "Android deletion mode prose repair preserves altered summaries");
        knownAndroidProseMarkup.Element("summary")!.Value =
            KnownAndroidProseRepairs[0].CorrectSummary;
        Assert(
            FindKnownAndroidProseRepair(
                KnownAndroidProseRepairs[0].MemberId,
                knownAndroidProseMarkup,
                knownAndroidProseDocs) is null,
            "corrected Android deletion mode prose remains idempotent");
        var rawSignatureBlock = Regex.Replace(
            file.Text[file.DocsBlocks[setTitle.Order].Start..file.DocsBlocks[setTitle.Order].End],
            @"<remarks\b[^>]*>.*?</remarks>",
            $"<remarks>{file.Newline}          <code lang=\"text/java\">public void setTitle()</code>{file.Newline}          <para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(javaExampleDocs.SourceUrl)}\" title=\"Reference documentation\">Android reference for <code>{XmlEscape(javaExampleDocs.SourceLabel)}</code>.</a></format></para>{file.Newline}          <para>{AndroidAttribution}</para>{file.Newline}        </remarks>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var rawSignatureText =
            file.Text[..file.DocsBlocks[setTitle.Order].Start] +
            rawSignatureBlock +
            file.Text[file.DocsBlocks[setTitle.Order].End..];
        Assert(!rawSignatureText.Equals(file.Text, StringComparison.Ordinal), "raw signature fixture setup");
        file.UpdateBlockOffsets(setTitle.Order, rawSignatureText);
        var refreshedSignatureText = ReplaceIncompleteCodeExampleRemarks(
            rawSignatureText,
            file,
            setTitle,
            javaExampleDocs);
        Assert(
            HasIncompleteImporterJavaExample(
                rawSignatureText[file.DocsBlocks[setTitle.Order].Start..file.DocsBlocks[setTitle.Order].End]),
            "raw Android signature is exact importer-owned repair evidence");
        Assert(
            !refreshedSignatureText.Contains(
                    "<code lang=\"text/java\">public void setTitle()</code>",
                    StringComparison.Ordinal) &&
                refreshedSignatureText.Contains(
                    "<para>Sets the widget title. The exact JNI overload is required.</para>",
                    StringComparison.Ordinal),
            "raw Android signature blocks are regenerated from source");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var authoredJavaExampleRepairs = new[]
        {
            rawSignatureText.Replace(
                $"</code>.</a></format></para>{file.Newline}          <para>{AndroidAttribution}</para>",
                $"</code> <c>Managed guidance.</c>.</a></format></para>{file.Newline}          <para>{AndroidAttribution}</para>",
                StringComparison.Ordinal),
            rawSignatureText.Replace(
                $"<para>{AndroidAttribution}</para>",
                $"<para>{AndroidAttribution} Managed guidance.</para>",
                StringComparison.Ordinal),
            rawSignatureText.Replace(
                "public void setTitle()",
                "public void <see cref=\"M:Example.Managed\" />setTitle()",
                StringComparison.Ordinal),
        };
        Assert(
            authoredJavaExampleRepairs.All(candidate =>
            {
                file.UpdateBlockOffsets(setTitle.Order, candidate);
                return !HasIncompleteImporterJavaExample(
                           candidate[file.DocsBlocks[setTitle.Order].Start..file.DocsBlocks[setTitle.Order].End]) &&
                    ReplaceIncompleteCodeExampleRemarks(
                        candidate,
                        file,
                        setTitle,
                        javaExampleDocs).Equals(candidate, StringComparison.Ordinal);
            }),
            "Java example repairs preserve nested source, attribution, and signature XML");
        var rawSignatureWithoutAttribution = rawSignatureText.Replace(
            $"{file.Newline}          <para>{AndroidAttribution}</para>",
            "",
            StringComparison.Ordinal);
        var authoredJavaExampleSiblings = new[]
        {
            $"<code lang=\"C#\">builder.SetTag(myTag);</code>",
            "<see cref=\"M:Example.Managed\" />",
            "<example><para>Managed guidance.</para></example>",
        }.Select(sibling => rawSignatureWithoutAttribution.Replace(
            $"</code>{file.Newline}          <para><format",
            $"</code>{file.Newline}          {sibling}{file.Newline}          <para><format",
            StringComparison.Ordinal));
        Assert(
            authoredJavaExampleSiblings.All(candidate =>
            {
                file.UpdateBlockOffsets(setTitle.Order, candidate);
                return !HasIncompleteImporterJavaExample(
                           candidate[file.DocsBlocks[setTitle.Order].Start..file.DocsBlocks[setTitle.Order].End]) &&
                    ReplaceIncompleteCodeExampleRemarks(
                        candidate,
                        file,
                        setTitle,
                        javaExampleDocs).Equals(candidate, StringComparison.Ordinal);
            }),
            "Java example repairs preserve unaffiliated authored siblings");
        file.UpdateBlockOffsets(setTitle.Order, fixtureText);
        var authoredCSharpExampleText = file.Text.Replace(
            $"<remarks>{file.Newline}          <para>Keep this existing prose.</para>",
            $"<remarks>{file.Newline}          <para>Example code:</para>{file.Newline}          <code lang=\"C#\">builder.SetTag(myTag);</code>{file.Newline}          <para>Managed guidance: call <see cref=\"M:Android.Hardware.Camera2.CaptureRequest.Builder.SetTag(Java.Lang.Object)\" /> first.</para>{file.Newline}          <para><format type=\"text/html\"><a href=\"{XmlAttributeEscape(mappedDocs.SourceUrl)}\" title=\"Reference documentation\">{mappedSourceKind} reference for <code>{XmlEscape(mappedDocs.SourceLabel)}</code>.</a></format></para>",
            StringComparison.Ordinal);
        Assert(
            !HasIncompleteImporterJavaExample(
                    authoredCSharpExampleText[
                        file.DocsBlocks[setTitle.Order].Start..file.DocsBlocks[setTitle.Order].End]) &&
                ReplaceIncompleteCodeExampleRemarks(
                    authoredCSharpExampleText,
                    file,
                    setTitle,
                    mappedDocs).Equals(authoredCSharpExampleText, StringComparison.Ordinal),
            "authored C# examples, guidance, and XML remain untouched");
        Assert(
            !IsImporterSourceReferenceParagraph(
                XElement.Parse(
                    $"<para>Application-specific guidance: <format type=\"text/html\"><a href=\"{XmlAttributeEscape(mappedDocs.SourceUrl)}\" title=\"Reference documentation\">{mappedSourceKind} reference for <code>{XmlEscape(mappedDocs.SourceLabel)}</code>.</a></format></para>")),
            "source references with authored surrounding text are not importer metadata");
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
            repositoryRoot,
            "tools",
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
        var malformedNestedList = SourcePage.HtmlTableCellText(
            "<code>int</code>: the render flag. One or more of:" +
            "<ul><li>FIRST</li><li>SECOND</li></ul>. <br>" +
            "Value is either <code>0</code> or a combination of the following:" +
            "<ul><li>FIRST</li><li>SECOND</li><li>THIRD</li><ul>");
        Assert(
            malformedNestedList ==
                "int: the render flag. Value is either 0 or a combination of the following: FIRST; SECOND; THIRD",
            "malformed Android nested lists retain each complete value once");
        var validNestedLists = SourcePage.HtmlTableCellText(
            "<code>int</code>: the render flag. One or more of:" +
            "<ul><li>FIRST</li><li>SECOND</li></ul>. <br>" +
            "Value is either <code>0</code> or a combination of the following:" +
            "<ul><li>FIRST</li><li>SECOND</li><li>THIRD</li></ul>");
        Assert(
            validNestedLists.Contains("One or more of:.", StringComparison.Ordinal) &&
                Regex.Matches(validNestedLists, @"\bFIRST\b").Count == 2,
            "valid Android table lists retain independent list lead-ins and values");
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
        var incompleteSummary = ChannelValueOrSkip(
            "Credential Manager is invoked instead of Autofill. When that happens, Save Dialog cannot be shown, and this will be populated in",
            "summary",
            "source_summary_missing");
        Assert(
            incompleteSummary.Reason == "source_channel_not_meaningful",
            "incomplete source summary skip");
        var malformedSourceMarkup = ChannelValueOrSkip(
            "See FetchAndJoinCustomAudienceRequest.getFetchUri() ()} for details.",
            "remarks",
            "source_remarks_missing");
        Assert(
            malformedSourceMarkup.Reason == "source_channel_not_meaningful",
            "malformed source markup skip");
        var completeGenericSummary = ChannelValueOrSkip(
            "Type used when the service can save the contents of a screen, but cannot describe what the content is for.",
            "summary",
            "source_summary_missing");
        Assert(
            completeGenericSummary.Text ==
                "Type used when the service can save the contents of a screen, but cannot describe what the content is for.",
            "complete SaveDataType.Generic summary ending in for");
        var completeGenericCardSummary = ChannelValueOrSkip(
            "Type used when the FillResponse represents a card that does not a specified card or cannot identify what the card is for.",
            "summary",
            "source_summary_missing");
        Assert(
            completeGenericCardSummary.Text ==
                "Type used when the FillResponse represents a card that does not a specified card or cannot identify what the card is for.",
            "complete SaveDataType.GenericCard summary ending in for");
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
            CleanSourceText("Triggers a custom UI before before autofilling the screen.") ==
                "Triggers a custom UI before autofilling the screen.",
            "Android duplicate word cleanup");
        Assert(
            CleanSourceText(
                "Requires android.Manifest.permission.READ_PRIVILEGED_PHONE_STATE Requires android.Manifest.permission.READ_PRIVILEGED_PHONE_STATE") ==
                    "Requires android.Manifest.permission.READ_PRIVILEGED_PHONE_STATE",
            "Android duplicate permission requirement cleanup");
        Assert(
            CleanSourceText("Altough similiarly named with another method.") ==
                "Although similarly named with another method.",
            "Android spelling cleanup");
        Assert(
            CleanSourceText(
                "a combination of FillResponse.FLAG_TRACK_CONTEXT_COMMITED and FillResponse.FLAG_DISABLE_ACTIVITY_ONLY, or 0. Value is either 0 or a combination of the following:") ==
                    "a combination of FillResponse.FLAG_TRACK_CONTEXT_COMMITED, FillResponse.FLAG_DISABLE_ACTIVITY_ONLY, and FillResponse.FLAG_DELAY_FILL, or 0. Value is either 0 or a combination of the following:",
            "Android FillResponse flags cleanup");
        Assert(
            CleanSourceText("Resoure Id of the custom string.") ==
                "Resource Id of the custom string.",
            "Android resource spelling cleanup");
        var malformedImageTransformationReturn = ReturnReplacement(
            new SourceDocs(
                "",
                [],
                new(StringComparer.Ordinal),
                "this build",
                new(StringComparer.Ordinal),
                "https://developer.android.com/reference/android/service/autofill/ImageTransformation.Builder#addOption(java.util.regex.Pattern,%20int)",
                "android.service.autofill.ImageTransformation.Builder.addOption",
                "android"));
        var ordinaryBuildReturn = ReturnReplacement(
            new SourceDocs(
                "",
                [],
                new(StringComparer.Ordinal),
                "this build",
                new(StringComparer.Ordinal),
                "https://developer.android.com/reference/android/os/Build#FINGERPRINT",
                "android.os.Build.FINGERPRINT",
                "android"));
        Assert(
            malformedImageTransformationReturn.Text == "this builder" &&
                ordinaryBuildReturn.Text == "this build" &&
                CleanSourceText("Build.FINGERPRINT identifies this build.") ==
                    "Build.FINGERPRINT identifies this build.",
            "ImageTransformation builder return cleanup is source-scoped");
        Assert(
            CleanSourceText(
                "Federated Compute Server documentation.. This value cannot be null.") ==
                    "Federated Compute Server documentation. This value cannot be null.",
            "Android federated compute parameter punctuation cleanup");
        Assert(
            CleanSourceText(
                "regular expression with groups (delimited by ( and () that are used to substitute parts of the value.") ==
                    "regular expression with groups (delimited by ( and )) that are used to substitute parts of the value.",
            "Autofill regex parenthesis cleanup");
        Assert(
            CleanSourceText(
                "The selected dataset was selected (getChangedFields();") ==
                    "The selected dataset was selected (getChangedFields());",
            "Autofill event parenthesis cleanup");
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
        var copiedLabelWithProsePage = SourcePage.Parse(
            javaRequest,
            javaHtml.Replace(
                "Description copied from interface: <code>CharSequence</code>",
                "Description copied from interface: CharSequence retains fixture semantics.",
                StringComparison.Ordinal));
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
                    paragraph => paragraph.Text.Contains("Deprecated", StringComparison.Ordinal) ||
                        paragraph.Text.Contains("Description copied from", StringComparison.Ordinal)),
            "Java deprecated and copied-description member blocks excluded");
        Assert(
            copiedLabelWithProsePage.Members.Single(member => member.Name == "length").Docs?.Paragraphs.Any(
                paragraph => paragraph.Text ==
                    "Description copied from interface: CharSequence retains fixture semantics.") == true,
            "Java copied-description prefixes with authored prose are preserved");
        var empty = javaPage.Members.Single(member => member.Name == "EMPTY");
        Assert(empty.IsField && empty.Docs?.Summary == "An empty fixture string.", "Java field extraction");
        var equivalent = javaPage.Members.Single(member => member.Name == "equivalent");
        Assert(
            equivalent.Docs?.Paragraphs.SequenceEqual(
                [
                    new SourceParagraph(
                        "Updates the fixture value. This is equivalent to:",
                        IsCode: false),
                    new SourceParagraph("map.put(key, value);", IsCode: true),
                    new SourceParagraph("except that the update is atomic.", IsCode: false),
                ]) == true,
            "Java code lead-ins retain their explanatory trailing colon");
        var incompleteLeadIn = javaPage.Members.Single(member =>
            member.Name == "incompleteLeadIn");
        Assert(
            incompleteLeadIn.Docs?.Paragraphs.SequenceEqual(
                [
                    new SourceParagraph("fixture.noop();", IsCode: true),
                    new SourceParagraph(
                        "The trailing fixture sentence is complete.",
                        IsCode: false),
                ]) == true,
            "ordinary incomplete Java prose before code blocks remains excluded");
        var equivalentDocs = equivalent.Docs ??
            throw new InvalidOperationException("SELF-TEST FAIL: Java equivalent source documentation");
        const string laterLeadingEquivalentRemarksText =
            "<Docs><remarks><para>To be added.</para><para>This is equivalent to:</para></remarks></Docs>";
        var laterLeadingEquivalentRemarks = XDocument.Parse(laterLeadingEquivalentRemarksText)
            .Root!.Element("remarks")!;
        var laterLeadingEquivalentPlaceholder = Placeholder.Create(
            laterLeadingEquivalentRemarks.Elements("para").First(),
            0);
        var laterLeadingEquivalentReplacement = LimitOverlappingRemarksReplacement(
            laterLeadingEquivalentPlaceholder,
            equivalentDocs,
            ReplacementFor(laterLeadingEquivalentPlaceholder, equivalentDocs),
            laterLeadingEquivalentRemarks);
        Assert(
            laterLeadingEquivalentReplacement.Text is null &&
                laterLeadingEquivalentReplacement.Reason ==
                    "source_remarks_overlap_order_conflict",
            "later retained equivalent source prose skips rather than violating source order");
        const string leadingEquivalentBeforePlaceholderText =
            "<Docs><remarks><para>Updates the fixture value.</para><para>To be added.</para></remarks></Docs>";
        var leadingEquivalentBeforePlaceholder = XDocument.Parse(
            leadingEquivalentBeforePlaceholderText).Root!.Element("remarks")!;
        var leadingEquivalentPlaceholder = Placeholder.Create(
            leadingEquivalentBeforePlaceholder.Elements("para").Last(),
            0);
        var leadingEquivalentReplacement = LimitOverlappingRemarksReplacement(
            leadingEquivalentPlaceholder,
            equivalentDocs,
            ReplacementFor(leadingEquivalentPlaceholder, equivalentDocs),
            leadingEquivalentBeforePlaceholder);
        Assert(
            TryReplacePlaceholder(
                leadingEquivalentBeforePlaceholderText,
                new DocsBlock(0, 0, leadingEquivalentBeforePlaceholderText.Length),
                leadingEquivalentPlaceholder,
                leadingEquivalentReplacement,
                out var leadingEquivalentCompleted,
                out _) &&
            XDocument.Parse(leadingEquivalentCompleted).Root!.Element("remarks")!
                .Elements().Select(element => element.Name.LocalName)
                    .SequenceEqual(["para", "para", "code", "para"]) &&
            XDocument.Parse(leadingEquivalentCompleted).Root!.Element("remarks")!
                    .Elements("para").ElementAt(1).Value == "This is equivalent to:" &&
                XDocument.Parse(leadingEquivalentCompleted).Root!.Element("remarks")!
                    .Elements("para").Last().Value == "except that the update is atomic.",
                "existing leading equivalent prose retains the code lead-in, code, and trailing prose order");
        var booleanReturnFixture = File.ReadAllText(Path.Combine(
            fixtureRoot,
            "concurrent-map-boolean-returns-java-reference.html"));
        var booleanReturnRepairCases = new[]
        {
            (
                "ConcurrentHashMap.xml",
                "M:Java.Util.Concurrent.ConcurrentHashMap.Remove(Java.Lang.Object,Java.Lang.Object)",
                "true if the value was removed"),
            (
                "ConcurrentHashMap.xml",
                "M:Java.Util.Concurrent.ConcurrentHashMap.Replace(Java.Lang.Object,Java.Lang.Object,Java.Lang.Object)",
                "true if the value was replaced"),
            (
                "ConcurrentSkipListMap.xml",
                "M:Java.Util.Concurrent.ConcurrentSkipListMap.Remove(Java.Lang.Object,Java.Lang.Object)",
                "true if the value was removed"),
            (
                "ConcurrentSkipListMap.xml",
                "M:Java.Util.Concurrent.ConcurrentSkipListMap.Replace(Java.Lang.Object,Java.Lang.Object,Java.Lang.Object)",
                "true if the value was replaced"),
        };
        foreach (var repairCase in booleanReturnRepairCases)
        {
            var booleanReturnFile = LoadedFile.Load(
                repositoryRoot,
                Path.Combine(
                    docsRoot,
                    "Java.Util.Concurrent",
                    repairCase.Item1));
            booleanReturnFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
            var booleanReturnOwner = booleanReturnFile.Owners.Single(owner =>
                owner.Id == repairCase.Item2);
            var booleanReturnPage = SourcePage.Parse(
                booleanReturnOwner.SourceRequest!,
                booleanReturnFixture);
            var booleanReturnMapping = MapOwner(
                booleanReturnOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [booleanReturnOwner.SourceRequest!.Url] =
                        SourceLoadResult.Success(booleanReturnPage),
                });
            var knownBooleanRepair = KnownBooleanReturnRepairs.Single(repair =>
                UrlsEqual(
                    repair.SourceUrl,
                    booleanReturnMapping.Docs!.SourceUrl));
            Assert(
                booleanReturnMapping.Docs?.Returns == repairCase.Item3 &&
                    ReturnReplacement(booleanReturnMapping.Docs).Text == repairCase.Item3 &&
                    !HasKnownIncorrectBooleanReturnRepairCandidate(
                        booleanReturnFile,
                        booleanReturnOwner),
                "official Java Boolean return fixture maps only current return documentation");

            var booleanReturnBlock = booleanReturnFile.DocsBlocks[booleanReturnOwner.Order];
            var booleanReturnBlockText = booleanReturnFile.Text[
                booleanReturnBlock.Start..booleanReturnBlock.End];
            var booleanReturnDocs = XElement.Parse(
                booleanReturnBlockText,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            Assert(
                TryGetElementSpan(
                    booleanReturnBlockText,
                    booleanReturnDocs.Element("returns")!,
                    out var booleanReturnSpan),
                "Boolean return fixture target is parser-located");
            var incorrectBooleanReturnElement =
                $"<returns>{knownBooleanRepair.IncorrectMarkup}</returns>";
            var incorrectBooleanReturnText =
                booleanReturnFile.Text[..booleanReturnBlock.Start] +
                booleanReturnBlockText[..booleanReturnSpan.Start] +
                incorrectBooleanReturnElement +
                booleanReturnBlockText[booleanReturnSpan.End..] +
                booleanReturnFile.Text[booleanReturnBlock.End..];
            booleanReturnFile.UpdateBlockOffsets(
                booleanReturnOwner.Order,
                incorrectBooleanReturnText);
            var incorrectBooleanReturnOwner = booleanReturnOwner with
            {
                Docs = XElement.Parse(
                    booleanReturnFile.Text[
                        booleanReturnFile.DocsBlocks[booleanReturnOwner.Order].Start..
                        booleanReturnFile.DocsBlocks[booleanReturnOwner.Order].End],
                    LoadOptions.PreserveWhitespace),
                Placeholders = [],
            };
            var isKnownIncorrectBooleanReturn =
                HasKnownIncorrectBooleanReturnRepairCandidate(
                    booleanReturnFile,
                    incorrectBooleanReturnOwner);
            var requiresBooleanReturnSource =
                RequiresSourceLoad(booleanReturnFile, incorrectBooleanReturnOwner);
            var repairedBooleanReturn = RepairKnownBooleanReturn(
                incorrectBooleanReturnText,
                booleanReturnFile,
                incorrectBooleanReturnOwner,
                booleanReturnMapping.Docs!);
            booleanReturnFile.UpdateBlockOffsets(
                booleanReturnOwner.Order,
                repairedBooleanReturn.Text);
            var repairedBooleanReturnDocs = XElement.Parse(
                repairedBooleanReturn.Text[
                    booleanReturnFile.DocsBlocks[booleanReturnOwner.Order].Start..
                    booleanReturnFile.DocsBlocks[booleanReturnOwner.Order].End],
                LoadOptions.PreserveWhitespace);
            Assert(
                isKnownIncorrectBooleanReturn &&
                    requiresBooleanReturnSource &&
                    repairedBooleanReturn is { Repaired: true, Skip: null } &&
                    repairedBooleanReturnDocs.Element("returns")?.Value == repairCase.Item3,
                "exact importer Boolean return error is repaired from the matching official Java source");

            var arbitraryReturnText = incorrectBooleanReturnText.Replace(
                incorrectBooleanReturnElement,
                "<returns>Authored documentation must remain unchanged.</returns>",
                StringComparison.Ordinal);
            booleanReturnFile.UpdateBlockOffsets(
                booleanReturnOwner.Order,
                arbitraryReturnText);
            var arbitraryReturnOwner = incorrectBooleanReturnOwner with
            {
                Docs = XElement.Parse(
                    booleanReturnFile.Text[
                        booleanReturnFile.DocsBlocks[booleanReturnOwner.Order].Start..
                        booleanReturnFile.DocsBlocks[booleanReturnOwner.Order].End],
                    LoadOptions.PreserveWhitespace),
            };
            var arbitraryReturnRepair = RepairKnownBooleanReturn(
                arbitraryReturnText,
                booleanReturnFile,
                arbitraryReturnOwner,
                booleanReturnMapping.Docs!);
            booleanReturnFile.UpdateBlockOffsets(
                booleanReturnOwner.Order,
                incorrectBooleanReturnText);
            var nonBooleanMember = new XElement(arbitraryReturnOwner.Member!);
            nonBooleanMember.Element("ReturnValue")!.Element("ReturnType")!.Value =
                "Java.Lang.Object";
            var mismatchedSourceRepair = RepairKnownBooleanReturn(
                incorrectBooleanReturnText,
                booleanReturnFile,
                incorrectBooleanReturnOwner,
                booleanReturnMapping.Docs! with
                {
                    SourceUrl = booleanReturnMapping.Docs.SourceUrl + "?untrusted",
                });
            var nonBooleanRepair = RepairKnownBooleanReturn(
                incorrectBooleanReturnText,
                booleanReturnFile,
                incorrectBooleanReturnOwner with { Member = nonBooleanMember },
                booleanReturnMapping.Docs!);
            var arbitraryCandidate =
                HasKnownIncorrectBooleanReturnRepairCandidate(
                    booleanReturnFile,
                    arbitraryReturnOwner);
            var arbitraryPreserved = arbitraryReturnRepair is { Repaired: false, Skip: null } &&
                arbitraryReturnRepair.Text == arbitraryReturnText;
            var mismatchedSourcePreserved =
                mismatchedSourceRepair is { Repaired: false, Skip: null } &&
                mismatchedSourceRepair.Text == incorrectBooleanReturnText;
            var nonBooleanPreserved = nonBooleanRepair is { Repaired: false, Skip: null } &&
                nonBooleanRepair.Text == incorrectBooleanReturnText;
            Assert(
                !arbitraryCandidate &&
                    arbitraryPreserved &&
                    mismatchedSourcePreserved &&
                    nonBooleanPreserved,
                $"Boolean return repair rejects arbitrary prose, a non-exact source URL, and non-Boolean managed returns (candidate={arbitraryCandidate}, arbitrary={arbitraryPreserved}, source={mismatchedSourcePreserved}, type={nonBooleanPreserved})");
        }
        var concurrentHashMapFile = LoadedFile.Load(
            repositoryRoot,
            Path.Combine(
                docsRoot,
                "Java.Util.Concurrent",
                "ConcurrentHashMap.xml"));
        concurrentHashMapFile.SelectOwners("PutIfAbsent");
        var putIfAbsentOwner = concurrentHashMapFile.Owners.Single();
        Assert(
            putIfAbsentOwner.MemberRegistration ==
                new MemberRegistration(
                    "putIfAbsent",
                    "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;",
                    false) &&
            putIfAbsentOwner.SourceRequest?.Url ==
                JavaReference + "java.base/java/util/concurrent/ConcurrentHashMap.html",
            "ConcurrentHashMap PutIfAbsent fixture uses its real registered Oracle member");
        var putIfAbsentRemarks = putIfAbsentOwner.Docs.Element("remarks")!;
        var putIfAbsentElements = putIfAbsentRemarks.Elements().ToList();
        var putIfAbsentSourceReference = putIfAbsentElements.Single(
            element => TryGetImporterSourceReferenceUrl(
                element.ToString(SaveOptions.DisableFormatting),
                out _));
        Assert(
            TryGetImporterSourceReferenceUrl(
                putIfAbsentSourceReference.ToString(SaveOptions.DisableFormatting),
                out var putIfAbsentSourceUrl),
            "ConcurrentHashMap PutIfAbsent fixture retains its importer source reference");
        var putIfAbsentSourceReferenceIndex = putIfAbsentElements.IndexOf(
            putIfAbsentSourceReference);
        var putIfAbsentSourceFragments = putIfAbsentElements
            .Take(putIfAbsentSourceReferenceIndex)
            .Select(element => new SourceParagraph(
                element.Value,
                element.Name.LocalName == "code"))
            .ToList();
        Assert(
            putIfAbsentSourceFragments.Count == 4 &&
                !putIfAbsentSourceFragments[0].IsCode &&
                !putIfAbsentSourceFragments[1].IsCode &&
                putIfAbsentSourceFragments[1].Text.Equals(
                    "This is equivalent to, for this map:",
                    StringComparison.Ordinal) &&
                putIfAbsentSourceFragments[2].IsCode &&
                !putIfAbsentSourceFragments[3].IsCode,
            "ConcurrentHashMap PutIfAbsent fixture retains its explanatory lead-in, code, and trailing prose");
        var putIfAbsentDocs = new SourceDocs(
            putIfAbsentOwner.Docs.Element("summary")!.Value,
            putIfAbsentSourceFragments,
            [],
            "",
            new Dictionary<string, string>(StringComparer.Ordinal),
            putIfAbsentSourceUrl,
            putIfAbsentSourceReference.Descendants("code").Single().Value,
            "java");
        var partialPutIfAbsentRemarks = new XElement(
            "remarks",
            new XElement("para", putIfAbsentSourceFragments[0].Text),
            ImporterSourceReference(putIfAbsentDocs));
        var partialPutIfAbsentRemarksText = partialPutIfAbsentRemarks.ToString(
            SaveOptions.DisableFormatting);
        void AssertPutIfAbsentRemarksRefresh(string prefix, string description)
        {
            var refreshFixtureText = $"<Docs>{prefix}{partialPutIfAbsentRemarksText}</Docs>";
            var refreshFixtureDocs = XElement.Parse(
                refreshFixtureText,
                LoadOptions.PreserveWhitespace);
            var refreshFixtureOwner = putIfAbsentOwner with
            {
                Order = 0,
                Docs = refreshFixtureDocs,
                Placeholders = [],
            };
            var refreshFixtureFile = new LoadedFile
            {
                Path = "ConcurrentHashMap.PutIfAbsent.refresh.xml",
                RelativePath = "ConcurrentHashMap.PutIfAbsent.refresh.xml",
                Text = refreshFixtureText,
                Newline = "\n",
                HasUtf8Bom = false,
                Root = refreshFixtureDocs,
            };
            refreshFixtureFile.UpdateBlockOffsets(0, refreshFixtureText);
            var refreshed = RefreshIncompleteImporterRemarks(
                refreshFixtureText,
                refreshFixtureFile,
                refreshFixtureOwner,
                putIfAbsentDocs);
            var refreshedRemarks = XElement.Parse(
                refreshed.Text,
                LoadOptions.PreserveWhitespace).Element("remarks")!;
            Assert(
                refreshed.Reason is null &&
                refreshed.Text.Contains(prefix, StringComparison.Ordinal) &&
                refreshedRemarks.Elements().Take(4).Select(element => element.Name.LocalName)
                    .SequenceEqual(["para", "para", "code", "para"]) &&
                refreshedRemarks.Elements().Take(4).Select(element => element.Value)
                    .SequenceEqual(putIfAbsentSourceFragments.Select(fragment => fragment.Text)),
                description);
        }
        AssertPutIfAbsentRemarksRefresh(
            "",
            "compact importer-owned remarks refresh produces valid XML");
        const string cdataLiteralRemarks =
            "<![CDATA[<remarks><para>CDATA literal one.</para><para>CDATA literal two.</para></remarks>]]>";
        AssertPutIfAbsentRemarksRefresh(
            cdataLiteralRemarks,
            "remarks refresh preserves earlier CDATA literal markup and updates the validated remarks");
        const string commentLiteralRemarks =
            "<!-- <remarks><para>comment literal one.</para><para>comment literal two.</para></remarks> -->";
        AssertPutIfAbsentRemarksRefresh(
            commentLiteralRemarks,
            "remarks refresh preserves earlier comment literal markup and updates the validated remarks");
        var hybridSourceRequest = SourceRequest.Create(
            "java/util/concurrent/CopyOnWriteArrayList")!;
        var hybridSourcePage = SourcePage.Parse(
            hybridSourceRequest,
            File.ReadAllText(Path.Combine(
                fixtureRoot,
                "copy-on-write-array-list-java-reference.html")));
        var hybridRemarksDocs = hybridSourcePage.Members.Single(member =>
            member.Name == "reversed" &&
            member.ArgumentDescriptors?.Count == 0).Docs ??
            throw new InvalidOperationException(
                "SELF-TEST FAIL: CopyOnWriteArrayList Reversed source documentation");
        var hybridSourceFragments = ExpandRemarksFragments(hybridRemarksDocs.Paragraphs);
        Assert(
            hybridSourceFragments.Count == 8 &&
                hybridSourceFragments.All(fragment =>
                    !fragment.Text.Equals("Added in 21.", StringComparison.Ordinal)),
            "cached Oracle Reversed source retains all prose fragments without Android annotations");
        var hybridAttribution = XElement.Parse($"<para>{AndroidAttribution}</para>");
        Assert(
            IsRetainedRemarksParagraph(XElement.Parse("<para>Added in 21.</para>")) &&
                IsRetainedRemarksParagraph(XElement.Parse("<para>Added in 21.0.</para>")),
            "hybrid remarks recognize Java major-version and dotted since paragraphs");
        var hybridRemarks = new XElement(
            "remarks",
            DocumentationElement(hybridSourceFragments[0]),
            new XElement(
                "para",
                string.Join(
                    " ",
                    hybridSourceFragments.Skip(5).Select(fragment => fragment.Text))),
            new XElement("para", "Added in 21."),
            ImporterSourceReference(hybridRemarksDocs),
            hybridAttribution);
        RemarksRefreshResult RefreshHybridRemarks(XElement remarks)
        {
            var text = $"<Docs>{remarks.ToString(SaveOptions.DisableFormatting)}</Docs>";
            var docs = XElement.Parse(text, LoadOptions.PreserveWhitespace);
            var owner = putIfAbsentOwner with
            {
                Order = 0,
                Docs = docs,
                Placeholders = [],
            };
            var refreshFile = new LoadedFile
            {
                Path = "CopyOnWriteArrayList.Reversed.hybrid-refresh.xml",
                RelativePath = "CopyOnWriteArrayList.Reversed.hybrid-refresh.xml",
                Text = text,
                Newline = "\n",
                HasUtf8Bom = false,
                Root = docs,
            };
            refreshFile.UpdateBlockOffsets(0, text);
            return RefreshImporterOwnedRemarks(text, refreshFile, owner, hybridRemarksDocs);
        }
        ImportReport ReportHybridRemarksRefresh(
            RemarksRefreshResult refreshed,
            string originalText)
        {
            var report = new ImportReport
            {
                Mode = "apply",
                Offline = true,
                MaxChanges = 1,
            };
            if (refreshed.Reason is not null)
            {
                report.Entries.Add(ReportEntry.Skipped(
                    "CopyOnWriteArrayList.Reversed.hybrid-refresh.xml",
                    putIfAbsentOwner.Id,
                    "remarks",
                    refreshed.Reason,
                    refreshed.Detail!,
                    hybridRemarksDocs.SourceUrl));
            }
            else if (!refreshed.Text.Equals(originalText, StringComparison.Ordinal))
            {
                report.Entries.Add(ReportEntry.Changed(
                    "would_apply",
                    "CopyOnWriteArrayList.Reversed.hybrid-refresh.xml",
                    putIfAbsentOwner.Id,
                    "remarks",
                    hybridRemarksDocs.SourceUrl));
            }
            return report;
        }
        void AssertHybridRemarksRefreshSkipped(
            XElement remarks,
            string description)
        {
            var originalText = $"<Docs>{remarks.ToString(SaveOptions.DisableFormatting)}</Docs>";
            var refreshed = RefreshHybridRemarks(remarks);
            var report = ReportHybridRemarksRefresh(refreshed, originalText);
            Assert(
                refreshed.Reason == "existing_remarks_not_importer_owned" &&
                    refreshed.Text.Equals(originalText, StringComparison.Ordinal) &&
                    report.Entries is
                    [{
                        Status: "skipped",
                        Target: "remarks",
                        Reason: "existing_remarks_not_importer_owned",
                        SourceUrl: var sourceUrl,
                    }] &&
                    sourceUrl == hybridRemarksDocs.SourceUrl,
                description);
        }
        var originalHybridReference = hybridRemarks.Elements().ElementAt(3).ToString(
            SaveOptions.DisableFormatting);
        var originalHybridAttribution = hybridRemarks.Elements().ElementAt(4).ToString(
            SaveOptions.DisableFormatting);
        var originalHybridText = $"<Docs>{hybridRemarks.ToString(SaveOptions.DisableFormatting)}</Docs>";
        var refreshedHybridRemarks = RefreshHybridRemarks(hybridRemarks);
        var refreshedHybridReport = ReportHybridRemarksRefresh(
            refreshedHybridRemarks,
            originalHybridText);
        var refreshedHybridElements = XElement.Parse(
            refreshedHybridRemarks.Text,
            LoadOptions.PreserveWhitespace).Element("remarks")!.Elements().ToList();
        var refreshedHybridReferenceIndex = refreshedHybridElements.FindIndex(element =>
            TryGetImporterSourceReferenceUrl(
                element.ToString(SaveOptions.DisableFormatting),
                out _));
        Assert(
            IsPotentialHybridImporterOwnedRemarks(hybridRemarks, "java") &&
                refreshedHybridRemarks.Reason is null &&
                string.Join(
                    " ",
                    refreshedHybridElements.Take(refreshedHybridReferenceIndex)
                        .Where(element => !IsRetainedRemarksParagraph(element))
                        .Select(element => NormalizeNormalWhitespace(element.Value))) ==
                    string.Join(
                        " ",
                        hybridSourceFragments.Select(fragment =>
                            NormalizeNormalWhitespace(fragment.Text))) &&
                refreshedHybridElements.Take(refreshedHybridReferenceIndex)
                    .Count(IsRetainedRemarksParagraph) == 1 &&
                refreshedHybridElements[refreshedHybridReferenceIndex].ToString(
                    SaveOptions.DisableFormatting).Equals(
                        originalHybridReference,
                        StringComparison.Ordinal) &&
                refreshedHybridElements[^1].ToString(SaveOptions.DisableFormatting).Equals(
                    originalHybridAttribution,
                    StringComparison.Ordinal) &&
                refreshedHybridReport.Entries is
                [{
                    Status: "would_apply",
                    Target: "remarks",
                    SourceUrl: var refreshedSourceUrl,
                }] &&
                refreshedSourceUrl == hybridRemarksDocs.SourceUrl,
            "hybrid Java remarks add missing exact source fragments, preserve provenance and attribution, and report the refresh");
        var hybridWithAuthoredProse = new XElement(hybridRemarks);
        hybridWithAuthoredProse.AddFirst(new XElement("para", "Authored prose must survive."));
        AssertHybridRemarksRefreshSkipped(
            hybridWithAuthoredProse,
            "hybrid remarks preserve and report unmatched authored prose");
        var hybridWithAppendedProse = new XElement(hybridRemarks);
        hybridWithAppendedProse.Elements().First().Value =
            hybridRemarksDocs.Paragraphs[0].Text + " Compatibility note.";
        AssertHybridRemarksRefreshSkipped(
            hybridWithAppendedProse,
            "hybrid remarks preserve and report source paragraphs with appended authored prose");
        var hybridWithInlineMarkup = new XElement(hybridRemarks);
        hybridWithInlineMarkup.Elements().First().ReplaceNodes(
            new XText(hybridRemarksDocs.Paragraphs[0].Text + " "),
            new XElement("c", "compatibility note"));
        AssertHybridRemarksRefreshSkipped(
            hybridWithInlineMarkup,
            "hybrid remarks preserve and report source paragraphs with inline markup");
        var hybridWithMismatchedSource = new XElement(hybridRemarks);
        hybridWithMismatchedSource.Elements().First().Value =
            "This paragraph does not exactly match the mapped source.";
        AssertHybridRemarksRefreshSkipped(
            hybridWithMismatchedSource,
            "hybrid remarks preserve and report mismatched source paragraphs");
        var hybridWithRepeatedAnnotation = new XElement(hybridRemarks);
        hybridWithRepeatedAnnotation.Elements()
            .Single(IsRetainedRemarksParagraph)
            .AddAfterSelf(new XElement("para", "Added in 21.0."));
        AssertHybridRemarksRefreshSkipped(
            hybridWithRepeatedAnnotation,
            "hybrid remarks preserve and report repeated retained annotations");
        var reorderedHybrid = new XElement(
            "remarks",
            DocumentationElement(hybridSourceFragments[1]),
            DocumentationElement(hybridSourceFragments[0]),
            new XElement(
                "para",
                string.Join(
                    " ",
                    hybridSourceFragments.Skip(5).Select(fragment => fragment.Text))),
            new XElement("para", "Added in 21."),
            ImporterSourceReference(hybridRemarksDocs),
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        var mismatchedHybridProvenance = new XElement(
            "remarks",
            DocumentationElement(hybridSourceFragments[0]),
            new XElement(
                "para",
                string.Join(
                    " ",
                    hybridSourceFragments.Skip(5).Select(fragment => fragment.Text))),
            new XElement("para", "Added in 21."),
            ImporterSourceReference(hybridRemarksDocs with
            {
                SourceUrl = JavaReference +
                    "java.base/java/util/concurrent/ConcurrentMap.html#putIfAbsent(K,V)",
            }),
            XElement.Parse($"<para>{AndroidAttribution}</para>"));
        AssertHybridRemarksRefreshSkipped(
            reorderedHybrid,
            "hybrid remarks preserve and report out-of-order source fragments");
        AssertHybridRemarksRefreshSkipped(
            mismatchedHybridProvenance,
            "hybrid remarks preserve and report mismatched source provenance");
        const string directEquivalentRemarksText =
            "<Docs>\n  <remarks>To be added.</remarks>\n</Docs>";
        var directEquivalentRemarks = XDocument.Parse(directEquivalentRemarksText)
            .Root!.Element("remarks")!;
        var directEquivalentPlaceholder = Placeholder.Create(directEquivalentRemarks, 0);
        var directEquivalentReplacement = ReplacementFor(
            directEquivalentPlaceholder,
            equivalentDocs);
        Assert(
            directEquivalentReplacement.Remarks?.SequenceEqual(equivalentDocs.Paragraphs) == true,
            "direct remarks replacement retains ordered prose, code, and prose source fragments");
        Assert(
            TryReplacePlaceholder(
                directEquivalentRemarksText,
                new DocsBlock(0, 0, directEquivalentRemarksText.Length),
                directEquivalentPlaceholder,
                directEquivalentReplacement,
                out var directEquivalentCompleted,
                out _),
            "direct remarks placeholder replacement succeeds");
        var renderedDirectEquivalent = XDocument.Parse(directEquivalentCompleted)
            .Root!.Element("remarks")!;
        Assert(
            renderedDirectEquivalent.Elements().Select(element => element.Name.LocalName)
                .SequenceEqual(["para", "code", "para"]) &&
            renderedDirectEquivalent.Elements("para").Last().Value ==
                "except that the update is atomic.",
            "direct remarks placeholder preserves trailing atomicity prose");
        const string nestedEquivalentRemarksText =
            "<Docs>\n  <remarks>\n    <para>To be added.</para>\n  </remarks>\n</Docs>";
        var nestedEquivalentRemarks = XDocument.Parse(nestedEquivalentRemarksText)
            .Root!.Element("remarks")!;
        var nestedEquivalentPlaceholder = Placeholder.Create(
            nestedEquivalentRemarks.Element("para")!,
            0);
        Assert(
            TryReplacePlaceholder(
                nestedEquivalentRemarksText,
                new DocsBlock(0, 0, nestedEquivalentRemarksText.Length),
                nestedEquivalentPlaceholder,
                ReplacementFor(nestedEquivalentPlaceholder, equivalentDocs),
                out var nestedEquivalentCompleted,
                out _),
            "nested remarks placeholder replacement succeeds");
        var renderedNestedEquivalent = XDocument.Parse(nestedEquivalentCompleted)
            .Root!.Element("remarks")!;
        Assert(
            renderedNestedEquivalent.Elements().Select(element => element.Name.LocalName)
                .SequenceEqual(["para", "code", "para"]) &&
            renderedNestedEquivalent.Elements("para").Last().Value ==
                "except that the update is atomic.",
            "nested remarks placeholder preserves trailing atomicity prose");
        const string compactDirectEquivalentRemarksText =
            "<Docs><remarks>To be added.</remarks></Docs>";
        var compactDirectEquivalentRemarks = XDocument.Parse(compactDirectEquivalentRemarksText)
            .Root!.Element("remarks")!;
        var compactDirectEquivalentPlaceholder = Placeholder.Create(
            compactDirectEquivalentRemarks,
            0);
        Assert(
            TryReplacePlaceholder(
                compactDirectEquivalentRemarksText,
                new DocsBlock(0, 0, compactDirectEquivalentRemarksText.Length),
                compactDirectEquivalentPlaceholder,
                ReplacementFor(compactDirectEquivalentPlaceholder, equivalentDocs),
                out var compactDirectEquivalentCompleted,
                out _) &&
            XDocument.Parse(compactDirectEquivalentCompleted).Root!.Element("remarks")!
                .Elements().Select(element => element.Name.LocalName)
                .SequenceEqual(["para", "code", "para"]),
            "compact direct remarks placeholder replacement produces valid XML");
        const string compactNestedEquivalentRemarksText =
            "<Docs><remarks><para>To be added.</para></remarks></Docs>";
        var compactNestedEquivalentRemarks = XDocument.Parse(compactNestedEquivalentRemarksText)
            .Root!.Element("remarks")!;
        var compactNestedEquivalentPlaceholder = Placeholder.Create(
            compactNestedEquivalentRemarks.Element("para")!,
            0);
        Assert(
            TryReplacePlaceholder(
                compactNestedEquivalentRemarksText,
                new DocsBlock(0, 0, compactNestedEquivalentRemarksText.Length),
                compactNestedEquivalentPlaceholder,
                ReplacementFor(compactNestedEquivalentPlaceholder, equivalentDocs),
                out var compactNestedEquivalentCompleted,
                out _) &&
            XDocument.Parse(compactNestedEquivalentCompleted).Root!.Element("remarks")!
                .Elements().Select(element => element.Name.LocalName)
                .SequenceEqual(["para", "code", "para"]),
            "compact nested remarks placeholder replacement produces valid XML");

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
        const string staleSetTitleSourceUrl =
            "https://developer.android.com/reference/android/example/Widget#setTitle(java.lang.String)";
        var importerStaleReference = ImporterSourceReference(
            mappedDocs with { SourceUrl = staleSetTitleSourceUrl }).ToString(
                SaveOptions.DisableFormatting);
        var updatedWithImporterStaleReference = Regex.Replace(
            updated,
            @"<para><format type=""text/html""><a href=""https://developer\.android\.com/reference/android/example/Widget#setTitle\(java\.lang\.String\)"" title=""Reference documentation"">.*?</a></format></para>",
            importerStaleReference,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert(
            !updatedWithImporterStaleReference.Equals(updated, StringComparison.Ordinal),
            "stale source fixture uses the exact importer reference structure");
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
        file.UpdateBlockOffsets(setTitle.Order, updatedWithImporterStaleReference);
        var withRemarks = AddSourceDocumentationIfSafe(
            updatedWithImporterStaleReference,
            file,
            setTitle,
            mappedDocs);
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
        var currentImporterReference = ImporterSourceReference(mappedDocs).ToString(
            SaveOptions.DisableFormatting);
        var sourceReferenceCdata =
            $"<![CDATA[Example XML: {currentImporterReference}]]>";
        var sourceReferenceComment =
            $"<!-- Example XML: {currentImporterReference} -->";
        var sourceReferenceProcessingInstruction =
            $"<?example {currentImporterReference} ?>";
        var literalSourceReferenceFixture = updatedWithImporterStaleReference.Replace(
            importerStaleReference,
            $"{sourceReferenceCdata}{file.Newline}          {sourceReferenceComment}{file.Newline}          {sourceReferenceProcessingInstruction}{file.Newline}          {importerStaleReference}",
            StringComparison.Ordinal);
        file.UpdateBlockOffsets(setTitle.Order, literalSourceReferenceFixture);
        var literalSourceReferenceBlock = literalSourceReferenceFixture[
            file.DocsBlocks[setTitle.Order].Start..
            file.DocsBlocks[setTitle.Order].End];
        var staleLiteralSourceCleanup = RemoveStaleSourceLinks(
            literalSourceReferenceBlock,
            mappedDocs.SourceUrl,
            removeAll: false);
        var literalSourceReferenceReport = new ImportReport
        {
            Mode = "apply",
            Offline = true,
            MaxChanges = 1,
        };
        var literalSourceReferenceUpdated = AddSourceDocumentationIfSafe(
            literalSourceReferenceFixture,
            file,
            setTitle,
            mappedDocs,
            out var literalSourceReferenceSkip);
        if (literalSourceReferenceSkip is not null)
        {
            ReportSourceReferenceCleanupSkip(
                literalSourceReferenceReport,
                file,
                setTitle,
                mappedDocs.SourceUrl,
                literalSourceReferenceSkip);
        }
        var literalSourceReferenceDocs = XDocument.Parse(
            literalSourceReferenceUpdated,
            LoadOptions.PreserveWhitespace).Root!
            .Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "SetTitle")
            .Element("Docs")!;
        Assert(
            staleLiteralSourceCleanup.Skip is null &&
                staleLiteralSourceCleanup.RemovedCount == 1 &&
                staleLiteralSourceCleanup.Text.Contains(
                    sourceReferenceCdata,
                    StringComparison.Ordinal) &&
                staleLiteralSourceCleanup.Text.Contains(
                    sourceReferenceComment,
                    StringComparison.Ordinal) &&
                staleLiteralSourceCleanup.Text.Contains(
                    sourceReferenceProcessingInstruction,
                    StringComparison.Ordinal) &&
                literalSourceReferenceSkip is null &&
                literalSourceReferenceReport.Entries.Count == 0 &&
                literalSourceReferenceUpdated.Contains(
                    sourceReferenceCdata,
                    StringComparison.Ordinal) &&
                literalSourceReferenceUpdated.Contains(
                    sourceReferenceComment,
                    StringComparison.Ordinal) &&
                literalSourceReferenceUpdated.Contains(
                    sourceReferenceProcessingInstruction,
                    StringComparison.Ordinal) &&
                CountImporterSourceReferences(
                    literalSourceReferenceDocs,
                    mappedDocs.SourceUrl) == 1 &&
                !literalSourceReferenceDocs.Descendants("para").Any(paragraph =>
                    TryGetImporterSourceReferenceUrl(paragraph, out var sourceUrl) &&
                    UrlsEqual(sourceUrl, staleSetTitleSourceUrl)),
            "parser-backed source cleanup ignores CDATA/comment literals, removes the actual stale importer reference, and applies one real current reference");
        var staleLiteralOnlyCleanup = RemoveStaleSourceLinks(
            $"<Docs><remarks><![CDATA[{importerStaleReference}]]><!-- {importerStaleReference} --><?example {importerStaleReference} ?>{currentImporterReference}</remarks></Docs>",
            mappedDocs.SourceUrl,
            removeAll: false);
        Assert(
            staleLiteralOnlyCleanup.Skip is null &&
                staleLiteralOnlyCleanup.RemovedCount == 0 &&
                staleLiteralOnlyCleanup.Text.Contains(
                    $"<![CDATA[{importerStaleReference}]]>",
                    StringComparison.Ordinal) &&
                staleLiteralOnlyCleanup.Text.Contains(
                    $"<!-- {importerStaleReference} -->",
                    StringComparison.Ordinal) &&
                staleLiteralOnlyCleanup.Text.Contains(
                    $"<?example {importerStaleReference} ?>",
                    StringComparison.Ordinal) &&
                CountImporterSourceReferences(
                    staleLiteralOnlyCleanup.Text,
                    mappedDocs.SourceUrl) == 1,
            "stale source-reference markup in CDATA/comments is never removed or allowed to remove the real importer reference");
        var mixedSourceReferenceCleanup = RemoveStaleSourceLinks(
            $"<Docs><remarks>Before {importerStaleReference}After</remarks></Docs>",
            mappedDocs.SourceUrl,
            removeAll: false);
        var mixedSourceReferenceReport = new ImportReport
        {
            Mode = "apply",
            Offline = true,
            MaxChanges = 1,
        };
        if (mixedSourceReferenceCleanup.Skip is not null)
        {
            ReportSourceReferenceCleanupSkip(
                mixedSourceReferenceReport,
                file,
                setTitle,
                mappedDocs.SourceUrl,
                mixedSourceReferenceCleanup.Skip);
        }
        Assert(
            mixedSourceReferenceCleanup.Skip?.Reason ==
                "source_reference_mixed_content" &&
                mixedSourceReferenceReport.Entries.SingleOrDefault() is
                {
                    Status: "skipped",
                    Target: "remarks",
                    Reason: "source_reference_mixed_content",
                } &&
                mixedSourceReferenceCleanup.Text.Contains(
                    $"Before {importerStaleReference}After",
                    StringComparison.Ordinal),
            "mixed-content source-reference cleanup conservatively skips and reports instead of collapsing authored text");
        const string nestedBuilderAnchor =
            "https://developer.android.com/reference/android/example/Widget.Builder#Widget$Builder()";
        const string builderAnchor =
            "https://developer.android.com/reference/android/example/Widget.Builder#Builder()";
        var staleNestedBuilderReference = ImporterSourceReference(
            mappedDocs with { SourceUrl = nestedBuilderAnchor }).ToString(
                SaveOptions.DisableFormatting);
        var staleNestedBuilderText =
            $"<Docs><remarks>{Environment.NewLine}{staleNestedBuilderReference}{Environment.NewLine}</remarks></Docs>";
        Assert(
            !RemoveStaleSourceLinks(
                staleNestedBuilderText,
                builderAnchor,
                removeAll: false).Text.Contains(nestedBuilderAnchor, StringComparison.Ordinal),
            "stale nested builder constructor link was removed");
        var nestedConstructorText =
            $"<Docs><remarks>{Environment.NewLine}<para><format type=\"text/html\"><a href=\"{nestedBuilderAnchor}\" " +
            $"title=\"Reference documentation\">Android reference for <code>Widget.Builder.Widget$Builder()</code>.</a></format></para>{Environment.NewLine}</remarks></Docs>";
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
            "Widget.Builder.Widget$Builder()",
            "Widget.Builder.Widget$Builder() unrelated$code",
            StringComparison.Ordinal);
        Assert(
            NormalizeStaleNestedConstructorLinks(
                nestedConstructorWithUnrelatedDollar,
                new DocsBlock(0, 0, nestedConstructorWithUnrelatedDollar.Length))
                .Contains("unrelated$code", StringComparison.Ordinal),
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
        file.UpdateBlockOffsets(favorite.Order, favoriteText);
        var selfClosingRemarksOwner = file.Owners.Single(owner =>
            owner.Id.Contains("EmptyRemarks", StringComparison.Ordinal));
        var completedEmptyRemarks = AddSourceDocumentationIfSafe(
            favoriteText,
            file,
            selfClosingRemarksOwner,
            equivalentDocs);
        var renderedEmptyRemarks = XDocument.Parse(completedEmptyRemarks)
            .Root!.Element("Members")!.Elements("Member")
            .Single(member => (string?)member.Attribute("MemberName") == "EmptyRemarks")
            .Element("Docs")!.Element("remarks")!;
        Assert(
            renderedEmptyRemarks.Elements().Take(3).Select(element => element.Name.LocalName)
                .SequenceEqual(["para", "code", "para"]) &&
            renderedEmptyRemarks.Elements("para").Take(2).Last().Value ==
                "except that the update is atomic.",
            "self-closing remarks control preserves all ordered source fragments");
        file.UpdateBlockOffsets(setTitle.Order, favoriteText);

        var completeRemarksDocs = favoriteResult.Docs! with
        {
            Paragraphs =
            [
                new SourceParagraph("The first complete contract paragraph.", IsCode: false),
                new SourceParagraph("The second complete contract paragraph.", IsCode: false),
                new SourceParagraph("result.setAuthentication(authentication);", IsCode: true),
            ],
        };
        const string completeRemarksText = "<Docs><remarks>To be added.</remarks></Docs>";
        var completeRemarksElement = XDocument.Parse(completeRemarksText).Root!.Element("remarks")!;
        var completeRemarksPlaceholder = Placeholder.Create(completeRemarksElement, 0);
        var completeRemarksReplacement = ReplacementFor(
            completeRemarksPlaceholder,
            completeRemarksDocs);
        Assert(
            completeRemarksReplacement.Remarks?.SequenceEqual(completeRemarksDocs.Paragraphs) == true,
            "remarks replacement retains every usable source paragraph and code block in order");
        Assert(
            TryReplacePlaceholder(
                completeRemarksText,
                new DocsBlock(0, 0, completeRemarksText.Length),
                completeRemarksPlaceholder,
                completeRemarksReplacement,
                out var completedRemarksText,
                out _),
            "complete remarks replacement succeeds");
        var renderedCompleteRemarks = XDocument.Parse(completedRemarksText).Root!.Element("remarks")!;
        Assert(
            renderedCompleteRemarks.Elements().Select(element => element.Name.LocalName).SequenceEqual(
                ["para", "para", "code"]) &&
            renderedCompleteRemarks.Elements("para").Select(element => element.Value).SequenceEqual(
                ["The first complete contract paragraph.", "The second complete contract paragraph."]) &&
            renderedCompleteRemarks.Element("code")?.Value == "result.setAuthentication(authentication);" &&
            (string?)renderedCompleteRemarks.Element("code")?.Attribute("lang") == "text/java",
            "complete remarks replacement renders source prose and code in order");
        const string compactParaRemarksText =
            "<Docs><remarks><para>To be added.</para></remarks></Docs>";
        var compactParaRemarks = XDocument.Parse(compactParaRemarksText)
            .Root!.Element("remarks")!;
        var compactParaPlaceholder = Placeholder.Create(
            compactParaRemarks.Element("para")!,
            0);
        Assert(
            TryReplacePlaceholder(
                compactParaRemarksText,
                new DocsBlock(0, 0, compactParaRemarksText.Length),
                compactParaPlaceholder,
                ReplacementFor(compactParaPlaceholder, completeRemarksDocs),
                out var completedCompactParaRemarksText,
                out _),
            "compact para remarks replacement succeeds");
        var completedCompactParaRemarks = XDocument.Parse(completedCompactParaRemarksText)
            .Root!.Element("remarks")!;
        Assert(
            completedCompactParaRemarks.Elements().Select(element => element.Name.LocalName).SequenceEqual(
                ["para", "para", "code"]) &&
            completedCompactParaRemarks.Elements("para").Select(element => element.Value).SequenceEqual(
                ["The first complete contract paragraph.", "The second complete contract paragraph."]) &&
            completedCompactParaRemarks.Element("code")?.Value ==
                "result.setAuthentication(authentication);",
            "compact para remarks replacement has structured XML without ancestor markup");

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
        var metadataReplacement = ReplacementFor(metadataRepair, completeRemarksDocs);
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
            repairedMetadataProse == NormalizeText(metadataReplacement.Text!),
            "importer metadata remarks placeholder retains source prose");
        Assert(
            repairedMetadataText.Contains("The second complete contract paragraph.", StringComparison.Ordinal) &&
            repairedMetadataText.Contains("result.setAuthentication(authentication);", StringComparison.Ordinal) &&
            repairedMetadataText.Contains("Reference documentation", StringComparison.Ordinal),
            "importer metadata remarks preserves reference metadata");
        var cleanedMissingRemarks = RemoveImporterRemarksMetadata(metadataRepairText);
        Assert(
            cleanedMissingRemarks.Skip?.Reason == "source_reference_mixed_content" &&
                cleanedMissingRemarks.Text.Equals(metadataRepairText, StringComparison.Ordinal),
            "inline importer metadata is preserved when removing it could collapse mixed content");
        var inlineElementMetadata =
            $"<Docs><remarks><c>Before</c>{ImporterSourceReference(completeRemarksDocs)}<c>After</c></remarks></Docs>";
        var preservedInlineElementMetadata = RemoveImporterRemarksMetadata(
            inlineElementMetadata);
        Assert(
            preservedInlineElementMetadata.Skip?.Reason ==
                "source_reference_mixed_content" &&
                preservedInlineElementMetadata.Text.Equals(
                    inlineElementMetadata,
                    StringComparison.Ordinal) &&
                !preservedInlineElementMetadata.Text.Contains(
                    "<c>Before</c><c>After</c>",
                    StringComparison.Ordinal),
            "metadata deletion preserves unseparated visible inline-element siblings");
        var legacyImporterAttribution = $"<para>{LegacyAndroidAttribution}</para>";
        var knownLegacyMetadata = $"<Docs><remarks>{file.Newline}  " +
            legacyImporterAttribution + $"{file.Newline}</remarks></Docs>";
        var removedLegacyMetadata = RemoveImporterRemarksMetadata(knownLegacyMetadata);
        Assert(
            IsLegacyImporterMetadataParagraph(
                XElement.Parse(legacyImporterAttribution)) &&
                removedLegacyMetadata.Skip is null &&
                !removedLegacyMetadata.Text.Contains(
                    "https://developers.google.com/terms/site-policies",
                    StringComparison.Ordinal),
            "known legacy importer attribution is recognized and removed as metadata");
        var legacyAttributionWithAuthoredText = legacyImporterAttribution.Replace(
            "</para>",
            " Authored attribution note.</para>",
            StringComparison.Ordinal);
        var legacyAttributionWithAuthoredMarkup = legacyImporterAttribution.Replace(
            "</para>",
            "<see cref=\"T:Java.Lang.Object\" /></para>",
            StringComparison.Ordinal);
        foreach (var unsafeLegacyAttribution in new[]
        {
            legacyAttributionWithAuthoredText,
            legacyAttributionWithAuthoredMarkup,
        })
        {
            var unsafeLegacyMetadata = $"<Docs><remarks>{file.Newline}  " +
                unsafeLegacyAttribution + $"{file.Newline}</remarks></Docs>";
            var preservedLegacyMetadata = RemoveImporterRemarksMetadata(
                unsafeLegacyMetadata);
            var unsafeLegacyDocs = XElement.Parse(
                unsafeLegacyMetadata,
                LoadOptions.PreserveWhitespace);
            var unsafeLegacyFile = new LoadedFile
            {
                Path = "legacy-attribution-fixture.xml",
                RelativePath = "legacy-attribution-fixture.xml",
                Text = unsafeLegacyMetadata,
                Newline = "\n",
                HasUtf8Bom = false,
                Root = unsafeLegacyDocs,
            };
            unsafeLegacyFile.UpdateBlockOffsets(0, unsafeLegacyMetadata);
            var unsafeLegacyOwner = setTitle with
            {
                Order = 0,
                Docs = unsafeLegacyDocs,
                Placeholders = [],
            };
            var reportedLegacySkip = FindUnownedLegacyAttribution(
                unsafeLegacyFile,
                unsafeLegacyOwner);
            var legacyMetadataReport = new ImportReport
            {
                Mode = "dry-run",
                Offline = true,
                MaxChanges = 1,
            };
            ReportSourceReferenceCleanupSkip(
                legacyMetadataReport,
                unsafeLegacyFile,
                unsafeLegacyOwner,
                copiedDescriptionRepairDocs.SourceUrl,
                reportedLegacySkip!);
            Assert(
                !IsLegacyImporterMetadataParagraph(
                    XElement.Parse(unsafeLegacyAttribution)) &&
                    !HasMetadataOnlyRemarks(unsafeLegacyMetadata) &&
                    preservedLegacyMetadata.Skip?.Reason ==
                        "legacy_attribution_not_importer_owned" &&
                    reportedLegacySkip?.Reason ==
                        "legacy_attribution_not_importer_owned" &&
                    preservedLegacyMetadata.Text.Equals(
                        unsafeLegacyMetadata,
                        StringComparison.Ordinal) &&
                    legacyMetadataReport.Entries.Single() is
                    {
                        Status: "skipped",
                        Target: "remarks",
                        Reason: "legacy_attribution_not_importer_owned",
                    },
                "legacy attribution with authored text or markup is preserved and reported");
        }
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
            favoriteResult.Docs.SourceUrl).Text;
        Assert(
            CountImporterSourceReferences(
                deduplicatedMetadataRepairText,
                favoriteResult.Docs.SourceUrl) == 1 &&
            deduplicatedMetadataRepairText.Contains(userReference, StringComparison.Ordinal) &&
            deduplicatedMetadataRepairText.Contains(AndroidAttribution, StringComparison.Ordinal) &&
            RemoveDuplicateSourceLinks(
                deduplicatedMetadataRepairText,
                favoriteResult.Docs.SourceUrl).Text.Equals(
                    deduplicatedMetadataRepairText,
                    StringComparison.Ordinal),
            "metadata-only repairs deduplicate importer references without removing user content");
        var emptyMetadataRepairText =
            "<Docs>\n  <remarks>\n    <para></para>\n    \n" +
            $"    {ImporterSourceReference(favoriteResult.Docs!)}\n" +
            $"    <para>{AndroidAttribution}</para>\n" +
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
                    .Elements("para").First().Value) == NormalizeText(metadataReplacement.Text!) &&
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
                        .Elements("para").First().Value) == NormalizeText(metadataReplacement.Text!),
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
                repairedDirectPlaceholderText.Contains(metadataReplacement.Text!, StringComparison.Ordinal),
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
                "<p>Use the following code:<button type=\"button\">Copy</button></p>") ==
                "Use the following code:",
            "Javadoc button controls are excluded from prose");
        Assert(
            SourcePage.HtmlText(
                "<p>calling AccessibilityService<code><a href=\"#getWindows\">AccessibilityService.getWindows()</a></code> will return an empty list</p>") ==
                "calling AccessibilityService.getWindows() will return an empty list",
            "adjacent Android receiver text and linked member reference are not duplicated");
        Assert(
            SourcePage.HtmlText(
                "<p>The callback will occur on the services's main thread if the handler is null.</p>") ==
                "The callback will occur on the service's main thread if the handler is null.",
            "known Android service-thread typo is corrected");
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
        var terminalNestedListParagraphs = SourcePage.ExtractParagraphs(
            "<p>Required controls <em>include</em></p><ul><li>First<ul><li>Nested</li></ul></li><li>Second</li></ul>");
        Assert(
            terminalNestedListParagraphs.Count == 1 &&
                terminalNestedListParagraphs[0].Text == "Required controls include; First; Nested; Second",
            "terminal nested lists remain in their introducing paragraph");
        var multiBridgeParagraphs = SourcePage.ExtractParagraphs(
            "<p>First:</p><ul><li>One</li><li>Two<ul><li>Nested</li></ul></li></ul><p>Middle.</p><p>Second:</p><ol><li>Three</li><li>Four</li></ol><p>Last.</p>");
        Assert(
            multiBridgeParagraphs.Count == 4 &&
                multiBridgeParagraphs[0].Text == "First: One; Two; Nested" &&
                multiBridgeParagraphs[1].Text == "Middle." &&
                multiBridgeParagraphs[2].Text == "Second: Three; Four" &&
                multiBridgeParagraphs[3].Text == "Last.",
            "multiple list bridges retain following paragraphs and nested list content");
        var missingJavaSignaturePeriod = SourcePage.ExtractBlocks(
            "<div class=\"block\">Sets a value.<p>The method signature is of the form <code>(T value)void</code></p><p>The symbolic type descriptor must match.</p></div>");
        Assert(
            missingJavaSignaturePeriod.Count == 1 &&
                missingJavaSignaturePeriod[0].Text ==
                "Sets a value. The method signature is of the form (T value)void. The symbolic type descriptor must match.",
            "Java signature paragraph boundaries preserve a sentence separator");

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
        var codeBearingListDocs = new SourceDocs(
            "",
            [
                new SourceParagraph("The pattern has these colors: black; red; green; blue", false),
                new SourceParagraph("builder.setPattern();", true),
            ],
            [],
            "",
            [],
            "https://developer.android.com/reference/android/example/ColorBars#VALUE",
            "VALUE",
            "android");
        var codeBearingExpectedSource =
            "<para><format type=\"text/html\"><a href=\"https://developer.android.com/reference/android/example/ColorBars#VALUE\" " +
            "title=\"Reference documentation\">Android reference for <code>VALUE</code>.</a></format></para>";
        var codeBearingListSummary = XElement.Parse(
            "<summary>" +
            "<para>The pattern has these colors:</para>" +
            "<code lang=\"text/java\">builder.setPattern();</code>" +
            codeBearingExpectedSource +
            $"<para>{AndroidAttribution}</para>" +
            "</summary>");
        Assert(
            HasImporterOwnedEnumMetadata(codeBearingListSummary, codeBearingListDocs),
            "code-bearing enum fixture contains exact importer metadata");
        Assert(
            HasImporterOwnedEnumListGap(codeBearingListSummary, codeBearingListDocs),
            "code-bearing importer-owned enum summaries can repair missing source lists");
        var codeBearingListWithAuthoredCodeXml = XElement.Parse(
            codeBearingListSummary.ToString(SaveOptions.DisableFormatting));
        codeBearingListWithAuthoredCodeXml.Element("code")!.AddFirst(
            new XElement("see", new XAttribute("cref", "T:Example.AuthoredCode")));
        Assert(
            !HasImporterOwnedEnumListGap(codeBearingListWithAuthoredCodeXml, codeBearingListDocs),
            "code-bearing enum summaries with authored code XML are preserved");
        codeBearingListSummary.Add(
            new XElement("para",
                new XElement("see", new XAttribute("cref", "T:Example.Authored"))));
        Assert(
            !HasImporterOwnedEnumListGap(codeBearingListSummary, codeBearingListDocs),
            "code-bearing enum summaries with authored XML are preserved");
        var enumMetadataWithoutTransfer =
            "<Docs><summary><para>Keep semantic prose.</para></summary><remarks>\n" +
            $"  {enumExpectedSource}\n" +
            $"  <para>{AndroidAttribution}</para>\n" +
            "</remarks></Docs>";
        Assert(
            RemoveEnumDiscardedMetadata(enumMetadataWithoutTransfer, enumMapped.Docs!).Text ==
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
            enumMapped.Docs!).Text;
        Assert(
            prunedEnumMetadata.Contains("<remarks />", StringComparison.Ordinal) &&
                !XElement.Parse(prunedEnumMetadata)
                    .Element("remarks")!
                    .Descendants("a")
                    .Any(),
            "enum remarks metadata self-closes after verified summary transfer");
        var enumSummaryWithCdata = enumMetadataWithTransfer.Replace(
            $"<para>{AndroidAttribution}</para></summary>",
            $"<para>{AndroidAttribution}</para><para>XML tooling may emit <![CDATA[</summary>]]> as a closing delimiter.</para></summary>",
            StringComparison.Ordinal);
        Assert(
            AddEnumSummaryMetadata(
                enumSummaryWithCdata,
                enumFile,
                enumFavorite,
                enumMapped.Docs!,
                allowCreation: false).Equals(enumSummaryWithCdata, StringComparison.Ordinal) &&
            XElement.Parse(enumSummaryWithCdata).Element("summary")!.Value.Contains(
                "</summary>",
                StringComparison.Ordinal),
            "enum summary metadata repair skips CDATA closing-tag text without aborting");
        var enumMetadataWithCdata = enumMetadataWithTransfer.Replace(
            "</remarks>",
            "  <para>XML tooling may emit <![CDATA[</para>]]> as a closing delimiter.</para>\n</remarks>",
            StringComparison.Ordinal);
        var prunedEnumMetadataWithCdata = RemoveEnumDiscardedMetadata(
            enumMetadataWithCdata,
            enumMapped.Docs!).Text;
        Assert(
            XElement.Parse(prunedEnumMetadataWithCdata).Element("remarks")!.Value.Contains(
                "</para>",
                StringComparison.Ordinal) &&
                prunedEnumMetadataWithCdata.Contains(
                    "<![CDATA[</para>]]>",
                    StringComparison.Ordinal) &&
                !XElement.Parse(prunedEnumMetadataWithCdata)
                    .Element("remarks")!
                    .Descendants("a")
                    .Any(),
            "enum metadata cleanup preserves CDATA paragraphs and removes only exact metadata");
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
                enumMapped.Docs!).Text == enumMetadataWithUnrelatedPolicyLink,
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
            repositoryRoot,
            "tools",
            $"android-api-doc-importer-self-test-{Environment.ProcessId}");
        var forEachPipelinePath = Path.Combine(
            docsRoot,
            "Java.Util.Concurrent",
            $"ConcurrentLinkedQueue.importer-self-test-{Environment.ProcessId}.xml");
        var compactForEachPipelinePath = Path.Combine(
            docsRoot,
            "Java.Util.Concurrent",
            $"ConcurrentLinkedQueue.compact-importer-self-test-{Environment.ProcessId}.xml");
        var enumPipelinePath = Path.Combine(
            docsRoot,
            $"WidgetKind.compact-importer-self-test-{Environment.ProcessId}.xml");
        var dreamFocusPipelinePath = Path.Combine(
            docsRoot,
            "Android.Service.Dreams",
            $"DreamService.importer-self-test-{Environment.ProcessId}.xml");
        var zoneTransitionPipelinePath = Path.Combine(
            docsRoot,
            "Java.Time.Zone",
            $"ZoneOffsetTransitionRule.importer-self-test-{Environment.ProcessId}.xml");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var dreamFocusCache = Path.Combine(tempDirectory, "dream-focus-cache");
            Directory.CreateDirectory(dreamFocusCache);
            var dreamFocusCachePath = Path.Combine(
                dreamFocusCache,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dreamFocusRequest.Url)))
                    .ToLowerInvariant() + ".html");
            File.WriteAllText(dreamFocusCachePath, dreamFocusHtml, new UTF8Encoding(false));
            var dreamFocusFirstFill = $"""
                <Type Name="DreamService" FullName="Android.Service.Dreams.DreamService">
                  <Attributes><Attribute><AttributeName Language="C#">[Android.Runtime.Register("android/service/dreams/DreamService", DoNotGenerateAcw=true)]</AttributeName></Attribute></Attributes>
                  <Docs><summary>Retain this type summary.</summary><remarks /></Docs>
                  <Members>
                    <Member MemberName="OnWindowFocusChanged">
                      <MemberSignature Language="DocId" Value="{DreamFocusMemberId}" />
                      <MemberType>Method</MemberType>
                      <Attributes><Attribute><AttributeName Language="C#">[Android.Runtime.Register("onWindowFocusChanged", "(Z)V", "")]</AttributeName></Attribute></Attributes>
                      <Parameters><Parameter Name="hasFocus" Type="System.Boolean" /></Parameters>
                      <ReturnValue><ReturnType>System.Void</ReturnType></ReturnValue>
                      <Docs><param name="hasFocus">Retain this parameter.</param><summary>To be added.</summary><remarks>To be added.</remarks></Docs>
                    </Member>
                  </Members>
                </Type>
                """;
            File.WriteAllText(dreamFocusPipelinePath, dreamFocusFirstFill, new UTF8Encoding(false));
            int RunDreamFocusPipeline(string stage, int expectedChanges)
            {
                var reportPath = Path.Combine(tempDirectory, "dream-focus-" + stage);
                var exitCode = RunAsync(
                    [
                        "--path", dreamFocusPipelinePath,
                        "--namespace", "Android.Service.Dreams",
                        "--member", "OnWindowFocusChanged",
                        "--offline", "--cache", dreamFocusCache,
                        "--max-changes", "2", "--apply", "--report", reportPath,
                    ]).GetAwaiter().GetResult();
                using var report = JsonDocument.Parse(File.ReadAllText(reportPath + ".json"));
                Assert(
                    exitCode == 0 &&
                    report.RootElement.GetProperty("errorCount").GetInt32() == 0 &&
                    report.RootElement.GetProperty("appliedCount").GetInt32() == expectedChanges,
                    $"registered DreamService focus complete importer pipeline {stage}");
                return report.RootElement.GetProperty("filesChanged").GetInt32();
            }
            RunDreamFocusPipeline("first-fill", 2);
            var firstFilledDreamFocus = File.ReadAllText(dreamFocusPipelinePath);
            Assert(
                firstFilledDreamFocus.Contains(CorrectDreamFocusRemark, StringComparison.Ordinal) &&
                !firstFilledDreamFocus.Contains("onWindowFocusChangedNotLocked", StringComparison.Ordinal) &&
                firstFilledDreamFocus.Contains("Retain this parameter.", StringComparison.Ordinal),
                "actual registered callback first-fill preserves the official target identity and existing channels");
            var firstFilledDreamFocusBytes = File.ReadAllBytes(dreamFocusPipelinePath);
            Assert(
                RunDreamFocusPipeline("first-fill-repeat", 0) == 0 &&
                firstFilledDreamFocusBytes.SequenceEqual(File.ReadAllBytes(dreamFocusPipelinePath)),
                "registered callback first-fill follow-up is byte-identical");
            foreach (var legacy in new[] { false, true })
            {
                var staleFocusOutput = firstFilledDreamFocus.Replace(
                    CorrectDreamFocusRemark, IncorrectDreamFocusRemark, StringComparison.Ordinal);
                if (legacy)
                {
                    staleFocusOutput = staleFocusOutput.Replace(
                        "Android reference for <code>android.service.dreams.DreamService.onWindowFocusChanged</code>",
                        "Java documentation for <code>android.service.dreams.DreamService.onWindowFocusChanged(boolean)</code>",
                        StringComparison.Ordinal);
                }
                File.WriteAllText(dreamFocusPipelinePath, staleFocusOutput, new UTF8Encoding(false));
                RunDreamFocusPipeline("repair-" + legacy, 1);
                Assert(
                    File.ReadAllText(dreamFocusPipelinePath) == staleFocusOutput.Replace(
                        IncorrectDreamFocusRemark, CorrectDreamFocusRemark, StringComparison.Ordinal),
                    "complete pipeline repairs only the exact old paragraph with canonical or retained legacy reference");
                var repairedDreamFocusBytes = File.ReadAllBytes(dreamFocusPipelinePath);
                Assert(
                    RunDreamFocusPipeline("repair-repeat-" + legacy, 0) == 0 &&
                    repairedDreamFocusBytes.SequenceEqual(File.ReadAllBytes(dreamFocusPipelinePath)),
                    "complete pipeline repair follow-up has zero edits and identical bytes");
            }
            var unsupportedFocusOutput = firstFilledDreamFocus.Replace(
                CorrectDreamFocusRemark, IncorrectDreamFocusRemark, StringComparison.Ordinal);
            File.WriteAllText(dreamFocusPipelinePath, unsupportedFocusOutput, new UTF8Encoding(false));
            File.WriteAllText(
                dreamFocusCachePath,
                dreamFocusHtml.Replace(
                    "#onWindowFocusChanged(boolean)\">", "#other(boolean)\">", StringComparison.Ordinal),
                new UTF8Encoding(false));
            var unsupportedFocusBytes = File.ReadAllBytes(dreamFocusPipelinePath);
            Assert(
                RunDreamFocusPipeline("unproven-target", 0) == 0 &&
                unsupportedFocusBytes.SequenceEqual(File.ReadAllBytes(dreamFocusPipelinePath)),
                "complete pipeline preserves old output when the source target does not prove the correction");
            File.WriteAllText(dreamFocusCachePath, dreamFocusHtml, new UTF8Encoding(false));
            var focusPipelineMismatches = new Dictionary<string, string>
            {
                ["member-mismatch"] = unsupportedFocusOutput.Replace(
                    DreamFocusMemberId, DreamFocusMemberId + ".Altered", StringComparison.Ordinal),
                ["registration-mismatch"] = unsupportedFocusOutput.Replace(
                    "\"(Z)V\"", "\"(I)V\"", StringComparison.Ordinal),
                ["source-reference-mismatch"] = unsupportedFocusOutput.Replace(
                    DreamFocusSourceUrl, DreamFocusSourceUrl + ".Altered", StringComparison.Ordinal),
                ["authored-provenance"] = unsupportedFocusOutput.Replace(
                    "Portions of this page", "Authored portions of this page", StringComparison.Ordinal),
                ["authored-paragraph"] = unsupportedFocusOutput.Replace(
                    IncorrectDreamFocusRemark,
                    IncorrectDreamFocusRemark + " Additional authored guidance.",
                    StringComparison.Ordinal),
                ["mixed-content"] = unsupportedFocusOutput.Replace(
                    IncorrectDreamFocusRemark, $"<c>{IncorrectDreamFocusRemark}</c>", StringComparison.Ordinal),
                ["cdata"] = unsupportedFocusOutput.Replace(
                    IncorrectDreamFocusRemark, $"<![CDATA[{IncorrectDreamFocusRemark}]]>", StringComparison.Ordinal),
                ["comment"] = unsupportedFocusOutput.Replace(
                    IncorrectDreamFocusRemark, $"<!--Keep-->{IncorrectDreamFocusRemark}", StringComparison.Ordinal),
                ["processing-instruction"] = unsupportedFocusOutput.Replace(
                    IncorrectDreamFocusRemark, $"<?keep guidance?>{IncorrectDreamFocusRemark}", StringComparison.Ordinal),
            };
            foreach (var (stage, mismatchText) in focusPipelineMismatches)
            {
                File.WriteAllText(dreamFocusPipelinePath, mismatchText, new UTF8Encoding(false));
                var mismatchBytes = File.ReadAllBytes(dreamFocusPipelinePath);
                Assert(
                    RunDreamFocusPipeline(stage, 0) == 0 &&
                    mismatchBytes.SequenceEqual(File.ReadAllBytes(dreamFocusPipelinePath)),
                    $"complete focus pipeline preserves byte-identical output for {stage}");
            }
            var zoneTransitionSource = LoadedFile.Load(
                repositoryRoot,
                Path.Combine(docsRoot, "Java.Time.Zone", "ZoneOffsetTransitionRule.xml"));
            zoneTransitionSource.SelectOwners(KnownUnsafeZoneTransitionMemberId);
            var zoneTransitionOwner = zoneTransitionSource.Owners.Single();
            var zoneTransitionHtml = File.ReadAllText(Path.Combine(
                fixtureRoot, "zone-offset-transition-rule-java-reference.html"));
            var zoneTransitionPage = SourcePage.Parse(
                zoneTransitionOwner.SourceRequest!, zoneTransitionHtml);
            var zoneTransitionMapping = MapOwner(
                zoneTransitionOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [zoneTransitionOwner.SourceRequest!.Url] =
                        SourceLoadResult.Success(zoneTransitionPage),
                });
            var unsafeZoneTransitionDocs = zoneTransitionMapping.Docs!;
            Assert(
                zoneTransitionOwner.Id == KnownUnsafeZoneTransitionMemberId &&
                zoneTransitionOwner.MemberRegistration?.Descriptor ==
                    "(Ljava/time/Month;ILjava/time/DayOfWeek;Ljava/time/LocalTime;ZLjava/time/zone/ZoneOffsetTransitionRule$TimeDefinition;Ljava/time/ZoneOffset;Ljava/time/ZoneOffset;Ljava/time/ZoneOffset;)Ljava/time/zone/ZoneOffsetTransitionRule;" &&
                unsafeZoneTransitionDocs.Parameters.Count == 9 &&
                unsafeZoneTransitionDocs.Parameters["time"] == KnownUnsafeZoneTransitionTime &&
                unsafeZoneTransitionDocs.UnsafeTargets?.ContainsKey("param:time") == true &&
                ReplacementFor(
                    new Placeholder(0, "param", "time", "param:time"),
                    unsafeZoneTransitionDocs).Reason == "source_channel_ambiguous",
                "the complete registered nine-parameter Java factory maps its exact source and excludes only the unsafe time channel");
            var rawZoneTransitionDocs = unsafeZoneTransitionDocs with { UnsafeTargets = null };
            var zoneTransitionSourceMismatches = new[]
            {
                rawZoneTransitionDocs with { SourceUrl = rawZoneTransitionDocs.SourceUrl + ".Altered" },
                rawZoneTransitionDocs with { SourceKind = "android" },
                rawZoneTransitionDocs with
                {
                    Parameters = new Dictionary<string, string>
                    {
                        ["time"] = KnownUnsafeZoneTransitionTime + " Additional source guidance.",
                    },
                },
                rawZoneTransitionDocs with
                {
                    Parameters = new Dictionary<string, string>
                    {
                        ["cutoverTime"] = KnownUnsafeZoneTransitionTime,
                    },
                },
            };
            Assert(
                zoneTransitionSourceMismatches.All(docs =>
                    ReferenceEquals(
                        WithoutKnownUnsafeJavaSourceChannels(KnownUnsafeZoneTransitionMemberId, docs),
                        docs)) &&
                ReferenceEquals(
                    WithoutKnownUnsafeJavaSourceChannels(
                        KnownUnsafeZoneTransitionMemberId + ".Altered", rawZoneTransitionDocs),
                    rawZoneTransitionDocs),
                "Java time exclusions require the exact source kind, canonical URL, managed member, parameter name, and complete source text");

            var zoneTransitionDocument = new XDocument(zoneTransitionSource.Root.Document!);
            zoneTransitionDocument.Root!.Element("Members")!.ReplaceNodes(
                new XElement(zoneTransitionOwner.Member!));
            var zoneFactoryDocs = zoneTransitionDocument.Root.Element("Members")!
                .Element("Member")!.Element("Docs")!;
            zoneFactoryDocs.ReplaceNodes(
                zoneTransitionOwner.Member!.Element("Parameters")!.Elements("Parameter")
                    .Select(parameter => new XElement(
                        "param", new XAttribute("name", (string)parameter.Attribute("Name")!), "To be added.")),
                new XElement("summary", "To be added."),
                new XElement("returns", "To be added."),
                new XElement("remarks", "To be added."));
            File.WriteAllText(
                zoneTransitionPipelinePath,
                zoneTransitionDocument.ToString(SaveOptions.DisableFormatting),
                new UTF8Encoding(false));
            var zoneTransitionCache = Path.Combine(tempDirectory, "zone-transition-cache");
            Directory.CreateDirectory(zoneTransitionCache);
            var zoneTransitionCacheKey = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(zoneTransitionOwner.SourceRequest!.Url))).ToLowerInvariant();
            File.WriteAllText(
                Path.Combine(zoneTransitionCache, zoneTransitionCacheKey + ".html"),
                zoneTransitionHtml, new UTF8Encoding(false));

            (int ExitCode, int Applied, bool TimeSkipped) RunZoneTransitionPipeline(string name)
            {
                var reportPath = Path.Combine(tempDirectory, name);
                var exitCode = RunAsync(
                    [
                        "--path", zoneTransitionPipelinePath,
                        "--namespace", "Java.Time.Zone",
                        "--member", KnownUnsafeZoneTransitionMemberId,
                        "--cache", zoneTransitionCache,
                        "--offline",
                        "--apply",
                        "--max-changes", "10",
                        "--report", reportPath,
                    ]).GetAwaiter().GetResult();
                using var report = JsonDocument.Parse(File.ReadAllText(reportPath + ".json"));
                return (
                    exitCode,
                    report.RootElement.GetProperty("appliedCount").GetInt32(),
                    report.RootElement.GetProperty("entries").EnumerateArray().Any(entry =>
                        entry.GetProperty("status").GetString() == "skipped" &&
                        entry.GetProperty("target").GetString() == "param:time" &&
                        entry.GetProperty("reason").GetString() == "source_channel_ambiguous"));
            }

            var zoneFirstFill = RunZoneTransitionPipeline("zone-first-fill");
            var zoneRemainingFill = RunZoneTransitionPipeline("zone-remaining-fill");
            var zoneFilledDocument = XDocument.Load(zoneTransitionPipelinePath);
            var zoneFilledDocs = zoneFilledDocument.Root!.Element("Members")!
                .Element("Member")!.Element("Docs")!;
            Assert(
                zoneFirstFill is (0, 10, true) &&
                zoneRemainingFill is (0, 1, true) &&
                zoneFilledDocs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").Value == "To be added." &&
                zoneFilledDocs.Elements().Count(element =>
                    element.Value.Contains("To be added.", StringComparison.Ordinal)) == 1 &&
                zoneFilledDocs.Elements("param").Where(parameter =>
                    (string?)parameter.Attribute("name") != "time").All(parameter =>
                        parameter.Value == unsafeZoneTransitionDocs.Parameters[
                            (string)parameter.Attribute("name")!]),
                "production first-fill imports all eleven safe channels while leaving only the time placeholder");
            var zoneFilledBytes = File.ReadAllBytes(zoneTransitionPipelinePath);
            var zoneFirstFillRepeat = RunZoneTransitionPipeline("zone-first-fill-repeat");
            Assert(
                zoneFirstFillRepeat is (0, 0, true) &&
                zoneFilledBytes.SequenceEqual(File.ReadAllBytes(zoneTransitionPipelinePath)),
                "production unsafe-channel exclusion is byte-identical and applies zero changes on repeat");

            zoneFilledDocs.Elements("param").Single(parameter =>
                (string?)parameter.Attribute("name") == "time").Value = KnownUnsafeZoneTransitionTime;
            zoneFilledDocs.Elements("param").Single(parameter =>
                (string?)parameter.Attribute("name") == "month").Value = "Keep this authored month documentation.";
            File.WriteAllText(
                zoneTransitionPipelinePath,
                zoneFilledDocument.ToString(SaveOptions.DisableFormatting),
                new UTF8Encoding(false));
            var previouslyOwnedZoneText = File.ReadAllText(zoneTransitionPipelinePath);
            var expectedWithdrawnZoneText = previouslyOwnedZoneText.Replace(
                $"<param name=\"time\">{KnownUnsafeZoneTransitionTime}</param>",
                "<param name=\"time\">To be added.</param>",
                StringComparison.Ordinal);
            var zoneWithdrawal = RunZoneTransitionPipeline("zone-prior-owned-withdrawal");
            Assert(
                zoneWithdrawal.ExitCode == 0 &&
                zoneWithdrawal.Applied == 1 &&
                expectedWithdrawnZoneText != previouslyOwnedZoneText &&
                File.ReadAllText(zoneTransitionPipelinePath) == expectedWithdrawnZoneText,
                "production withdrawal changes only the exact previous importer-owned time parameter");
            var zoneWithdrawnBytes = File.ReadAllBytes(zoneTransitionPipelinePath);
            var zoneWithdrawalRepeat = RunZoneTransitionPipeline("zone-withdrawal-repeat");
            Assert(
                zoneWithdrawalRepeat is (0, 0, true) &&
                zoneWithdrawnBytes.SequenceEqual(File.ReadAllBytes(zoneTransitionPipelinePath)),
                "production withdrawal is byte-identical and applies zero changes on repeat");

            Action<XElement>[] authoredZoneTimeChanges =
            [
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").Value += " Authored guidance.",
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").ReplaceNodes(
                        new XElement("c", KnownUnsafeZoneTransitionTime)),
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").ReplaceNodes(
                        new XCData(KnownUnsafeZoneTransitionTime)),
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").Add(new XComment("Authored.")),
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").Add(new XProcessingInstruction("keep", "authored")),
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").SetAttributeValue("authored", "true"),
                docs => docs.Elements("param").Single(parameter =>
                    (string?)parameter.Attribute("name") == "time").SetAttributeValue("name", "cutoverTime"),
                docs => docs.Add(new XElement("param", new XAttribute("name", "time"), KnownUnsafeZoneTransitionTime)),
                docs => docs.Element("remarks")!.AddFirst(new XElement("para", "Authored prose.")),
                docs => docs.Element("remarks")!.Add(new XComment("Authored.")),
                docs => docs.Element("remarks")!.Add(new XProcessingInstruction("keep", "authored")),
                docs => docs.Element("remarks")!.Element("para")!.ReplaceNodes(
                    new XCData(docs.Element("remarks")!.Element("para")!.Value)),
                docs => docs.Element("remarks")!.Element("para")!.ReplaceNodes(
                    new XElement("c", docs.Element("remarks")!.Element("para")!.Value)),
                docs => docs.Element("remarks")!.SetAttributeValue("authored", "true"),
                docs => docs.Element("remarks")!.Elements("para").Last().Descendants("a")
                    .Single().SetAttributeValue("href", KnownUnsafeZoneTransitionSourceUrl + ".Altered"),
                docs => docs.Element("remarks")!.Add(new XElement(docs.Element("remarks")!.Elements("para").Last())),
                docs => docs.Element("remarks")!.Elements("para").Last().Remove(),
                docs => docs.Add(new XComment("Authored.")),
                docs => docs.Add(new XProcessingInstruction("keep", "authored")),
                docs => docs.Add(new XText("Authored prose.")),
                docs => docs.Add(new XCData("Authored prose.")),
                docs => docs.SetAttributeValue("authored", "true"),
            ];
            var authoredZoneCase = 0;
            foreach (var change in authoredZoneTimeChanges)
            {
                var authored = XDocument.Parse(previouslyOwnedZoneText);
                change(authored.Root!.Element("Members")!.Element("Member")!.Element("Docs")!);
                File.WriteAllText(
                    zoneTransitionPipelinePath,
                    authored.ToString(SaveOptions.DisableFormatting),
                    new UTF8Encoding(false));
                var authoredFile = LoadedFile.Load(repositoryRoot, zoneTransitionPipelinePath);
                authoredFile.SelectOwners(KnownUnsafeZoneTransitionMemberId);
                var authoredOwner = authoredFile.Owners.Single();
                var preserved = RepairKnownUnsafeParameter(
                    authoredFile.Text, authoredFile, authoredOwner, unsafeZoneTransitionDocs);
                Assert(
                    !preserved.Repaired && preserved.Text == authoredFile.Text,
                    "Java time withdrawal preserves authored parameter/remarks content, CDATA, comments, processing instructions, and ownership mismatches");
                var authoredBytes = File.ReadAllBytes(zoneTransitionPipelinePath);
                var authoredRun = RunZoneTransitionPipeline($"zone-authored-{authoredZoneCase++}");
                Assert(
                    authoredRun.ExitCode == 0 &&
                    authoredRun.Applied == 0 &&
                    authoredBytes.SequenceEqual(File.ReadAllBytes(zoneTransitionPipelinePath)),
                    "production withdrawal preserves authored content and ownership mismatches byte-for-byte");
            }
            File.WriteAllText(zoneTransitionPipelinePath, previouslyOwnedZoneText, new UTF8Encoding(false));
            var ownedZoneFile = LoadedFile.Load(repositoryRoot, zoneTransitionPipelinePath);
            ownedZoneFile.SelectOwners(KnownUnsafeZoneTransitionMemberId);
            var ownedZoneOwner = ownedZoneFile.Owners.Single();
            var withdrawalSourceMismatches = zoneTransitionSourceMismatches.Concat(
                [
                    rawZoneTransitionDocs with
                    {
                        Paragraphs = [new SourceParagraph("Different source prose.", IsCode: false)],
                    },
                ]);
            Assert(
                withdrawalSourceMismatches.All(docs =>
                {
                    var preserved = RepairKnownUnsafeParameter(
                        ownedZoneFile.Text, ownedZoneFile, ownedZoneOwner, docs);
                    return !preserved.Repaired && preserved.Text == ownedZoneFile.Text;
                }) &&
                !RepairKnownUnsafeParameter(
                    ownedZoneFile.Text,
                    ownedZoneFile,
                    ownedZoneOwner with { Id = ownedZoneOwner.Id + ".Altered" },
                    unsafeZoneTransitionDocs).Repaired,
                "Java time withdrawal preserves mismatched source kinds, URLs, full text, parameter names, remarks, and managed members");
            foreach (var attribute in new[] { "Name", "Type" })
            {
                var alteredMember = new XElement(ownedZoneOwner.Member!);
                alteredMember.Element("Parameters")!.Elements("Parameter").ElementAt(3)
                    .SetAttributeValue(attribute, "Altered");
                var preserved = RepairKnownUnsafeParameter(
                    ownedZoneFile.Text,
                    ownedZoneFile,
                    ownedZoneOwner with { Member = alteredMember },
                    unsafeZoneTransitionDocs);
                Assert(
                    !preserved.Repaired && preserved.Text == ownedZoneFile.Text,
                    "Java time withdrawal preserves mismatched managed parameter metadata");
                var alteredDocument = XDocument.Parse(previouslyOwnedZoneText);
                alteredDocument.Root!.Element("Members")!.Element("Member")!
                    .Element("Parameters")!.Elements("Parameter").ElementAt(3)
                    .SetAttributeValue(attribute, "Altered");
                File.WriteAllText(
                    zoneTransitionPipelinePath,
                    alteredDocument.ToString(SaveOptions.DisableFormatting),
                    new UTF8Encoding(false));
                var alteredBytes = File.ReadAllBytes(zoneTransitionPipelinePath);
                var alteredRun = RunZoneTransitionPipeline($"zone-altered-parameter-{attribute}");
                Assert(
                    alteredRun.ExitCode == 0 &&
                    alteredRun.Applied == 0 &&
                    alteredBytes.SequenceEqual(File.ReadAllBytes(zoneTransitionPipelinePath)),
                    "production withdrawal preserves mismatched managed parameter metadata byte-for-byte");
            }

            var forEachSourcePath = Path.Combine(
                docsRoot,
                "Java.Util.Concurrent",
                "ConcurrentLinkedQueue.xml");
            var forEachSourceFile = LoadedFile.Load(repositoryRoot, forEachSourcePath);
            forEachSourceFile.SelectOwners(null, new InterfaceMemberResolver(docsRoot));
            var forEachOwner = forEachSourceFile.Owners.Single(owner =>
                owner.Id ==
                "M:Java.Util.Concurrent.ConcurrentLinkedQueue.ForEach(Java.Util.Functions.IConsumer)");
            var forEachSourcePage = SourcePage.Parse(
                forEachOwner.SourceRequest!,
                File.ReadAllText(Path.Combine(
                    fixtureRoot,
                    "concurrent-linked-queue-java-reference.html")));
            var forEachMapping = MapOwner(
                forEachOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [forEachOwner.SourceRequest!.Url] =
                        SourceLoadResult.Success(forEachSourcePage),
                });
            Assert(
                forEachOwner.MemberRegistration ==
                    new MemberRegistration(
                        "forEach",
                        "(Ljava/util/function/Consumer;)V",
                        false) &&
                    forEachOwner.Placeholders.Count == 0 &&
                    forEachMapping.Docs is
                    {
                        SourceKind: "java",
                        SourceUrl:
                            JavaReference +
                            "java.base/java/util/concurrent/ConcurrentLinkedQueue.html#forEach(java.util.function.Consumer)",
                        Summary:
                            "Performs the given action for each element of the Iterable until all elements have been processed or the action throws an exception.",
                    },
                "ConcurrentLinkedQueue ForEach production owner maps its exact Java source fixture");

            var forEachBlock = forEachSourceFile.DocsBlocks[forEachOwner.Order];
            var forEachBlockText = forEachSourceFile.Text[
                forEachBlock.Start..forEachBlock.End];
            var forEachDocs = XElement.Parse(
                forEachBlockText,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            Assert(
                TryGetElementSpan(
                    forEachBlockText,
                    forEachDocs.Element("summary")!,
                    out var forEachSummarySpan),
                "ConcurrentLinkedQueue ForEach copied summary is parser-located");
            var copiedSummary =
                "<summary>Description copied from interface: java.lang.Iterable</summary>";
            var copiedForEachBlock =
                forEachBlockText[..forEachSummarySpan.Start] +
                copiedSummary +
                forEachBlockText[forEachSummarySpan.End..];
            var copiedForEachDocs = XElement.Parse(
                copiedForEachBlock,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var originalForEachReference = copiedForEachDocs
                .Element("remarks")!
                .Elements("para")
                .Single(paragraph => TryGetImporterSourceReferenceUrl(
                    paragraph,
                    out var sourceUrl) &&
                    UrlsEqual(sourceUrl, forEachMapping.Docs!.SourceUrl));
            Assert(
                TryGetElementSpan(
                    copiedForEachBlock,
                    originalForEachReference,
                    out var originalForEachReferenceSpan),
                "ConcurrentLinkedQueue ForEach source reference is parser-located");
            var unsafeDuplicateReference =
                $"<c>Before</c>{ImporterSourceReference(forEachMapping.Docs!)}<c>After</c>";
            var forEachPipelineBlock =
                copiedForEachBlock[..originalForEachReferenceSpan.End] +
                unsafeDuplicateReference +
                copiedForEachBlock[originalForEachReferenceSpan.End..];
            var forEachPipelineText =
                forEachSourceFile.Text[..forEachBlock.Start] +
                forEachPipelineBlock +
                forEachSourceFile.Text[forEachBlock.End..];
            File.WriteAllText(
                forEachPipelinePath,
                forEachPipelineText,
                new UTF8Encoding(false));

            var forEachCacheDirectory = Path.Combine(tempDirectory, "for-each-cache");
            Directory.CreateDirectory(forEachCacheDirectory);
            var forEachCacheKey = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(forEachOwner.SourceRequest!.Url))).ToLowerInvariant();
            File.WriteAllText(
                Path.Combine(forEachCacheDirectory, forEachCacheKey + ".html"),
                File.ReadAllText(Path.Combine(
                    fixtureRoot,
                    "concurrent-linked-queue-java-reference.html")),
                new UTF8Encoding(false));
            var forEachReportPath = Path.Combine(tempDirectory, "for-each-pipeline");
            var forEachExitCode = RunAsync(
                [
                    "--path", forEachPipelinePath,
                    "--namespace", "Java.Util.Concurrent",
                    "--member", "ForEach",
                    "--offline",
                    "--cache", forEachCacheDirectory,
                    "--max-changes", "2",
                    "--apply",
                    "--report", forEachReportPath,
                ]).GetAwaiter().GetResult();
            var appliedForEachText = File.ReadAllText(forEachPipelinePath);
            var appliedForEachDocs = XDocument.Parse(
                appliedForEachText,
                LoadOptions.PreserveWhitespace)
                .Root!.Element("Members")!.Elements("Member")
                .Single(member => member.Elements("MemberSignature").Any(signature =>
                    (string?)signature.Attribute("Language") == "DocId" &&
                    (string?)signature.Attribute("Value") == forEachOwner.Id))
                .Element("Docs")!;
            var appliedForEachRemarks = appliedForEachDocs.Element("remarks")!;
            using var forEachReport = JsonDocument.Parse(
                File.ReadAllText(forEachReportPath + ".json"));
            var reportedUnsafeDuplicate = forEachReport.RootElement
                .GetProperty("entries")
                .EnumerateArray()
                .Any(entry =>
                    entry.GetProperty("status").GetString() == "skipped" &&
                    entry.GetProperty("target").GetString() == "remarks" &&
                    entry.GetProperty("reason").GetString() ==
                        "source_reference_mixed_content" &&
                    entry.GetProperty("sourceUrl").GetString() ==
                        forEachMapping.Docs!.SourceUrl);
            var forEachSummaryRepaired =
                appliedForEachDocs.Element("summary")?.Value ==
                forEachMapping.Docs!.Summary;
            var forEachReferencesPreserved = CountImporterSourceReferences(
                appliedForEachRemarks,
                forEachMapping.Docs.SourceUrl) == 2;
            var forEachMixedMarkupPreserved = appliedForEachText.Contains(
                    unsafeDuplicateReference,
                    StringComparison.Ordinal) &&
                !appliedForEachText.Contains(
                    "<c>Before</c><c>After</c>",
                    StringComparison.Ordinal);
            Assert(
                forEachExitCode == 0 &&
                    forEachSummaryRepaired &&
                    forEachReferencesPreserved &&
                    forEachMixedMarkupPreserved &&
                    reportedUnsafeDuplicate,
                $"production copied-summary repair preserves an unseparated duplicate source reference and reports the unsafe metadata deletion (exit={forEachExitCode}, summary={forEachSummaryRepaired}, references={forEachReferencesPreserved}, markup={forEachMixedMarkupPreserved}, report={reportedUnsafeDuplicate})");

            var compactForEachDocs = XElement.Parse(
                forEachBlockText,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            Assert(
                TryGetElementSpan(
                    forEachBlockText,
                    compactForEachDocs.Element("remarks")!,
                    out var compactForEachRemarksSpan),
                "ConcurrentLinkedQueue ForEach compact metadata remarks are parser-located");
            var compactForEachReference = ImporterSourceReference(
                forEachMapping.Docs!).ToString(SaveOptions.DisableFormatting);
            var compactForEachRemarks =
                $"<remarks>{compactForEachReference}" +
                $"{compactForEachReference}" +
                $"<para>{AndroidAttribution}</para></remarks>";
            var compactForEachBlock =
                forEachBlockText[..compactForEachRemarksSpan.Start] +
                compactForEachRemarks +
                forEachBlockText[compactForEachRemarksSpan.End..];
            var compactForEachBlockDocument = XElement.Parse(
                compactForEachBlock,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            Assert(
                TryGetElementSpan(
                    compactForEachBlock,
                    compactForEachBlockDocument.Element("param")!,
                    out var compactForEachParameterSpan),
                "ConcurrentLinkedQueue ForEach action parameter is parser-located");
            compactForEachBlock =
                compactForEachBlock[..compactForEachParameterSpan.Start] +
                "<param name=\"action\">To be added.</param>" +
                compactForEachBlock[compactForEachParameterSpan.End..];
            var compactForEachPipelineText =
                forEachSourceFile.Text[..forEachBlock.Start] +
                compactForEachBlock +
                forEachSourceFile.Text[forEachBlock.End..];
            File.WriteAllText(
                compactForEachPipelinePath,
                compactForEachPipelineText,
                new UTF8Encoding(false));

            var compactForEachReportPath = Path.Combine(
                tempDirectory,
                "compact-for-each-pipeline");
            var compactForEachExitCode = RunAsync(
                [
                    "--path", compactForEachPipelinePath,
                    "--namespace", "Java.Util.Concurrent",
                    "--member", "ForEach",
                    "--offline",
                    "--cache", forEachCacheDirectory,
                    "--max-changes", "1",
                    "--apply",
                    "--report", compactForEachReportPath,
                ]).GetAwaiter().GetResult();
            var appliedCompactForEachText = File.ReadAllText(compactForEachPipelinePath);
            var appliedCompactForEachDocs = XDocument.Parse(
                appliedCompactForEachText,
                LoadOptions.PreserveWhitespace)
                .Root!.Element("Members")!.Elements("Member")
                .Single(member => member.Elements("MemberSignature").Any(signature =>
                    (string?)signature.Attribute("Language") == "DocId" &&
                    (string?)signature.Attribute("Value") == forEachOwner.Id))
                .Element("Docs")!;
            var appliedCompactForEachRemarks =
                appliedCompactForEachDocs.Element("remarks")!;
            using var compactForEachReport = JsonDocument.Parse(
                File.ReadAllText(compactForEachReportPath + ".json"));
            var compactForEachCleanupSkip = compactForEachReport.RootElement
                .GetProperty("entries")
                .EnumerateArray()
                .Any(entry =>
                    entry.GetProperty("status").GetString() == "skipped" &&
                    entry.GetProperty("target").GetString() == "remarks" &&
                    entry.GetProperty("reason").GetString() ==
                        "source_reference_mixed_content" &&
                    entry.GetProperty("sourceUrl").GetString() ==
                        forEachMapping.Docs!.SourceUrl);
            var compactForEachContract = NormalizeText(
                forEachMapping.Docs!.Paragraphs.Single().Text);
            var compactForEachElements =
                appliedCompactForEachRemarks.Elements().ToList();
            var compactForEachSourceProseIndex =
                compactForEachElements.FindIndex(paragraph =>
                    NormalizeText(paragraph.Value) == compactForEachContract);
            var compactForEachSourceReferenceIndex =
                compactForEachElements.FindIndex(paragraph =>
                    TryGetImporterSourceReferenceUrl(
                        paragraph,
                        out var sourceUrl) &&
                    UrlsEqual(sourceUrl, forEachMapping.Docs.SourceUrl));
            var compactForEachCompleted =
                NormalizeText(appliedCompactForEachDocs.Element("param")!.Value) ==
                    NormalizeText(forEachMapping.Docs.Parameters["action"]) &&
                compactForEachSourceProseIndex >= 0 &&
                compactForEachSourceProseIndex < compactForEachSourceReferenceIndex &&
                compactForEachContract.Contains(
                    "order of iteration",
                    StringComparison.Ordinal) &&
                compactForEachContract.Contains(
                    "Exceptions thrown by the action are relayed to the caller.",
                    StringComparison.Ordinal) &&
                compactForEachContract.Contains(
                    "side-effects that modify the underlying source of elements",
                    StringComparison.Ordinal);
            var compactForEachMetadataPreserved =
                CountImporterSourceReferences(
                    appliedCompactForEachRemarks,
                    forEachMapping.Docs.SourceUrl) == 2 &&
                appliedCompactForEachText.Contains(
                    compactForEachReference + compactForEachReference,
                    StringComparison.Ordinal) &&
                appliedCompactForEachRemarks.Elements("para").Any(
                    IsImporterAttributionParagraph);
            var compactForEachSecondReportPath = Path.Combine(
                tempDirectory,
                "compact-for-each-pipeline-second");
            var compactForEachSecondExitCode = RunAsync(
                [
                    "--path", compactForEachPipelinePath,
                    "--namespace", "Java.Util.Concurrent",
                    "--member", "ForEach",
                    "--offline",
                    "--cache", forEachCacheDirectory,
                    "--max-changes", "1",
                    "--apply",
                    "--report", compactForEachSecondReportPath,
                ]).GetAwaiter().GetResult();
            Assert(
                compactForEachExitCode == 0 &&
                    compactForEachCompleted &&
                    compactForEachMetadataPreserved &&
                    compactForEachCleanupSkip &&
                    compactForEachSecondExitCode == 0 &&
                    File.ReadAllText(compactForEachPipelinePath).Equals(
                        appliedCompactForEachText,
                        StringComparison.Ordinal),
                $"production compact metadata completion retains guarded duplicate references while adding the exact ForEach parameter and full Java contract (exit={compactForEachExitCode}, completed={compactForEachCompleted}, metadata={compactForEachMetadataPreserved}, cleanup={compactForEachCleanupSkip}, second={compactForEachSecondExitCode})");

            var enumPipelineFile = LoadedFile.Load(
                repositoryRoot,
                Path.Combine(fixtureRoot, "enum-source.xml"));
            enumPipelineFile.SelectOwners("Deprecated");
            var enumPipelineOwner = enumPipelineFile.Owners.Single();
            var enumPipelinePage = SourcePage.Parse(
                enumPipelineOwner.SourceRequest!,
                File.ReadAllText(Path.Combine(fixtureRoot, "android-reference.html")));
            var enumPipelineMapping = MapOwner(
                enumPipelineOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [enumPipelineOwner.SourceRequest!.Url] =
                        SourceLoadResult.Success(enumPipelinePage),
                });
            Assert(
                enumPipelineOwner.IsEnumField &&
                    enumPipelineOwner.Placeholders.Any(placeholder =>
                        placeholder.Name == "summary") &&
                    enumPipelineMapping.Docs?.Paragraphs.Count >= 2,
                "deprecated enum production owner maps its source paragraphs");
            var enumPipelineBlock = enumPipelineFile.Text[
                enumPipelineFile.DocsBlocks[enumPipelineOwner.Order].Start..
                enumPipelineFile.DocsBlocks[enumPipelineOwner.Order].End];
            var enumPipelineBlockDocument = XElement.Parse(
                enumPipelineBlock,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            Assert(
                TryGetElementSpan(
                    enumPipelineBlock,
                    enumPipelineBlockDocument.Element("remarks")!,
                    out var enumPipelineRemarksSpan),
                "deprecated enum compact metadata remarks are parser-located");
            var enumCompactReference = ImporterSourceReference(
                enumPipelineMapping.Docs!).ToString(SaveOptions.DisableFormatting);
            var enumCompactRemarks =
                $"<remarks>{enumCompactReference}" +
                $"{enumCompactReference}" +
                $"<para>{AndroidAttribution}</para></remarks>";
            enumPipelineBlock =
                enumPipelineBlock[..enumPipelineRemarksSpan.Start] +
                enumCompactRemarks +
                enumPipelineBlock[enumPipelineRemarksSpan.End..];
            var enumPipelineText =
                enumPipelineFile.Text[
                    ..enumPipelineFile.DocsBlocks[enumPipelineOwner.Order].Start] +
                enumPipelineBlock +
                enumPipelineFile.Text[
                    enumPipelineFile.DocsBlocks[enumPipelineOwner.Order].End..];
            File.WriteAllText(
                enumPipelinePath,
                enumPipelineText,
                new UTF8Encoding(false));
            var enumCacheDirectory = Path.Combine(tempDirectory, "enum-cache");
            Directory.CreateDirectory(enumCacheDirectory);
            var enumCacheKey = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(enumPipelineOwner.SourceRequest!.Url)))
                .ToLowerInvariant();
            File.WriteAllText(
                Path.Combine(enumCacheDirectory, enumCacheKey + ".html"),
                File.ReadAllText(Path.Combine(fixtureRoot, "android-reference.html")),
                new UTF8Encoding(false));
            var enumPipelineReportPath = Path.Combine(
                tempDirectory,
                "deprecated-enum-pipeline");
            var enumPipelineExitCode = RunAsync(
                [
                    "--path", enumPipelinePath,
                    "--namespace", "Android.Example",
                    "--member", "Deprecated",
                    "--offline",
                    "--cache", enumCacheDirectory,
                    "--max-changes", "1",
                    "--apply",
                    "--report", enumPipelineReportPath,
                ]).GetAwaiter().GetResult();
            var appliedEnumPipelineText = File.ReadAllText(enumPipelinePath);
            var appliedEnumPipelineDocs = XDocument.Parse(
                appliedEnumPipelineText,
                LoadOptions.PreserveWhitespace)
                .Root!.Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "Deprecated")
                .Element("Docs")!;
            var appliedEnumPipelineSummary = appliedEnumPipelineDocs.Element("summary")!;
            var appliedEnumPipelineRemarks = appliedEnumPipelineDocs.Element("remarks")!;
            using var enumPipelineReport = JsonDocument.Parse(
                File.ReadAllText(enumPipelineReportPath + ".json"));
            var enumPipelineCleanupSkip = enumPipelineReport.RootElement
                .GetProperty("entries")
                .EnumerateArray()
                .Any(entry =>
                    entry.GetProperty("status").GetString() == "skipped" &&
                    entry.GetProperty("target").GetString() == "remarks" &&
                    entry.GetProperty("reason").GetString() ==
                        "source_reference_mixed_content" &&
                    entry.GetProperty("sourceUrl").GetString() ==
                        enumPipelineMapping.Docs!.SourceUrl);
            var expectedEnumProse = enumPipelineMapping.Docs!.Paragraphs
                .Where(paragraph => !paragraph.IsCode)
                .Select(paragraph => NormalizeText(paragraph.Text))
                .ToList();
            var actualEnumProse = appliedEnumPipelineSummary.Elements("para")
                .Where(paragraph => !paragraph.Descendants("a").Any())
                .Select(paragraph => NormalizeText(paragraph.Value))
                .ToList();
            var enumPipelineCompleted =
                actualEnumProse.SequenceEqual(
                    expectedEnumProse,
                    StringComparer.Ordinal) &&
                actualEnumProse.Any(paragraph =>
                    paragraph.Contains(
                        "Identifies the deprecated fixture value.",
                        StringComparison.Ordinal)) &&
                ContainsSourceUrl(
                    appliedEnumPipelineSummary,
                    enumPipelineMapping.Docs.SourceUrl) &&
                appliedEnumPipelineSummary.Elements("para").Any(
                    IsImporterAttributionParagraph);
            var enumPipelineMetadataPreserved =
                CountImporterSourceReferences(
                    appliedEnumPipelineRemarks,
                    enumPipelineMapping.Docs.SourceUrl) == 2 &&
                appliedEnumPipelineRemarks.Elements("para").Any(
                    IsImporterAttributionParagraph);
            var enumPipelineSecondReportPath = Path.Combine(
                tempDirectory,
                "deprecated-enum-pipeline-second");
            var enumPipelineSecondExitCode = RunAsync(
                [
                    "--path", enumPipelinePath,
                    "--namespace", "Android.Example",
                    "--member", "Deprecated",
                    "--offline",
                    "--cache", enumCacheDirectory,
                    "--max-changes", "1",
                    "--apply",
                    "--report", enumPipelineSecondReportPath,
                ]).GetAwaiter().GetResult();
            Assert(
                enumPipelineExitCode == 0 &&
                    enumPipelineCompleted &&
                    enumPipelineMetadataPreserved &&
                    enumPipelineCleanupSkip &&
                    enumPipelineSecondExitCode == 0 &&
                    File.ReadAllText(enumPipelinePath).Equals(
                        appliedEnumPipelineText,
                        StringComparison.Ordinal),
                $"production deprecated enum completion retains guarded metadata while publishing complete source prose and summary provenance (exit={enumPipelineExitCode}, completed={enumPipelineCompleted}, metadata={enumPipelineMetadataPreserved}, cleanup={enumPipelineCleanupSkip}, second={enumPipelineSecondExitCode})");

            var copyOnWriteArrayListPath = Path.Combine(
                docsRoot,
                "Java.Util.Concurrent",
                "CopyOnWriteArrayList.xml");
            var copyOnWriteArrayListFile = LoadedFile.Load(
                repositoryRoot,
                copyOnWriteArrayListPath);
            copyOnWriteArrayListFile.SelectOwners(
                "Reversed",
                new InterfaceMemberResolver(docsRoot));
            var copyOnWriteArrayListOwner = copyOnWriteArrayListFile.Owners.Single();
            var copyOnWriteArrayListBlock = copyOnWriteArrayListFile.DocsBlocks[
                copyOnWriteArrayListOwner.Order];
            var copyOnWriteArrayListBlockText = copyOnWriteArrayListFile.Text[
                copyOnWriteArrayListBlock.Start..copyOnWriteArrayListBlock.End];
            var parsedCopyOnWriteArrayListBlock = XElement.Parse(
                copyOnWriteArrayListBlockText,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var parsedCopyOnWriteArrayListRemarks =
                parsedCopyOnWriteArrayListBlock.Element("remarks")!;
            var parsedCopyOnWriteArrayListElements =
                parsedCopyOnWriteArrayListRemarks.Elements().ToList();
            var parsedCopyOnWriteArrayListSourceReferenceIndex =
                parsedCopyOnWriteArrayListElements.FindIndex(element =>
                    TryGetImporterSourceReferenceUrl(
                        element.ToString(SaveOptions.DisableFormatting),
                        out _));
            var parsedCopyOnWriteArrayListSourceElements =
                parsedCopyOnWriteArrayListElements
                    .Take(parsedCopyOnWriteArrayListSourceReferenceIndex)
                    .ToList();
            Assert(
                copyOnWriteArrayListOwner.MemberRegistration ==
                    new MemberRegistration("reversed", "()Ljava/util/List;", false) &&
                copyOnWriteArrayListOwner.SourceRequest?.Url ==
                    JavaReference +
                        "java.base/java/util/concurrent/CopyOnWriteArrayList.html" &&
                parsedCopyOnWriteArrayListSourceElements.Count == 7 &&
                !IsRetainedRemarksParagraph(parsedCopyOnWriteArrayListSourceElements[^2]) &&
                NormalizeNormalWhitespace(parsedCopyOnWriteArrayListSourceElements[^2].Value)
                    .Equals(
                        string.Join(
                            " ",
                            hybridSourceFragments.Skip(5).Select(fragment =>
                                NormalizeNormalWhitespace(fragment.Text))),
                        StringComparison.Ordinal) &&
                IsRetainedRemarksParagraph(parsedCopyOnWriteArrayListSourceElements[^1]),
                "CopyOnWriteArrayList Reversed fixture uses its real registered Oracle member and retained annotation");

            string RemoveCopyOnWriteArrayListSourceElements(
                IEnumerable<XElement> sourceElements)
            {
                var spans = new List<XmlSpan>();
                foreach (var element in sourceElements)
                {
                    Assert(
                        TryGetElementSpan(
                            copyOnWriteArrayListBlockText,
                            element,
                            out var span),
                        "CopyOnWriteArrayList Reversed source paragraph can be located");
                    spans.Add(span);
                }

                var refreshedBlockText = copyOnWriteArrayListBlockText;
                foreach (var span in spans.OrderByDescending(span => span.Start))
                {
                    var lineStart = refreshedBlockText.LastIndexOf(
                        '\n',
                        Math.Max(0, span.Start - 1));
                    lineStart = lineStart < 0 ? 0 : lineStart + 1;
                    var lineEnd = refreshedBlockText.IndexOf(
                        '\n',
                        span.End);
                    var end = lineEnd < 0 ? span.End : lineEnd + 1;
                    refreshedBlockText =
                        refreshedBlockText[..lineStart] +
                        refreshedBlockText[end..];
                }

                return copyOnWriteArrayListFile.Text[..copyOnWriteArrayListBlock.Start] +
                    refreshedBlockText +
                    copyOnWriteArrayListFile.Text[copyOnWriteArrayListBlock.End..];
            }

            Assert(
                TryGetElementSpan(
                    copyOnWriteArrayListBlockText,
                    parsedCopyOnWriteArrayListSourceElements[0],
                    out var copyOnWriteArrayListFirstSourceSpan),
                "CopyOnWriteArrayList Reversed first source paragraph can be located");
            var copyOnWriteArrayListFirstSourceMarkup =
                copyOnWriteArrayListBlockText[
                    copyOnWriteArrayListFirstSourceSpan.Start..
                    copyOnWriteArrayListFirstSourceSpan.End];
            var hybridCopyOnWriteArrayListText =
                RemoveCopyOnWriteArrayListSourceElements(
                    parsedCopyOnWriteArrayListSourceElements.Skip(1).Take(4));
            var trailingCopyOnWriteArrayListText =
                RemoveCopyOnWriteArrayListSourceElements(
                    [parsedCopyOnWriteArrayListSourceElements[^2]]);

            (LoadedFile File, DocsOwner Owner, SourceDocs Docs, RemarksRefreshResult Refresh)
                RefreshCopyOnWriteArrayListRemarks(string fileName, string text)
            {
                var path = Path.Combine(tempDirectory, fileName);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                var refreshFile = LoadedFile.Load(repositoryRoot, path);
                refreshFile.SelectOwners(
                    "Reversed",
                    new InterfaceMemberResolver(docsRoot));
                var refreshOwner = refreshFile.Owners.Single();
                var refreshPage = SourcePage.Parse(
                    refreshOwner.SourceRequest!,
                    File.ReadAllText(Path.Combine(
                        fixtureRoot,
                        "copy-on-write-array-list-java-reference.html")));
                var refreshMapping = MapOwner(
                    refreshOwner,
                    new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                    {
                        [refreshOwner.SourceRequest!.Url] =
                            SourceLoadResult.Success(refreshPage),
                    });
                Assert(
                    refreshMapping.Docs is not null &&
                    HasPotentialImporterOwnedRemarksRefresh(refreshFile, refreshOwner),
                    "CopyOnWriteArrayList Reversed normal owner and source pipeline reaches remarks refresh");
                return (
                    refreshFile,
                    refreshOwner,
                    refreshMapping.Docs!,
                    RefreshImporterOwnedRemarks(
                        refreshFile.Text,
                        refreshFile,
                        refreshOwner,
                        refreshMapping.Docs!));
            }

            ImportReport ReportCopyOnWriteArrayListRefresh(
                (LoadedFile File, DocsOwner Owner, SourceDocs Docs, RemarksRefreshResult Refresh)
                    refresh,
                string originalText)
            {
                var report = new ImportReport
                {
                    Mode = "apply",
                    Offline = true,
                    MaxChanges = 1,
                };
                if (refresh.Refresh.Reason is not null)
                {
                    report.Entries.Add(ReportEntry.Skipped(
                        refresh.File.RelativePath,
                        refresh.Owner.Id,
                        "remarks",
                        refresh.Refresh.Reason,
                        refresh.Refresh.Detail!,
                        refresh.Docs.SourceUrl));
                }
                else if (!refresh.Refresh.Text.Equals(originalText, StringComparison.Ordinal))
                {
                    report.Entries.Add(ReportEntry.Changed(
                        "would_apply",
                        refresh.File.RelativePath,
                        refresh.Owner.Id,
                        "remarks",
                        refresh.Docs.SourceUrl));
                }
                return report;
            }

            var refreshedCopyOnWriteArrayList = RefreshCopyOnWriteArrayListRemarks(
                "copy-on-write-array-list-reversed-hybrid.xml",
                hybridCopyOnWriteArrayListText);
            var refreshedCopyOnWriteArrayListReport =
                ReportCopyOnWriteArrayListRefresh(
                    refreshedCopyOnWriteArrayList,
                    hybridCopyOnWriteArrayListText);
            var refreshedCopyOnWriteArrayListRemarks = XDocument.Parse(
                refreshedCopyOnWriteArrayList.Refresh.Text,
                LoadOptions.PreserveWhitespace)
                .Root!.Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "Reversed")
                .Element("Docs")!.Element("remarks")!;
            var refreshedCopyOnWriteArrayListElements =
                refreshedCopyOnWriteArrayListRemarks.Elements().ToList();
            var refreshedCopyOnWriteArrayListSourceReferenceIndex =
                refreshedCopyOnWriteArrayListElements.FindIndex(element =>
                    TryGetImporterSourceReferenceUrl(
                        element.ToString(SaveOptions.DisableFormatting),
                        out _));
            var refreshedCopyOnWriteArrayListSourceElements =
                refreshedCopyOnWriteArrayListElements
                    .Take(refreshedCopyOnWriteArrayListSourceReferenceIndex)
                    .ToList();
            var expectedCopyOnWriteArrayListFragments = ExpandRemarksFragments(
                refreshedCopyOnWriteArrayList.Docs.Paragraphs);
            var expectedCopyOnWriteArrayListText = string.Join(
                " ",
                expectedCopyOnWriteArrayListFragments
                    .Where(fragment => !fragment.IsCode)
                    .Select(fragment => NormalizeNormalWhitespace(fragment.Text)));
            var refreshedCopyOnWriteArrayListText = string.Join(
                " ",
                refreshedCopyOnWriteArrayListSourceElements
                    .Where(element => !IsRetainedRemarksParagraph(element))
                    .Select(element => NormalizeNormalWhitespace(element.Value)));
            Assert(
                refreshedCopyOnWriteArrayList.Refresh.Reason is null &&
                !refreshedCopyOnWriteArrayList.Refresh.Text.Equals(
                    hybridCopyOnWriteArrayListText,
                    StringComparison.Ordinal) &&
                refreshedCopyOnWriteArrayListSourceElements.Count == 7 &&
                refreshedCopyOnWriteArrayListSourceElements.Count(
                    IsRetainedRemarksParagraph) == 1 &&
                refreshedCopyOnWriteArrayListText.Equals(
                    expectedCopyOnWriteArrayListText,
                    StringComparison.Ordinal) &&
                refreshedCopyOnWriteArrayListSourceElements.Any(element =>
                    NormalizeNormalWhitespace(element.Value).Equals(
                        NormalizeNormalWhitespace(
                            parsedCopyOnWriteArrayListSourceElements[5].Value),
                        StringComparison.Ordinal)) &&
                refreshedCopyOnWriteArrayListReport.Entries is
                [{
                    Status: "would_apply",
                    Target: "remarks",
                    SourceUrl: var refreshedCopyOnWriteArrayListSourceUrl,
                }] &&
                refreshedCopyOnWriteArrayListSourceUrl ==
                    refreshedCopyOnWriteArrayList.Docs.SourceUrl &&
                !expectedCopyOnWriteArrayListFragments.Any(fragment =>
                    fragment.Text.Equals("Added in 21.", StringComparison.Ordinal)),
                "CopyOnWriteArrayList Reversed hybrid refresh restores missing middle Oracle source fragments in order while retaining its non-source annotation");

            var refreshedTrailingCopyOnWriteArrayList =
                RefreshCopyOnWriteArrayListRemarks(
                    "copy-on-write-array-list-reversed-trailing-refresh.xml",
                    trailingCopyOnWriteArrayListText);
            var reAdmittedTrailingCopyOnWriteArrayList =
                RefreshCopyOnWriteArrayListRemarks(
                    "copy-on-write-array-list-reversed-trailing-re-admission.xml",
                    refreshedTrailingCopyOnWriteArrayList.Refresh.Text);
            var refreshedTrailingCopyOnWriteArrayListRemarks = XDocument.Parse(
                refreshedTrailingCopyOnWriteArrayList.Refresh.Text,
                LoadOptions.PreserveWhitespace)
                .Root!.Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "Reversed")
                .Element("Docs")!.Element("remarks")!;
            var refreshedTrailingCopyOnWriteArrayListElements =
                refreshedTrailingCopyOnWriteArrayListRemarks.Elements().ToList();
            var refreshedTrailingCopyOnWriteArrayListAnnotationIndex =
                refreshedTrailingCopyOnWriteArrayListElements.FindIndex(
                    IsRetainedRemarksParagraph);
            var refreshedTrailingCopyOnWriteArrayListSourceReferenceIndex =
                refreshedTrailingCopyOnWriteArrayListElements.FindIndex(element =>
                    TryGetImporterSourceReferenceUrl(
                        element.ToString(SaveOptions.DisableFormatting),
                        out _));
            var refreshedTrailingCopyOnWriteArrayListSourceText = string.Join(
                " ",
                refreshedTrailingCopyOnWriteArrayListElements
                    .Take(refreshedTrailingCopyOnWriteArrayListAnnotationIndex)
                    .Select(element => NormalizeNormalWhitespace(element.Value)));
            Assert(
                refreshedTrailingCopyOnWriteArrayList.Refresh.Reason is null &&
                reAdmittedTrailingCopyOnWriteArrayList.Refresh.Reason is null &&
                reAdmittedTrailingCopyOnWriteArrayList.Refresh.Text.Equals(
                    refreshedTrailingCopyOnWriteArrayList.Refresh.Text,
                    StringComparison.Ordinal) &&
                refreshedTrailingCopyOnWriteArrayListAnnotationIndex ==
                    expectedCopyOnWriteArrayListFragments.Count &&
                refreshedTrailingCopyOnWriteArrayListSourceReferenceIndex ==
                    refreshedTrailingCopyOnWriteArrayListAnnotationIndex + 1 &&
                refreshedTrailingCopyOnWriteArrayListElements[
                    refreshedTrailingCopyOnWriteArrayListAnnotationIndex].Value.Equals(
                        "Added in 21.",
                        StringComparison.Ordinal) &&
                refreshedTrailingCopyOnWriteArrayListSourceText.Equals(
                    expectedCopyOnWriteArrayListText,
                    StringComparison.Ordinal) &&
                refreshedTrailingCopyOnWriteArrayListElements[
                    refreshedTrailingCopyOnWriteArrayListSourceReferenceIndex].ToString(
                        SaveOptions.DisableFormatting).Equals(
                            parsedCopyOnWriteArrayListElements[
                                parsedCopyOnWriteArrayListSourceReferenceIndex].ToString(
                                    SaveOptions.DisableFormatting),
                            StringComparison.Ordinal) &&
                refreshedTrailingCopyOnWriteArrayListSourceReferenceIndex + 2 ==
                    refreshedTrailingCopyOnWriteArrayListElements.Count &&
                refreshedTrailingCopyOnWriteArrayListElements[^1].ToString(
                    SaveOptions.DisableFormatting).Equals(
                        parsedCopyOnWriteArrayListElements[^1].ToString(
                            SaveOptions.DisableFormatting),
                        StringComparison.Ordinal),
                "CopyOnWriteArrayList Reversed trailing refresh inserts all source prose before its retained annotation, preserves exact metadata, and is re-admitted");

            void AssertCopyOnWriteArrayListNonTextNodeSkip(
                string node,
                string fileName,
                string description)
            {
                var unsafeFirstSourceMarkup =
                    copyOnWriteArrayListFirstSourceMarkup[..^"</para>".Length] +
                    node +
                    "</para>";
                var unsafeText = hybridCopyOnWriteArrayListText.Replace(
                    copyOnWriteArrayListFirstSourceMarkup,
                    unsafeFirstSourceMarkup,
                    StringComparison.Ordinal);
                var unsafeRefresh = RefreshCopyOnWriteArrayListRemarks(fileName, unsafeText);
                var unsafeReport = ReportCopyOnWriteArrayListRefresh(
                    unsafeRefresh,
                    unsafeText);
                Assert(
                    unsafeRefresh.Refresh.Reason ==
                        "existing_remarks_not_importer_owned" &&
                    unsafeRefresh.Refresh.Detail ==
                        "Existing remarks source content contained non-text nodes, markup, or an unsupported element." &&
                    unsafeRefresh.Refresh.Text.Equals(unsafeText, StringComparison.Ordinal) &&
                    unsafeReport.Entries is
                    [{
                        Status: "skipped",
                        Target: "remarks",
                        Reason: "existing_remarks_not_importer_owned",
                        SourceUrl: var unsafeSourceUrl,
                    }] &&
                    unsafeSourceUrl == unsafeRefresh.Docs.SourceUrl,
                    description);
            }

            AssertCopyOnWriteArrayListNonTextNodeSkip(
                "<!-- Authored note. -->",
                "copy-on-write-array-list-reversed-comment.xml",
                "CopyOnWriteArrayList Reversed comments inside source-matching paragraphs skip without modification");
            AssertCopyOnWriteArrayListNonTextNodeSkip(
                "<?authored note?>",
                "copy-on-write-array-list-reversed-processing-instruction.xml",
                "CopyOnWriteArrayList Reversed processing instructions inside source-matching paragraphs skip without modification");
            AssertCopyOnWriteArrayListNonTextNodeSkip(
                "<![CDATA[ ]]>",
                "copy-on-write-array-list-reversed-cdata.xml",
                "CopyOnWriteArrayList Reversed CDATA inside source-matching paragraphs skips without modification");

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

            var retryPolicyFile = LoadedFile.Load(
                repositoryRoot,
                Path.Combine(docsRoot, "Android.Security", "KeyStoreException.xml"));
            retryPolicyFile.SelectOwners("RetryPolicy");
            var retryPolicyOwner = retryPolicyFile.Owners.Single();
            var retryPolicyPage = SourcePage.Parse(
                retryPolicyOwner.SourceRequest!,
                """
                <html><body><main id="jd-content">
                <h2 class="api-section">Public methods</h2>
                <h3 class="api-name" id="getRetryPolicy()">getRetryPolicy</h3>
                <p>Returns the re-try policy for transient failures.</p>
                <table><tr><th colspan="2">Returns</th></tr>
                <tr><td>int</td><td>Value is either <code>0</code> or a combination of the following:
                <ul>
                <li><code>RETRY_NEVER</code></li>
                <li><code>RETRY_WITH_EXPONENTIAL_BACKOFF</code></li>
                <li><code>RETRY_WHEN_CONNECTIVITY_AVAILABLE</code></li>
                <li><code>RETRY_AFTER_NEXT_REBOOT</code></li>
                </ul></td></tr></table>
                </main></body></html>
                """);
            var retryPolicyMapping = MapOwner(
                retryPolicyOwner,
                new Dictionary<string, SourceLoadResult>(StringComparer.Ordinal)
                {
                    [retryPolicyOwner.SourceRequest!.Url] =
                        SourceLoadResult.Success(retryPolicyPage),
                });
            var unsafeRetryPolicyDocs = retryPolicyMapping.Docs!;
            const string unsafeRetryPolicyText =
                "Value is either 0 or a combination of the following: RETRY_NEVER; RETRY_WITH_EXPONENTIAL_BACKOFF; RETRY_WHEN_CONNECTIVITY_AVAILABLE; RETRY_AFTER_NEXT_REBOOT";
            var retryPolicyValue = new Placeholder(0, "value", "", "value");
            Assert(
                retryPolicyOwner.Id == "P:Android.Security.KeyStoreException.RetryPolicy" &&
                unsafeRetryPolicyDocs.Returns == unsafeRetryPolicyText &&
                ReplacementFor(retryPolicyValue, unsafeRetryPolicyDocs) is
                {
                    Text: null,
                    Reason: "source_channel_ambiguous",
                } &&
                ReplacementFor(
                    new Placeholder(1, "summary", "", "summary"),
                    unsafeRetryPolicyDocs).Text ==
                    "Returns the re-try policy for transient failures." &&
                ReplacementFor(
                    new Placeholder(2, "remarks", "", "remarks"),
                    unsafeRetryPolicyDocs).Remarks is { Count: 1 } &&
                ReplacementFor(
                    new Placeholder(3, "returns", "", "returns"),
                    unsafeRetryPolicyDocs).Text == unsafeRetryPolicyText,
                "exact RetryPolicy flag wording is rejected only for the value channel");
            foreach (var (ownerId, sourceDocs) in new[]
            {
                ("P:Android.Security.KeyStoreException.OtherPolicy",
                    unsafeRetryPolicyDocs with { UnsafeTargets = null }),
                (retryPolicyOwner.Id, unsafeRetryPolicyDocs with
                {
                    SourceUrl = AndroidReference + "android/security/KeyStoreException#getOtherPolicy()",
                    UnsafeTargets = null,
                }),
                (retryPolicyOwner.Id, unsafeRetryPolicyDocs with
                {
                    Returns = "One retry-policy code.",
                    UnsafeTargets = null,
                }),
            })
            {
                Assert(
                    ReplacementFor(
                        retryPolicyValue,
                        WithoutKnownUnsafeAndroidSourceChannels(ownerId, sourceDocs)).Text ==
                        sourceDocs.Returns,
                    "RetryPolicy exclusion requires the exact managed member, source URL and source text");
            }
            var authoredRetryPolicyDocument = XDocument.Parse(
                retryPolicyFile.Text,
                LoadOptions.PreserveWhitespace);
            var authoredRetryPolicyMember = authoredRetryPolicyDocument.Root!
                .Element("Members")!.Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "RetryPolicy");
            authoredRetryPolicyMember.Element("Docs")!.Element("value")!.Value =
                "An authored retry-policy description.";
            var authoredRetryPolicyPath = Path.Combine(tempDirectory, "authored-retry-policy.xml");
            var authoredRetryPolicyText = authoredRetryPolicyDocument.ToString(SaveOptions.DisableFormatting);
            File.WriteAllText(authoredRetryPolicyPath, authoredRetryPolicyText, new UTF8Encoding(false));
            var authoredRetryPolicyFile = LoadedFile.Load(repositoryRoot, authoredRetryPolicyPath);
            authoredRetryPolicyFile.SelectOwners("RetryPolicy");
            Assert(
                authoredRetryPolicyFile.Owners.Single().Placeholders.All(
                    placeholder => placeholder.Target != "value") &&
                authoredRetryPolicyFile.Text == authoredRetryPolicyText,
                "RetryPolicy source exclusion does not select or overwrite authored value documentation");

            var unsafePublishDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.Net.Wifi.Aware.PublishConfig.Builder.SetPublishType(Android.Net.Wifi.Aware.PublishType)",
                new SourceDocs(
                    "Specify the type: solicited (aka active - publish packets are transmitted over-the-air), or unsolicited (aka passive - no publish packets are transmitted).",
                    [new SourceParagraph(
                        "Solicited (aka active - publish packets are transmitted over-the-air).",
                        false)],
                    new Dictionary<string, string>(),
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/net/wifi/aware/PublishConfig.Builder#setPublishType(int)",
                    "android.net.wifi.aware.PublishConfig.Builder.setPublishType",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "summary", "", "summary"),
                    unsafePublishDocs).Reason == "source_channel_ambiguous" &&
                ReplacementFor(
                    new Placeholder(1, "remarks", "", "remarks"),
                    unsafePublishDocs).Reason == "source_channel_ambiguous" &&
                unsafePublishDocs.Paragraphs.Count == 0,
                "unsafe publish session semantics are not imported");

            var unsafeSubscribeDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.Net.Wifi.Aware.SubscribeConfig.Builder.SetSubscribeType(Android.Net.Wifi.Aware.SubscribeType)",
                new SourceDocs(
                    "Sets the type: passive (no subscribe packets are transmitted, a match is made against a solicited/active publish session).",
                    [new SourceParagraph(
                        "Passive subscribers match a solicited/active publish session.",
                        false)],
                    new Dictionary<string, string>(),
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/net/wifi/aware/SubscribeConfig.Builder#setSubscribeType(int)",
                    "android.net.wifi.aware.SubscribeConfig.Builder.setSubscribeType",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "summary", "", "summary"),
                    unsafeSubscribeDocs).Reason == "source_channel_ambiguous" &&
                ReplacementFor(
                    new Placeholder(1, "remarks", "", "remarks"),
                    unsafeSubscribeDocs).Reason == "source_channel_ambiguous" &&
                unsafeSubscribeDocs.Paragraphs.Count == 0,
                "unsafe subscribe session semantics are not imported");

            var unsafeGeofenceDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.Net.Wifi.Aware.SubscribeConfig.Builder.SetMaxDistanceMm(System.Int32)",
                new SourceDocs(
                    "Configure the maximum distance.",
                    [new SourceParagraph(
                        "Discovery with min <= distance <= max. The ingress rule is distance <= max or distance >= min.",
                        false)],
                    new Dictionary<string, string>(),
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/net/wifi/aware/SubscribeConfig.Builder#setMaxDistanceMm(int)",
                    "android.net.wifi.aware.SubscribeConfig.Builder.setMaxDistanceMm",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "remarks", "", "remarks"),
                    unsafeGeofenceDocs).Reason == "source_channel_ambiguous" &&
                unsafeGeofenceDocs.Paragraphs.Count == 0,
                "unsafe geofence remarks are not imported");

            var unsafePortDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.Net.Wifi.Aware.WifiAwareNetworkSpecifier.Builder.SetPort(System.Int32)",
                new SourceDocs(
                    "Configure the port.",
                    [],
                    new Dictionary<string, string>
                    {
                        ["port"] = "A positive integer. Value is between 0 and 65535 inclusive",
                    },
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/net/wifi/aware/WifiAwareNetworkSpecifier.Builder#setPort(int)",
                    "android.net.wifi.aware.WifiAwareNetworkSpecifier.Builder.setPort",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "param", "port", "param:port"),
                    unsafePortDocs).Reason == "source_channel_ambiguous",
                "unsafe port range is not imported");

            var unsafeContinueStrokeDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.AccessibilityServices.GestureDescription.StrokeDescription.ContinueStroke(Android.Graphics.Path,System.Int64,System.Int64,System.Boolean)",
                new SourceDocs(
                    "Create a new stroke that will continue this one.",
                    [],
                    new Dictionary<string, string>
                    {
                        ["duration"] = "The duration for the new stroke. Must not be negative.",
                    },
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/accessibilityservice/GestureDescription.StrokeDescription#continueStroke(android.graphics.Path,%20long,%20long,%20boolean)",
                    "android.accessibilityservice.GestureDescription.StrokeDescription.continueStroke",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "param", "duration", "param:duration"),
                    unsafeContinueStrokeDocs).Reason == "source_channel_ambiguous",
                "unsafe ContinueStroke duration is not imported");

            var unsafeAdSelectionResultDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.AdServices.AdSelection.PersistAdSelectionResultRequest.Builder.SetAdSelectionResult(System.Byte[])",
                new SourceDocs(
                    "Sets the ad selection result String.",
                    [new SourceParagraph("Sets the ad selection result String.", false)],
                    new Dictionary<string, string>(),
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/adservices/adselection/PersistAdSelectionResultRequest.Builder#setAdSelectionResult(byte[])",
                    "android.adservices.adselection.PersistAdSelectionResultRequest.Builder.setAdSelectionResult",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "summary", "", "summary"),
                    unsafeAdSelectionResultDocs).Reason == "source_channel_ambiguous" &&
                ReplacementFor(
                    new Placeholder(1, "remarks", "", "remarks"),
                    unsafeAdSelectionResultDocs).Reason == "source_channel_ambiguous" &&
                unsafeAdSelectionResultDocs.Paragraphs.Count == 0,
                "unsafe ad selection byte-array wording is not imported");

            var unsafeReportingDestinationDocs = WithoutKnownUnsafeAndroidSourceChannels(
                "M:Android.AdServices.AdSelection.ReportEventRequest.Builder.SetReportingDestinations(System.Int32)",
                new SourceDocs(
                    "Sets the bitfield of reporting destinations to report to (buyer, seller, or both).",
                    [
                        new SourceParagraph(
                            "Sets the bitfield of reporting destinations to report to (buyer, seller, or both).",
                            false),
                        new SourceParagraph(
                            "See ReportEventRequest.getReportingDestinations() for more information.",
                            false),
                    ],
                    new Dictionary<string, string>(),
                    "",
                    new Dictionary<string, string>(),
                    "https://developer.android.com/reference/android/adservices/adselection/ReportEventRequest.Builder#setReportingDestinations(int)",
                    "android.adservices.adselection.ReportEventRequest.Builder.setReportingDestinations",
                    "android"));
            Assert(
                ReplacementFor(
                    new Placeholder(0, "summary", "", "summary"),
                    unsafeReportingDestinationDocs).Reason == "source_channel_ambiguous" &&
                ReplacementFor(
                    new Placeholder(1, "remarks", "", "remarks"),
                    unsafeReportingDestinationDocs).Reason == "source_channel_ambiguous" &&
                !ShouldAddSourceDocumentation(
                    deferredRemarksPlaceholder: false,
                    replacedRemarksPlaceholder: false,
                    importedSourceChannel: true,
                    unsafeReportingDestinationDocs) &&
                unsafeReportingDestinationDocs.Paragraphs.Count == 0,
                "unsafe reporting destination wording and metadata are not imported");

            var unsafeMetadataDocument = XDocument.Parse(
                fixtureText,
                LoadOptions.PreserveWhitespace);
            var unsafeMetadataMember = unsafeMetadataDocument.Root!
                .Element("Members")!
                .Elements("Member")
                .Single(member => (string?)member.Attribute("MemberName") == "SetTitle");
            unsafeMetadataMember.Element("Docs")!
                .Element("remarks")!
                .ReplaceWith(XElement.Parse($"<remarks><para>{AndroidAttribution}</para></remarks>"));
            var unsafeMetadataPath = Path.Combine(tempDirectory, "unsafe-source-metadata.xml");
            File.WriteAllText(
                unsafeMetadataPath,
                unsafeMetadataDocument.ToString(SaveOptions.DisableFormatting),
                new UTF8Encoding(false));
            var unsafeMetadataFile = LoadedFile.Load(repositoryRoot, unsafeMetadataPath);
            unsafeMetadataFile.SelectOwners("SetTitle");
            var unsafeMetadataOwner = unsafeMetadataFile.Owners.Single();
            var unsafeMetadataResult = AddSourceDocumentationIfSafe(
                unsafeMetadataFile.Text,
                unsafeMetadataFile,
                unsafeMetadataOwner,
                unsafePublishDocs);
            Assert(
                unsafeMetadataResult.Contains(unsafePublishDocs.SourceUrl, StringComparison.Ordinal) &&
                unsafeMetadataResult.Contains(AndroidAttribution, StringComparison.Ordinal) &&
                !unsafeMetadataResult.Contains(
                    "solicited (aka active - publish packets are transmitted over-the-air)",
                    StringComparison.Ordinal),
                "unsafe source metadata insertion preserves attribution without source prose");

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
            if (File.Exists(dreamFocusPipelinePath))
                File.Delete(dreamFocusPipelinePath);
            if (File.Exists(forEachPipelinePath))
                File.Delete(forEachPipelinePath);
            if (File.Exists(compactForEachPipelinePath))
                File.Delete(compactForEachPipelinePath);
            if (File.Exists(enumPipelinePath))
                File.Delete(enumPipelinePath);
            if (File.Exists(zoneTransitionPipelinePath))
                File.Delete(zoneTransitionPipelinePath);
            Directory.Delete(tempDirectory, true);
        }

        Console.WriteLine("SELF-TEST PASS: Android/Java exact matching, ICU text and Health Connect importer regressions, ordered paragraph/code preservation, strict importer-owned remarks refreshes, metadata-only and placeholder repairs, source-channel validation, XML parsing, and atomic writes.");
        return 0;
    }

    static void TestKnownEapChannelCorrections(string repositoryRoot)
    {
        var docsRoot = Path.Combine(repositoryRoot, "docs", "xml");
        var directory = Path.Combine(Path.GetTempPath(), $"eap-importer-self-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var pipelinePath = Path.Combine(docsRoot, "Android.Net.Eap",
            $"Eap.importer-self-test-{Environment.ProcessId}.xml");
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            foreach (var correction in KnownEapChannelCorrections)
            {
                var isParameter = correction.Target.StartsWith("param:", StringComparison.Ordinal);
                var fileName = isParameter ? "EapAkaInfo+Builder.xml" : "EapSessionConfig+Builder.xml";
                var fixtureName = isParameter
                    ? "eap-aka-info-builder-android-reference.html"
                    : "eap-session-config-builder-android-reference.html";
                var html = File.ReadAllText(Path.Combine(repositoryRoot, "tools", "importer-fixtures", fixtureName));
                var original = XElement.Load(Path.Combine(docsRoot, "Android.Net.Eap", fileName));
                var member = original.Element("Members")!.Elements("Member").Single(element =>
                    element.Elements("MemberSignature").Any(signature =>
                        (string?)signature.Attribute("Language") == "DocId" &&
                        (string?)signature.Attribute("Value") == correction.MemberId));
                original.Element("Members")!.ReplaceNodes(new XElement(member));
                original.Element("Docs")!.ReplaceNodes(
                    new XElement("summary", "Authored type summary."),
                    new XElement("remarks", "Authored type remarks."));
                member = original.Element("Members")!.Element("Member")!;
                member.Element("Docs")!.ReplaceWith(KnownEapPriorDocs(correction));
                var cache = Path.Combine(directory, "cache");
                Directory.CreateDirectory(cache);
                var sourceUrl = correction.Source.SourceUrl.Split('#')[0];
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceUrl))).ToLowerInvariant();
                var cachePath = Path.Combine(cache, key + ".html");
                var reportPath = Path.Combine(directory, "report");
                var args = new[]
                {
                    "--path", pipelinePath, "--namespace", "Android.Net.Eap",
                    "--member", (string)member.Attribute("MemberName")!,
                    "--offline", "--cache", cache, "--max-changes", "10",
                    "--apply", "--report", reportPath,
                };

                JsonDocument Apply(XElement root, string sourceHtml)
                {
                    File.WriteAllText(pipelinePath, root.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
                    File.WriteAllText(cachePath, sourceHtml, new UTF8Encoding(false));
                    Assert(RunAsync(args).GetAwaiter().GetResult() == 0,
                        "registered EAP production pipeline succeeds");
                    var report = JsonDocument.Parse(File.ReadAllText(reportPath + ".json"));
                    Assert(report.RootElement.GetProperty("errorCount").GetInt32() == 0,
                        "registered EAP production pipeline has no errors");
                    return report;
                }

                XElement AppliedDocs() => XElement.Load(pipelinePath).Element("Members")!.Element("Member")!.Element("Docs")!;
                XElement Target(XElement docs) => docs.Elements(correction.Target.Split(':')[0]).Single(element =>
                    !isParameter || (string?)element.Attribute("name") == "reauthId");
                void AssertNoEdit(XElement root, string sourceHtml, string description)
                {
                    var before = Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
                    using var report = Apply(root, sourceHtml);
                    Assert(report.RootElement.GetProperty("appliedCount").GetInt32() == 0 &&
                        before.SequenceEqual(File.ReadAllBytes(pipelinePath)), description);
                }

                var firstFill = new XElement(original);
                var placeholders = firstFill.Element("Members")!.Element("Member")!.Element("Docs")!;
                foreach (var channel in placeholders.Elements())
                    channel.ReplaceNodes("To be added.");
                using (var report = Apply(firstFill, html))
                {
                    Assert(report.RootElement.GetProperty("appliedCount").GetInt32() ==
                        (isParameter ? 3 : 5), "EAP registered FIRST-FILL imports all and only safe channels");
                    Assert(Target(AppliedDocs()).Value == (correction.CorrectText ?? "To be added."),
                        "EAP registered FIRST-FILL corrects or suppresses the exact source channel before early returns");
                    if (isParameter)
                        Assert(report.RootElement.GetProperty("entries").EnumerateArray().Any(entry =>
                            entry.GetProperty("target").GetString() == correction.Target &&
                            entry.GetProperty("reason").GetString() == "source_channel_ambiguous"),
                            "EAP unsafe FIRST-FILL is explicitly reported");
                }
                var firstFillResult = XElement.Load(pipelinePath);
                var oldMetadata = new XElement(firstFill);
                var newMetadata = new XElement(firstFillResult);
                foreach (var docs in oldMetadata.Descendants("Docs"))
                    docs.RemoveNodes();
                foreach (var docs in newMetadata.Descendants("Docs"))
                    docs.RemoveNodes();
                Assert(XNode.DeepEquals(oldMetadata, newMetadata),
                    "EAP FIRST-FILL preserves all managed metadata");
                AssertNoEdit(firstFillResult, html, "EAP FIRST-FILL repeat is zero-edit byte-identical");

                var differentMemberFirstFill = new XElement(firstFill);
                differentMemberFirstFill.Element("Members")!.Element("Member")!.Elements("MemberSignature")
                    .Single(signature => (string?)signature.Attribute("Language") == "DocId")
                    .SetAttributeValue("Value", correction.MemberId + ".Other");
                using (var report = Apply(differentMemberFirstFill, html))
                {
                    Assert(Target(AppliedDocs()).Value == correction.IncorrectText,
                        "EAP registered FIRST-FILL never applies a correction to a different managed owner");
                }
                var changedContractHtml = html.Replace(correction.Source.Summary,
                    correction.Source.Summary + " Changed.", StringComparison.Ordinal);
                using (var report = Apply(firstFill, changedContractHtml))
                {
                    Assert(Target(AppliedDocs()).Value == correction.IncorrectText,
                        "EAP registered FIRST-FILL requires the complete exact source, not just the defective channel");
                }

                using (var report = Apply(original, html))
                {
                    Assert(report.RootElement.GetProperty("appliedCount").GetInt32() == 1 &&
                        Target(AppliedDocs()).Value == (correction.CorrectText ?? "To be added."),
                        "EAP complete prior-owned output receives exactly one repair or withdrawal");
                }
                var repaired = XElement.Load(pipelinePath);
                var expectedDocs = KnownEapPriorDocs(correction);
                Target(expectedDocs).Value = correction.CorrectText ?? "To be added.";
                Assert(ImporterMarkupEquals(AppliedDocs(), expectedDocs),
                    "EAP prior-owned repair preserves every other source channel and provenance");
                AssertNoEdit(repaired, html, "EAP prior-owned repair repeat is zero-edit byte-identical");

                foreach (var mutation in new Action<XElement>[]
                {
                    element => element.Value += " Authored.",
                    element => element.ReplaceNodes(new XElement("c", element.Value)),
                    element => element.ReplaceNodes(new XCData(element.Value)),
                    element => element.Add(new XComment("Authored")),
                    element => element.Add(new XProcessingInstruction("authored", "keep")),
                    element => element.SetAttributeValue("authored", "keep"),
                })
                {
                    var authored = new XElement(original);
                    mutation(Target(authored.Element("Members")!.Element("Member")!.Element("Docs")!));
                    AssertNoEdit(authored, html, "EAP prior-owned target rejects authored/mixed/CDATA/comment/PI/attribute content");
                }
                foreach (var mutation in new Action<XElement>[]
                {
                    docs => docs.Add(new XElement("exception", new XAttribute("cref", "T:System.Exception"), "Authored.")),
                    docs => docs.SetAttributeValue("authored", "keep"),
                    docs => docs.Add(new XComment("Authored")),
                    docs => docs.Add(new XProcessingInstruction("authored", "keep")),
                    docs => docs.Element("summary")!.ReplaceNodes(new XCData(docs.Element("summary")!.Value)),
                    docs => docs.Element("summary")!.SetAttributeValue("authored", "keep"),
                    docs => docs.Element("summary")!.Add(new XComment("Authored")),
                    docs => docs.Element("remarks")!.AddFirst(new XElement("para", "Authored notes.")),
                    docs => docs.Element("remarks")!.SetAttributeValue("authored", "keep"),
                    docs => docs.Element("remarks")!.Elements("para").First().ReplaceNodes(new XCData(correction.Source.Summary)),
                    docs => docs.Element("remarks")!.Descendants("a").First().SetAttributeValue("href", sourceUrl + "#other"),
                    docs => docs.Element("remarks")!.Descendants("code").First().Value += ".Other",
                    docs => docs.Element("remarks")!.Elements("para").Last().Add(new XComment("Authored attribution")),
                    docs => docs.Element("remarks")!.Elements("para").Last().Descendants("a").Last().SetAttributeValue("href", "https://example.invalid"),
                    docs => docs.Add(new XElement(Target(docs))),
                })
                {
                    var authored = new XElement(original);
                    mutation(authored.Element("Members")!.Element("Member")!.Element("Docs")!);
                    AssertNoEdit(authored, html, "EAP repair rejects altered complete Docs/source-reference/attribution provenance");
                }
                foreach (var mutation in new Action<XElement>[]
                {
                    element => element.Element("ReturnValue")!.Element("ReturnType")!.Value = "System.Object",
                    element => element.Element("Parameters")!.Element("Parameter")!.SetAttributeValue("Type", "System.Object"),
                    element => element.Element("Parameters")!.Element("Parameter")!.SetAttributeValue("Name", "other"),
                    element => element.Elements("MemberSignature").Single(signature =>
                        (string?)signature.Attribute("Language") == "C#").SetAttributeValue("Value", "public object Other ();"),
                    element => element.Elements("MemberSignature").Single(signature =>
                        (string?)signature.Attribute("Language") == "DocId").SetAttributeValue("Value", correction.MemberId + ".Other"),
                    element => element.Element("Attributes")!.Descendants("AttributeName")
                        .First(attribute => attribute.Value.Contains("Android.Runtime.Register", StringComparison.Ordinal)).Value =
                            "[Android.Runtime.Register(\"other\", \"()V\", \"\")]",
                })
                {
                    var altered = new XElement(original);
                    mutation(altered.Element("Members")!.Element("Member")!);
                    AssertNoEdit(altered, html, "EAP repair rejects changed registered member/JNI/managed metadata");
                }
                foreach (var changedHtml in new[]
                {
                    html.Replace(correction.Source.Summary, correction.Source.Summary + " Changed.", StringComparison.Ordinal),
                    html.Replace("This value cannot be <code>null</code>.", "This value may be <code>null</code>.", StringComparison.Ordinal),
                    html.Replace("<table>", "<p>Additional source contract.</p><table>", StringComparison.Ordinal),
                    html.Replace(isParameter ? "client's EAP Identity" : "faciliate", isParameter ? "reauthentication ID" : "facilitate", StringComparison.Ordinal),
                    html.Replace(isParameter ? ">setReauthId</h3>" : ">setEapMsChapV2Config</h3>", ">other</h3>", StringComparison.Ordinal),
                    html.Replace(isParameter ? "<code>byte</code>:" : "<code>String</code>:", "<code>Object</code>:", StringComparison.Ordinal),
                    html.Replace("</div>", "<table><tr><th>Throws</th></tr><tr><td>IllegalArgumentException</td><td>Invalid input.</td></tr></table></div>", StringComparison.Ordinal),
                })
                    AssertNoEdit(original, changedHtml, "EAP repair requires the complete unchanged official source contract");

                var alteredUrl = new XElement(original);
                foreach (var attribute in alteredUrl.Element("Attributes")!.Descendants("AttributeName")
                    .Where(attribute => attribute.Value.Contains("Android.Runtime.Register", StringComparison.Ordinal)))
                    attribute.Value = attribute.Value.Replace("Builder", "OtherBuilder", StringComparison.Ordinal);
                var otherUrl = sourceUrl.Replace("Builder", "OtherBuilder", StringComparison.Ordinal);
                var otherKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otherUrl))).ToLowerInvariant();
                File.WriteAllText(Path.Combine(cache, otherKey + ".html"), html);
                AssertNoEdit(alteredUrl, html, "EAP correction requires the canonical registered source owner and URL");
            }
        }
        finally
        {
            Console.SetOut(originalOutput);
            if (File.Exists(pipelinePath))
                File.Delete(pipelinePath);
            Directory.Delete(directory, recursive: true);
        }
        Console.WriteLine("SELF-TEST PASS: registered EAP first-fill, strict prior-owned repair/withdrawal, byte-identical repeats, complete-source and authored/metadata/provenance negatives.");
    }

    static void TestKnownAndroidTextRepairs()
    {
        foreach (var group in KnownAndroidTextRepairs.GroupBy(repair => repair.MemberId))
        {
            var repair = group.First();
            var parameterName = repair.Target.StartsWith("param:", StringComparison.Ordinal)
                ? repair.Target["param:".Length..] : null;
            var isEnum = repair.MemberId.StartsWith("F:", StringComparison.Ordinal);
            var summary = parameterName is null
                ? repair.IncorrectText
                : "Constructs a new Builder for creating a channel sounding ranging session.";
            var rawSource = new SourceDocs(
                summary,
                [new SourceParagraph(summary, false)],
                parameterName is null ? new Dictionary<string, string>() :
                    new Dictionary<string, string> { [parameterName] = "String: " + repair.IncorrectText },
                "",
                new Dictionary<string, string>(),
                repair.SourceUrl,
                "android.ranging.ble.cs.exactTestMember",
                "android");
            var source = WithKnownAndroidTextCorrections(repair.MemberId, rawSource);
            Assert(WithKnownAndroidTextCorrections(repair.MemberId + ".Other", rawSource) == rawSource,
                "Android text corrections require the exact member");
            var wrongUrlSource = rawSource with { SourceUrl = rawSource.SourceUrl + ".Other" };
            Assert(WithKnownAndroidTextCorrections(repair.MemberId, wrongUrlSource) == wrongUrlSource,
                "Android text corrections require the exact source URL");
            var modifiedSource = rawSource with
            {
                Summary = summary + " Changed.",
                Paragraphs = [new SourceParagraph(summary + " Changed.", false)],
                Parameters = parameterName is null ? rawSource.Parameters :
                    new Dictionary<string, string> { [parameterName] = repair.IncorrectText + " Changed." },
            };
            var untouchedSource = WithKnownAndroidTextCorrections(repair.MemberId, modifiedSource);
            Assert(untouchedSource.Summary == modifiedSource.Summary &&
                untouchedSource.Paragraphs.SequenceEqual(modifiedSource.Paragraphs) &&
                untouchedSource.Parameters.SequenceEqual(modifiedSource.Parameters),
                "Android text corrections preserve changed official source prose");

            var container = new XElement(isEnum ? "summary" : "remarks",
                new XElement("para", summary),
                ImporterSourceReference(rawSource),
                XElement.Parse($"<para>{AndroidAttribution}</para>"));
            var docs = new XElement("Docs");
            if (!isEnum)
                docs.Add(new XElement("summary", summary));
            if (parameterName is not null)
                docs.Add(new XElement("param", new XAttribute("name", parameterName), repair.IncorrectText));
            docs.Add(container);
            var eligible = FindKnownAndroidTextRepairTargets(repair.MemberId, docs, source);
            Assert(eligible.Count == group.Count(), "Android text repairs select every exact owned channel");
            Assert(FindKnownAndroidTextRepairTargets(repair.MemberId + ".Other", docs, source).Count == 0,
                "Android text repairs preserve different members");
            Assert(FindKnownAndroidTextRepairTargets(repair.MemberId, docs,
                source with { SourceUrl = source.SourceUrl + ".Other" }).Count == 0,
                "Android text repairs preserve mismatched source URLs");
            Assert(FindKnownAndroidTextRepairTargets(repair.MemberId, docs, modifiedSource).Count == 0,
                "Android text repairs preserve mismatched mapped source prose");
            var uncorrected = FindKnownAndroidTextRepairTargets(repair.MemberId, docs, rawSource);
            Assert(uncorrected.Count == 0, "Android text repairs require verified corrected source channels");
            if (!isEnum)
            {
                var duplicateProvenance = new XElement(docs);
                duplicateProvenance.Add(new XElement("remarks", "Authored notes."));
                Assert(FindKnownAndroidTextRepairTargets(repair.MemberId, duplicateProvenance, source).Count == 0,
                    "Android text repairs preserve duplicate provenance containers");
            }

            foreach (var target in eligible)
            {
                foreach (var mutation in new Action<XElement>[]
                {
                    element => element.ReplaceNodes(new XElement("c", element.Value)),
                    element => element.ReplaceNodes(new XCData(element.Value)),
                    element => element.Add(new XComment("Authored")),
                    element => element.Add(new XProcessingInstruction("authored", "keep")),
                    element => element.SetAttributeValue("authored", "keep"),
                    element => element.Value += " Authored.",
                    element => element.Value = " " + element.Value,
                })
                {
                    var authored = new XElement(docs);
                    var authoredTarget = FindKnownAndroidTextRepairTargets(repair.MemberId, authored, source)
                        .Single(candidate => candidate.Repair.Target == target.Repair.Target);
                    mutation(authoredTarget.Element);
                    Assert(!FindKnownAndroidTextRepairTargets(repair.MemberId, authored, source)
                        .Any(candidate => candidate.Repair.Target == target.Repair.Target),
                        "Android text repairs preserve authored nodes, markup, attributes and text");
                }
                var duplicate = new XElement(docs);
                var duplicateChannel = duplicate.Elements(target.Repair.Target.Split(':')[0]).First();
                duplicateChannel.AddAfterSelf(new XElement(duplicateChannel));
                Assert(!FindKnownAndroidTextRepairTargets(repair.MemberId, duplicate, source)
                    .Any(candidate => candidate.Repair.Target == target.Repair.Target),
                    "Android text repairs preserve duplicated channels");
            }
            foreach (var mutation in new Action<XElement>[]
            {
                element => element.AddFirst(new XText("Authored content.")),
                element => element.AddFirst(new XComment("Authored")),
                element => element.AddFirst(new XElement("para", "Authored paragraph.")),
                element => element.Elements().Last().Add(new XComment("Authored attribution")),
                element => element.Descendants("a").First().SetAttributeValue("href", repair.SourceUrl + ".Other"),
            })
            {
                var authored = new XElement(docs);
                mutation(authored.Element(isEnum ? "summary" : "remarks")!);
                Assert(FindKnownAndroidTextRepairTargets(repair.MemberId, authored, source).Count == 0,
                    "Android text repairs require complete exact source structure and attribution");
            }

            var decoy = $"<!-- <summary>{repair.IncorrectText}</summary> -->";
            var text = $"<Type>{decoy}{docs.ToString(SaveOptions.DisableFormatting)}</Type>";
            var file = new LoadedFile
            {
                Path = "", RelativePath = "known-android-text-self-test.xml", Text = text,
                Newline = "\n", HasUtf8Bom = false, Root = XElement.Parse(text, LoadOptions.PreserveWhitespace),
            };
            file.UpdateBlockOffsets(0, text);
            var owner = new DocsOwner(0, repair.MemberId, file.Root.Element("Docs")!, null, null, null, [], isEnum);
            Assert(RequiresSourceLoad(file, owner), "Android text repair-only owners load official sources");
            var repaired = RepairKnownAndroidText(text, file, owner, source);
            Assert(repaired.Targets.Count == group.Count() && repaired.Text.Contains(decoy, StringComparison.Ordinal),
                "Android text repairs locate exact elements without editing comment decoys");
            var correctedDocs = XElement.Parse(repaired.Text, LoadOptions.PreserveWhitespace).Element("Docs")!;
            foreach (var target in eligible)
            {
                var parts = target.Repair.Target.Split(':');
                var correctedChannel = correctedDocs.Elements(parts[0]).Single(channel =>
                    parts.Length == 1 || (string?)channel.Attribute("name") == parts[1]);
                var correctedElement = target.Repair.Target == "remarks" || isEnum
                    ? correctedChannel.Elements("para").First() : correctedChannel;
                Assert(correctedElement.Value == target.Repair.CorrectText,
                    "Android text repairs emit the exact allow-listed correction");
            }
            Assert(FindKnownAndroidTextRepairTargets(repair.MemberId, correctedDocs, source).Count == 0,
                "Android text corrections are idempotent");
            file.UpdateBlockOffsets(0, repaired.Text);
            var correctedOwner = owner with { Docs = correctedDocs };
            Assert(RepairKnownAndroidText(repaired.Text, file, correctedOwner, source).Text == repaired.Text,
                "Android text repairs do not rewrite corrected XML");
            if (!isEnum)
            {
                Assert(RefreshImporterOwnedRemarks(repaired.Text, file, correctedOwner, source).Text == repaired.Text,
                    "source refresh does not reintroduce corrected Android typos");
            }
        }
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
        public int? ApiSince { get; private set; }
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
                    case "--api-since":
                        options.ApiSince = PositiveInt(Value(), argument, 10_000);
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
              --api-since <level>    Owners declared with the exact Android API level

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
            InterfaceMemberResolver? interfaceMemberResolver = null,
            int? apiSince = null)
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
                var owner = member ?? Root;
                if (memberFilter is not null &&
                    !string.Equals(name, memberFilter, StringComparison.Ordinal) &&
                    !id.Contains(memberFilter, StringComparison.Ordinal))
                {
                    continue;
                }
                if (apiSince is not null && !IsIntroducedInApi(owner, Root, apiSince.Value))
                    continue;

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

        static bool IsIntroducedInApi(XElement owner, XElement type, int apiSince)
        {
            if (HasAvailability(owner, apiSince))
                return true;
            return owner != type &&
                !HasAnyAvailability(owner) &&
                HasAvailability(type, apiSince);
        }

        static bool HasAvailability(XElement owner, int apiSince) =>
            owner.Element("Attributes")?
                .Elements("Attribute")
                .Elements("AttributeName")
                .Any(attribute =>
                    Regex.IsMatch(
                        attribute.Value,
                        $@"\bApiSince\s*=\s*{apiSince}\b|" +
                            $@"\bSupportedOSPlatform\s*\(\s*""android{apiSince}\.0""",
                        RegexOptions.CultureInvariant)) == true;

        static bool HasAnyAvailability(XElement owner) =>
            owner.Element("Attributes")?
                .Elements("Attribute")
                .Elements("AttributeName")
                .Any(attribute =>
                    Regex.IsMatch(
                        attribute.Value,
                        @"\bApiSince\s*=\s*\d+\b|" +
                            @"\bSupportedOSPlatform\s*\(\s*""android\d+\.0""",
                        RegexOptions.CultureInvariant)) == true;

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
                string.Concat(element.Nodes()
                    .OfType<XText>()
                    .Where(node => node is not XCData)
                    .Select(node => node.Value)));
            var emptyParagraphs = paragraphs
                .Where(paragraph => !paragraph.HasElements && NormalizeText(paragraph.Value).Length == 0)
                .ToList();
            if (!hasDirectPlaceholder && emptyParagraphs.Count != 1)
                return false;

            var metadataParagraphs = paragraphs.Except(emptyParagraphs).ToList();
            return metadataParagraphs.Count > 0 &&
                metadataParagraphs.All(paragraph =>
                    paragraph.Name.LocalName == "para" &&
                    IsImporterMetadataParagraph(paragraph));
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
    sealed record XmlSpan(int Start, int End);
    sealed record ImporterSourceReferenceElement(XElement Element, string Url);
    sealed record KnownBooleanReturnRepair(
        string SourceUrl,
        string IncorrectReturn,
        string CorrectReturn,
        string IncorrectMarkup);
    sealed record KnownJavaExampleRepair(
        string SourceUrl,
        string IncompleteCode,
        string CorrectCode,
        string? MemberId = null);
    sealed record KnownJavaProseRepair(
        string SourceUrl,
        string IncorrectText,
        string CorrectText);
    sealed record KnownAndroidParameterRepair(
        string SourceUrl,
        string MemberId,
        string ParameterName,
        string IncorrectText,
        string CorrectText);
    sealed record KnownAndroidTextRepair(
        string SourceUrl,
        string MemberId,
        string Target,
        string IncorrectText,
        string CorrectText);
    sealed record AndroidTextRepairTarget(KnownAndroidTextRepair Repair, XElement Element);
    sealed record AndroidTextRepairResult(string Text, IReadOnlyList<string> Targets);
    sealed record KnownEapChannelCorrection(
        string MemberId,
        MemberRegistration Registration,
        string ReturnType,
        string ManagedSignature,
        (string Name, string Type)[] ParameterTypes,
        SourceDocs Source,
        string Target,
        string IncorrectText,
        string? CorrectText);
    sealed record KnownAndroidProseRepair(
        string SourceUrl,
        string MemberId,
        string IncorrectSummary,
        string CorrectSummary,
        string IncorrectRemarks,
        string CorrectRemarks);
    sealed record BooleanReturnRepairSkip(string Reason, string Detail);
    sealed record BooleanReturnRepairResult(
        string Text,
        bool Repaired,
        BooleanReturnRepairSkip? Skip)
    {
        public static BooleanReturnRepairResult NoChange(string text) =>
            new(text, false, null);

        public static BooleanReturnRepairResult RepairedText(string text) =>
            new(text, true, null);

        public static BooleanReturnRepairResult Failure(
            string text,
            string reason,
            string detail) =>
            new(text, false, new BooleanReturnRepairSkip(reason, detail));
    }
    sealed record JavaExampleRepairResult(string Text, bool Repaired)
    {
        public static JavaExampleRepairResult NoChange(string text) =>
            new(text, false);

        public static JavaExampleRepairResult RepairedText(string text) =>
            new(text, true);
    }
    sealed record AndroidParameterRepairResult(
        string Text,
        bool Repaired,
        string ParameterName)
    {
        public static AndroidParameterRepairResult NoChange(string text) =>
            new(text, false, string.Empty);

        public static AndroidParameterRepairResult RepairedText(
            string text,
            string parameterName) =>
            new(text, true, parameterName);
    }
    sealed record AndroidProseRepairResult(string Text, bool Repaired)
    {
        public static AndroidProseRepairResult NoChange(string text) =>
            new(text, false);

        public static AndroidProseRepairResult RepairedText(string text) =>
            new(text, true);
    }
    sealed record JavaProseRepairResult(string Text, bool Repaired)
    {
        public static JavaProseRepairResult NoChange(string text) =>
            new(text, false);

        public static JavaProseRepairResult RepairedText(string text) =>
            new(text, true);
    }
    sealed record UnsafeParameterRepairResult(string Text, bool Repaired, string ParameterName)
    {
        public static UnsafeParameterRepairResult NoChange(string text) =>
            new(text, false, string.Empty);

        public static UnsafeParameterRepairResult RepairedText(string text, string parameterName) =>
            new(text, true, parameterName);
    }
    sealed record SourceReferenceCleanupSkip(string Reason, string Detail)
    {
        public static SourceReferenceCleanupSkip NotLocated(string detail) =>
            new("source_reference_target_not_located", detail);

        public static SourceReferenceCleanupSkip MixedContent(string detail) =>
            new("source_reference_mixed_content", detail);

        public static SourceReferenceCleanupSkip UnownedLegacyAttribution(string detail) =>
            new("legacy_attribution_not_importer_owned", detail);
    }
    sealed record SourceReferenceCleanupResult(
        string Text,
        SourceReferenceCleanupSkip? Skip,
        int RemovedCount)
    {
        public static SourceReferenceCleanupResult Success(
            string text,
            int removedCount = 0) =>
            new(text, null, removedCount);

        public static SourceReferenceCleanupResult Failure(
            string text,
            SourceReferenceCleanupSkip skip) =>
            new(text, skip, 0);
    }
    sealed record CopiedDescriptionRepairTarget(string Target, XElement Element);
    sealed record XmlSpanEdit(XmlSpan Span, string Replacement);
    sealed record CopiedDescriptionRepairSkip(string Target, string Reason, string Detail);
    sealed record CopiedDescriptionRepairResult(
        string Text,
        List<string> Targets,
        List<CopiedDescriptionRepairSkip> Skips)
    {
        public static CopiedDescriptionRepairResult Skip(
            string text,
            IEnumerable<CopiedDescriptionRepairTarget> targets,
            string reason,
            string detail) =>
            new(
                text,
                [],
                targets.Select(target => new CopiedDescriptionRepairSkip(
                    target.Target,
                    reason,
                    detail)).ToList());
    }
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
        bool IsImporterMetadataRepair = false,
        int RemarksChildIndex = -1,
        int MetadataRepairIndex = -1)
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
            var parent = element.Parent;
            var remarksChildIndex = parent?.Name.LocalName == "remarks"
                ? parent.Elements()
                    .TakeWhile(sibling => !ReferenceEquals(sibling, element))
                    .Count()
                : -1;
            var isImporterMetadataRepair =
                LoadedFile.IsImporterAugmentedRemarksPlaceholder(element);
            var metadataRepairIndex = isImporterMetadataRepair &&
                parent?.Name.LocalName == "Docs"
                ? parent.Elements("remarks")
                    .TakeWhile(sibling => !ReferenceEquals(sibling, element))
                    .Count(LoadedFile.IsImporterAugmentedRemarksPlaceholder)
                : -1;
            return new Placeholder(
                order,
                name,
                key,
                target,
                isImporterMetadataRepair,
                remarksChildIndex,
                metadataRepairIndex);
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
                ["M:Android.Service.Autofill.ImageTransformation.Builder.AddOption(Java.Util.Regex.Pattern,System.Int32)"] =
                    Mapping("android/service/autofill/ImageTransformation$Builder", "addOption", "(Ljava/util/regex/Pattern;I)Landroid/service/autofill/ImageTransformation$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.ImageTransformation.Builder.AddOption(Java.Util.Regex.Pattern,System.Int32,System.String)"] =
                    Mapping("android/service/autofill/ImageTransformation$Builder", "addOption", "(Ljava/util/regex/Pattern;ILjava/lang/CharSequence;)Landroid/service/autofill/ImageTransformation$Builder;"),
                ["M:Android.Service.Autofill.SaveInfo.Builder.SetDescription(System.String)"] =
                    Mapping("android/service/autofill/SaveInfo$Builder", "setDescription", "(Ljava/lang/CharSequence;)Landroid/service/autofill/SaveInfo$Builder;"),
                ["M:Android.Service.Autofill.Dataset.Builder.SetInlinePresentation(Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setInlinePresentation", "(Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetInlinePresentation(Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setInlinePresentation", "(Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Android.Widget.RemoteViews)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Landroid/widget/RemoteViews;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern,Android.Widget.RemoteViews)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;Landroid/widget/RemoteViews;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.Dataset.Builder.SetValue(Android.Views.Autofill.AutofillId,Android.Views.Autofill.AutofillValue,Java.Util.Regex.Pattern,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/Dataset$Builder", "setValue", "(Landroid/view/autofill/AutofillId;Landroid/view/autofill/AutofillValue;Ljava/util/regex/Pattern;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/Dataset$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews)"] =
                    Mapping("android/service/autofill/FillResponse$Builder", "setAuthentication", "([Landroid/view/autofill/AutofillId;Landroid/content/IntentSender;Landroid/widget/RemoteViews;)Landroid/service/autofill/FillResponse$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/FillResponse$Builder", "setAuthentication", "([Landroid/view/autofill/AutofillId;Landroid/content/IntentSender;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/FillResponse$Builder;",
                        useFirstMeaningfulSummary: true),
                ["M:Android.Service.Autofill.FillResponse.Builder.SetAuthentication(Android.Views.Autofill.AutofillId[],Android.Content.IntentSender,Android.Widget.RemoteViews,Android.Service.Autofill.InlinePresentation,Android.Service.Autofill.InlinePresentation)"] =
                    Mapping("android/service/autofill/FillResponse$Builder", "setAuthentication", "([Landroid/view/autofill/AutofillId;Landroid/content/IntentSender;Landroid/widget/RemoteViews;Landroid/service/autofill/InlinePresentation;Landroid/service/autofill/InlinePresentation;)Landroid/service/autofill/FillResponse$Builder;",
                        useFirstMeaningfulSummary: true),
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
            fragment = NormalizeKnownAndroidParagraphBoundary(fragment, url);
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
            if (url == DreamFocusSourceUrl)
            {
                prose = prose.Replace(
                    StaleDreamFocusSourceLink,
                    StaleDreamFocusSourceLink.Replace(
                        "View.onWindowFocusChangedNotLocked(boolean)",
                        "View.onWindowFocusChanged(boolean)",
                        StringComparison.Ordinal),
                    StringComparison.Ordinal);
            }
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
                request.Kind,
                HasMalformedSourceMarkup: prose.Contains("()}", StringComparison.Ordinal));
        }

        internal static string NormalizeKnownAndroidParagraphBoundary(string html, string sourceUrl)
        {
            if (!sourceUrl.Equals(ControlTemplateSourceUrl, StringComparison.Ordinal))
                return html;

            return Regex.Replace(
                html,
                @"(?<open><p\b[^>]*>)(?<lead>.*?)\r?\n[ \t]*\r?\n[ \t]*(?<description>.*?)</p>",
                match =>
                    HtmlText(match.Groups["lead"].Value) == ControlTemplateLead &&
                    HtmlText(match.Groups["description"].Value) == ControlTemplateDescription
                        ? match.Groups["open"].Value + match.Groups["lead"].Value +
                            "</p><p>" + match.Groups["description"].Value + "</p>"
                        : match.Value,
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
            html = Regex.Replace(
                html,
                @"</p>\s*\.\s*<br>\s*(?<thread>This method must be called from the main thread of your app\.)\s*</p>\s*</p>",
                "</p><p>${thread}</p>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var paragraphs = new List<(int Position, SourceParagraph Paragraph)>();
            var codeRanges = new List<(int Start, int End, SourceParagraph Paragraph)>();
            var stack = new Stack<(string Tag, int TagStart, int ContentStart)>();

            IReadOnlyList<(int Start, int End, SourceParagraph Paragraph)> VisibleCodeRanges() =>
                codeRanges
                    .Where(candidate => !codeRanges.Any(container =>
                        container.Start <= candidate.Start &&
                        container.End >= candidate.End &&
                        (container.Start < candidate.Start || container.End > candidate.End)))
                    .OrderBy(code => code.Start)
                    .ToList();

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
                    codeRanges.Add((open.TagStart, elementEnd, new SourceParagraph(value, IsCode: true)));
                    return;
                }

                var nestedCode = VisibleCodeRanges()
                    .Where(code => code.Start >= open.ContentStart && code.End <= contentEnd)
                    .ToList();
                var textStart = open.ContentStart;
                foreach (var code in nestedCode)
                {
                    if (code.Start < textStart)
                        continue;
                    AddSourceTextParagraph(
                        html[textStart..code.Start],
                        textStart,
                        paragraphs);
                    paragraphs.Add((code.Start, code.Paragraph));
                    textStart = code.End;
                }
                if (textStart <= contentEnd)
                    AddSourceTextParagraph(
                        html[textStart..contentEnd],
                        textStart,
                        paragraphs,
                        preserveEmpty: nestedCode.Count == 0);
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
            foreach (var code in VisibleCodeRanges().Where(code =>
                !paragraphs.Any(paragraph => paragraph.Position == code.Start &&
                    paragraph.Paragraph == code.Paragraph)))
            {
                paragraphs.Add((code.Start, code.Paragraph));
            }
            var ordered = paragraphs
                .OrderBy(paragraph => paragraph.Position)
                .Select(paragraph => paragraph.Paragraph)
                .ToList();
            var usable = new List<SourceParagraph>();
            for (var index = 0; index < ordered.Count; index++)
            {
                var paragraph = ordered[index];
                if (paragraph.IsCode)
                {
                    if (!string.IsNullOrWhiteSpace(paragraph.Text))
                        usable.Add(paragraph);
                    continue;
                }

                var isCddlIntroduction = IsCddlCodeLeadIn(paragraph.Text) &&
                    index + 1 < ordered.Count &&
                    ordered[index + 1].IsCode &&
                    !string.IsNullOrWhiteSpace(ordered[index + 1].Text);
                var text = isCddlIntroduction
                    ? paragraph.Text
                    : CleanSourceParagraph(paragraph.Text);
                if (isCddlIntroduction || IsMeaningfulChannel(text, "remarks"))
                    usable.Add(paragraph with { Text = text });
            }
            return usable;
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
            List<(int Position, SourceParagraph Paragraph)> paragraphs,
            bool preserveEmpty = false)
        {
            var sourceText = CleanSourceText(HtmlText(html));
            if (preserveEmpty || sourceText.Length > 0)
                paragraphs.Add((position, new SourceParagraph(sourceText, IsCode: false)));
        }

        internal static List<SourceParagraph> ExtractBlocks(string html) =>
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
                .SelectMany(match => ExtractBlockParagraphs(match.Groups["body"].Value))
                .Where(paragraph => paragraph.Text.Length > 0 &&
                    !IsJavaDescriptionCopiedLabel(paragraph.Text))
                .ToList();

        static List<SourceParagraph> ExtractBlockParagraphs(string html)
        {
            html = NormalizeJavaSignatureParagraphBoundary(html);
            var codeExamples = Regex.Matches(
                html,
                @"<pre\b[^>]*>(?<body>.*?)</pre>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (codeExamples.Count == 0)
                return [new SourceParagraph(HtmlText(html), IsCode: false)];

            var paragraphs = new List<SourceParagraph>();
            var position = 0;
            foreach (Match codeExample in codeExamples)
            {
                AddBlockTextParagraph(
                    html[position..codeExample.Index],
                    paragraphs,
                    isImmediatelyBeforeCode: true);
                var code = HtmlCodeText(codeExample.Groups["body"].Value);
                if (code.Length > 0)
                    paragraphs.Add(new SourceParagraph(code, IsCode: true));
                position = codeExample.Index + codeExample.Length;
            }
            AddBlockTextParagraph(html[position..], paragraphs);
            return paragraphs;
        }

        static string NormalizeJavaSignatureParagraphBoundary(string html) =>
            Regex.Replace(
                html,
                @"(?<signature>The method signature is of the form\s*<code\b[^>]*>.*?</code>)\s*</p>\s*(?<next><p\b[^>]*>\s*)(?=The symbolic type descriptor\b)",
                match => match.Groups["signature"].Value + ".</p>" + match.Groups["next"].Value,
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static void AddBlockTextParagraph(
            string html,
            List<SourceParagraph> paragraphs,
            bool isImmediatelyBeforeCode = false)
        {
            var sourceText = CleanSourceText(HtmlText(html));
            var text = CleanSourceParagraph(sourceText);
            if (isImmediatelyBeforeCode && IsExplanatoryJavaCodeLeadIn(sourceText))
                paragraphs.Add(new SourceParagraph(sourceText, IsCode: false));
            else if (text.Length > 0)
                paragraphs.Add(new SourceParagraph(text, IsCode: false));
        }

        static bool IsJavaDescriptionCopiedLabel(string text) =>
            Regex.IsMatch(
                NormalizeText(text).Trim(),
                @"^Description copied from (?:class|interface):\s+\S+$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
                @"(?<introOpen><p\b[^>]*>)(?<introBody>.*?)</p>\s*<(?<tag>ul|ol)\b[^>]*>(?<body>(?:(?<nested><(?:ul|ol)\b[^>]*>)|(?<-nested></(?:ul|ol)\s*>)|(?!</?(?:ul|ol)\b).)*(?(nested)(?!)))</\k<tag>\s*>",
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
                    ? @"<(?:script|style|svg|button)\b[^>]*>.*?</(?:script|style|svg|button)>"
                    : @"<(?:script|style|svg|button|pre|devsite-code)\b[^>]*>.*?</(?:script|style|svg|button|pre|devsite-code)>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var withBreaks = Regex.Replace(
                withoutIgnored,
                @"</?(?:p|div|li|tr|td|th|dd|dt|br|ul|ol|blockquote)\b[^>]*>",
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return NormalizeAndroidSourceText(
                CleanSourceText(StripHtmlTags(withBreaks, addWhitespace: false)));
        }

        internal static string NormalizeAndroidSourceText(string text)
        {
            text = Regex.Replace(
                text,
                @"(?<![A-Za-z0-9_.])(?<type>[A-Z][A-Za-z0-9_]*)\k<type>\.(?<member>[A-Za-z_][A-Za-z0-9_]*\([^)]*\))",
                "${type}.${member}",
                RegexOptions.CultureInvariant);

            // Correct a known typo on the Android SoftKeyboardController reference page.
            return text.Replace(
                "services's main thread if the handler is null",
                "service's main thread if the handler is null",
                StringComparison.Ordinal);
        }

        internal static string HtmlTableCellText(string html)
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
            var unbalancedListMarkup =
                Regex.Matches(html, @"<(?:ul|ol)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count !=
                Regex.Matches(html, @"</(?:ul|ol)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
            if (unbalancedListMarkup &&
                listIntroduction.Contains("One or more of:.", StringComparison.Ordinal) &&
                listIntroduction.Contains("Value is either", StringComparison.Ordinal) &&
                listItems.Distinct(StringComparer.Ordinal).Count() < listItems.Count)
            {
                listIntroduction = Regex.Replace(
                    listIntroduction,
                    @"\s*One or more of:\.\s*(?=Value is either\b)",
                    " ",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                listItems = listItems.Distinct(StringComparer.Ordinal).ToList();
            }
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
        string SourceKind,
        IReadOnlyDictionary<string, string>? UnsafeTargets = null,
        bool HasMalformedSourceMarkup = false,
        KnownEapChannelCorrection? EapCorrection = null);

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

    sealed record Replacement(
        string? Text,
        string? Reason,
        string Detail,
        IReadOnlyList<SourceParagraph>? Remarks = null)
    {
        public static Replacement Use(string text) => new(text, null, "");
        public static Replacement UseRemarks(IReadOnlyList<SourceParagraph> remarks) =>
            new(remarks[0].Text, null, "", remarks);
        public static Replacement RemoveRemarksPlaceholder() =>
            new("", null, "", []);
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
            string sourceUrl,
            string reason = "exact_structural_match",
            string detail = "") =>
            new()
            {
                Status = status,
                Path = path,
                Member = member,
                Target = target,
                Reason = reason,
                Detail = detail,
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
