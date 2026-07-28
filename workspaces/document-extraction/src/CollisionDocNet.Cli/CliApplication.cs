using System.Globalization;
using CollisionDocNet.Core;
using CollisionDocNet.Extraction;
using CollisionDocNet.Model;

namespace CollisionDocNet.Cli;

public static class CliApplication
{
    public const int UsageExitCode = 64;

    public static Task<int> RunAsync(string[] args, Stream standardInput, TextWriter standardOutput,
        TextWriter standardError, CancellationToken cancellationToken = default) =>
        RunAsync(args, standardInput, standardOutput, standardError, PhysicalCliFileSystem.Instance, cancellationToken);

    internal static async Task<int> RunAsync(string[] args, Stream standardInput, TextWriter standardOutput,
        TextWriter standardError, ICliFileSystem fileSystem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (!TryParse(args, out CliCommand? command, out string? usageError))
        {
            await WriteLineAsync(standardError, $"{{\"code\":\"usage\",\"message\":\"{usageError}\"}}").ConfigureAwait(false);
            return UsageExitCode;
        }

        CliCommand parsed = command ?? throw new InvalidOperationException("A successful parse must produce a command.");
        if (parsed.Kind == CommandKind.Version)
        {
            await WriteLineAsync(standardOutput, CliJson.Serialize(new VersionEnvelope(
                "collisiondocnet", DocumentExtractor.ExtractorVersion, ExtractionResult.CurrentSchemaVersion))).ConfigureAwait(false);
            return 0;
        }
        if (parsed.Kind == CommandKind.Help)
        {
            await WriteLineAsync(standardOutput, CliJson.Serialize(new HelpEnvelope("collisiondocnet",
                "collisiondocnet <detect|extract> --input <path|-> [--name <filename>] [--media-type <hint>] | collisiondocnet <help|version>"))).ConfigureAwait(false);
            return 0;
        }

        try
        {
            ExtractionInput input;
            string fileName;
            Stream? ownedInput = null;
            if (parsed.InputPath == "-")
            {
                input = ExtractionInput.FromStream(standardInput);
                fileName = parsed.Name!;
            }
            else
            {
                string path = CliPathPolicy.ResolveInput(parsed.InputPath!, fileSystem);
                ownedInput = fileSystem.OpenRead(path);
                input = ExtractionInput.FromStream(ownedInput);
                fileName = fileSystem.GetFileName(path);
            }

            try
            {
                ExtractionPolicy policy = CreatePolicy(parsed);
                var request = new ExtractionRequest(input, "cli-input", fileName, parsed.MediaType, policy);
                ExtractionResult result = await DocumentExtractor.ExtractAsync(request, cancellationToken).ConfigureAwait(false);
                if (parsed.Kind == CommandKind.Detect)
                {
                    await WriteLineAsync(standardOutput, CliJson.Serialize(new DetectionEnvelope(
                        "collisiondocnet-detection/1", result.DetectedContainer.ToString(), result.DetectedFormat.ToString(),
                        result.Outcome.ToString(), result.SourceHash.Hex))).ConfigureAwait(false);
                }
                else
                {
                    CancellationToken publicationToken = result.Outcome is ExtractionOutcome.Cancelled or ExtractionOutcome.TimedOut
                        ? CancellationToken.None
                        : cancellationToken;
                    await OutputBundleWriter.WriteAsync(parsed.OutputPath!, result, fileSystem, publicationToken).ConfigureAwait(false);
                    await WriteLineAsync(standardOutput, CliJson.Serialize(new CompletionEnvelope(
                        "collisiondocnet-completion/1", result.Outcome.ToString(), "result.json"))).ConfigureAwait(false);
                }
                return ExitCode(result.Outcome);
            }
            finally
            {
                if (ownedInput is not null) await ownedInput.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            if (parsed.Kind == CommandKind.Extract)
            {
                await WriteLineAsync(standardOutput, CliJson.Serialize(new CompletionEnvelope(
                    "collisiondocnet-completion/1", ExtractionOutcome.Cancelled.ToString(), null))).ConfigureAwait(false);
            }
            else
            {
                await WriteLineAsync(standardOutput, CliJson.Serialize(new DetectionEnvelope(
                    "collisiondocnet-detection/1", "Unknown", "Unknown", ExtractionOutcome.Cancelled.ToString(), null))).ConfigureAwait(false);
            }
            await WriteLineAsync(standardError, "{\"code\":\"cancelled\",\"message\":\"The operation was cancelled.\"}").ConfigureAwait(false);
            return ExitCode(ExtractionOutcome.Cancelled);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (parsed.Kind == CommandKind.Extract)
            {
                await WriteLineAsync(standardOutput, CliJson.Serialize(new CompletionEnvelope(
                    "collisiondocnet-completion/1", ExtractionOutcome.TechnicalFailure.ToString(), null))).ConfigureAwait(false);
            }
            else
            {
                await WriteLineAsync(standardOutput, CliJson.Serialize(new DetectionEnvelope(
                    "collisiondocnet-detection/1", "Unknown", "Unknown", ExtractionOutcome.TechnicalFailure.ToString(), null))).ConfigureAwait(false);
            }
            await WriteLineAsync(standardError, "{\"code\":\"technical-failure\",\"message\":\"The CLI could not read the input or create the output bundle.\"}").ConfigureAwait(false);
            return ExitCode(ExtractionOutcome.TechnicalFailure);
        }
    }

    internal static int ExitCode(ExtractionOutcome outcome) => outcome switch
    {
        ExtractionOutcome.Complete => 0,
        ExtractionOutcome.Partial => 10,
        ExtractionOutcome.UnsupportedFormat => 20,
        ExtractionOutcome.UnsupportedFeature => 21,
        ExtractionOutcome.Encrypted => 22,
        ExtractionOutcome.Corrupt => 23,
        ExtractionOutcome.ResourceLimitExceeded => 24,
        ExtractionOutcome.Cancelled => 25,
        ExtractionOutcome.TimedOut => 26,
        _ => 70,
    };

    private static ExtractionPolicy CreatePolicy(CliCommand command)
    {
        ResourceLimits baseline = ResourceLimits.CreateCollisionSpikeDefault();
        long input = command.MaxInputBytes ?? baseline.MaxInputBytes;
        long decoded = command.MaxDecodedBytes ?? baseline.MaxDecodedBytes;
        int objects = command.MaxObjects ?? baseline.MaxObjects;
        int text = command.MaxTextCharacters ?? baseline.MaxTextCharacters;
        int assets = command.MaxAssets ?? baseline.MaxAssets;
        long assetBytes = command.MaxAssetBytes ?? baseline.MaxAssetBytes;
        int depth = command.MaxNestingDepth ?? baseline.MaxNestingDepth;
        TimeSpan elapsed = command.MaxElapsedMilliseconds is long milliseconds
            ? TimeSpan.FromMilliseconds(milliseconds)
            : baseline.MaxElapsed;
        var limits = new ResourceLimits(ResourceLimits.CollisionSpikeTenMegabytePolicy, input, decoded, objects,
            text, assets, assetBytes, depth, elapsed);
        return new ExtractionPolicy(ExtractionPolicy.DefaultPolicyId, DeterministicText.PolicyId,
            StableIdentity.PolicyId, limits);
    }

    private static bool TryParse(string[] args, out CliCommand? command, out string? error)
    {
        command = null;
        error = null;
        if (args.Length == 1 && args[0] is "version" or "help")
        {
            command = CliCommand.Simple(args[0] == "version" ? CommandKind.Version : CommandKind.Help);
            return true;
        }
        if (args.Length == 0 || !TryCommand(args[0], out CommandKind kind))
        {
            error = "Expected help, version, detect or extract.";
            return false;
        }

        string? input = null;
        string? output = null;
        string? name = null;
        string? mediaType = null;
        string? limitsClass = null;
        bool quiet = false;
        var numeric = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--quiet")
            {
                if (quiet) { error = "--quiet was repeated."; return false; }
                quiet = true;
                continue;
            }
            if (index + 1 >= args.Length) { error = "An option value is missing."; return false; }
            string value = args[++index];
            if (string.IsNullOrWhiteSpace(value)) { error = "An option value is empty."; return false; }
            switch (option)
            {
                case "--input": if (!AssignOnce(ref input, value)) { error = "--input was repeated."; return false; } break;
                case "--output": if (!AssignOnce(ref output, value)) { error = "--output was repeated."; return false; } break;
                case "--name": if (!AssignOnce(ref name, value)) { error = "--name was repeated."; return false; } break;
                case "--media-type": if (!AssignOnce(ref mediaType, value)) { error = "--media-type was repeated."; return false; } break;
                case "--limits": if (!AssignOnce(ref limitsClass, value)) { error = "--limits was repeated."; return false; } break;
                case "--max-input-bytes" or "--max-decoded-bytes" or "--max-objects" or "--max-text-characters" or
                     "--max-assets" or "--max-asset-bytes" or "--max-nesting-depth" or "--max-elapsed-ms":
                    bool permitsZero = option is "--max-assets" or "--max-asset-bytes" or "--max-nesting-depth";
                    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
                        parsed < (permitsZero ? 0 : 1) || !numeric.TryAdd(option, parsed))
                    { error = "A numeric limit is invalid or repeated."; return false; }
                    break;
                default: error = "An unknown option was supplied."; return false;
            }
        }

        if (input is null) { error = "--input is required."; return false; }
        if (input == "-" && name is null) { error = "--input - requires --name."; return false; }
        if (input != "-" && name is not null) { error = "--name is valid only with --input -."; return false; }
        if (kind == CommandKind.Extract && output is null) { error = "extract requires --output."; return false; }
        if (kind == CommandKind.Detect && output is not null) { error = "detect does not accept --output."; return false; }
        if (limitsClass is not null && limitsClass != ResourceLimits.CollisionSpikeTenMegabytePolicy)
        { error = "The named resource class is not supported."; return false; }

        ResourceLimits baseline = ResourceLimits.CreateCollisionSpikeDefault();
        if (!TryLower(numeric, "--max-input-bytes", baseline.MaxInputBytes, out long? maxInput) ||
            !TryLower(numeric, "--max-decoded-bytes", baseline.MaxDecodedBytes, out long? maxDecoded) ||
            !TryLowerInt(numeric, "--max-objects", baseline.MaxObjects, out int? maxObjects) ||
            !TryLowerInt(numeric, "--max-text-characters", baseline.MaxTextCharacters, out int? maxText) ||
            !TryLowerInt(numeric, "--max-assets", baseline.MaxAssets, out int? maxAssets) ||
            !TryLower(numeric, "--max-asset-bytes", baseline.MaxAssetBytes, out long? maxAssetBytes) ||
            !TryLowerInt(numeric, "--max-nesting-depth", baseline.MaxNestingDepth, out int? maxDepth) ||
            !TryLower(numeric, "--max-elapsed-ms", checked((long)baseline.MaxElapsed.TotalMilliseconds), out long? maxElapsed))
        { error = "A command-line limit may lower but not raise its named resource class."; return false; }

        command = new(kind, input, name, output, mediaType, quiet, maxInput, maxDecoded, maxObjects,
            maxText, maxAssets, maxAssetBytes, maxDepth, maxElapsed);
        return true;
    }

    private static bool AssignOnce(ref string? target, string value)
    {
        if (target is not null) return false;
        target = value;
        return true;
    }

    private static bool TryLower(Dictionary<string, long> values, string key, long maximum, out long? result)
    {
        if (!values.TryGetValue(key, out long value)) { result = null; return true; }
        result = value;
        return value <= maximum;
    }

    private static bool TryLowerInt(Dictionary<string, long> values, string key, int maximum, out int? result)
    {
        if (!values.TryGetValue(key, out long value)) { result = null; return true; }
        if (value > int.MaxValue) { result = null; return false; }
        result = (int)value;
        return value <= maximum;
    }

    private static bool TryCommand(string value, out CommandKind kind)
    {
        kind = value switch { "detect" => CommandKind.Detect, "extract" => CommandKind.Extract, _ => CommandKind.Help };
        return value is "detect" or "extract";
    }

    private static async Task WriteLineAsync(TextWriter writer, string value)
    {
        await writer.WriteAsync(value).ConfigureAwait(false);
        await writer.WriteAsync("\n").ConfigureAwait(false);
    }

    private enum CommandKind { Help, Version, Detect, Extract }
    private sealed record CliCommand(CommandKind Kind, string? InputPath, string? Name, string? OutputPath,
        string? MediaType, bool Quiet, long? MaxInputBytes, long? MaxDecodedBytes, int? MaxObjects,
        int? MaxTextCharacters, int? MaxAssets, long? MaxAssetBytes, int? MaxNestingDepth, long? MaxElapsedMilliseconds)
    {
        internal static CliCommand Simple(CommandKind kind) => new(kind, null, null, null, null, false,
            null, null, null, null, null, null, null, null);
    }
}
