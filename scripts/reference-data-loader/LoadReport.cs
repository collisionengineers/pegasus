using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Pegasus.ReferenceDataLoader;

/// <summary>
/// What a load would write, computed from the stored state and the files
/// alone, with no clock in it: its SHA-256 is the plan hash an
/// <c>apply</c> must be given.
/// </summary>
internal sealed record LoadPlan(
    string Target,
    string ActorStaffId,
    string Batch,
    string ReviewedAt,
    IReadOnlyList<InputFile> Files,
    Baseline Baseline,
    IReadOnlyList<KindCount> Counts,
    IReadOnlyList<SkippedRows> Skipped,
    IReadOnlyList<RowReject> Rejects,
    IReadOnlyList<HygieneDrop> Hygiene,
    IReadOnlyList<RestEntry> RestListed,
    IReadOnlyList<Overwrite> Overwrites,
    LinkCounts Links,
    LocationCounts Locations,
    IReadOnlyList<MatchedLocation> MatchedLocations,
    IReadOnlyList<ContactWrite> ContactWrites,
    IReadOnlyList<LocationWrite> LocationWrites);

internal sealed record InputFile(string Name, string Sha256, int Rows);

/// <summary>The stored directory the plan was computed against.</summary>
internal sealed record Baseline(int Principals, int ClaimSources, IReadOnlyList<string> PrincipalCodes);

internal sealed record KindCount(string Kind, int Create, int Update, int Unchanged, int Reject);

/// <summary>Rows counted and never written: not approved, or an action that loads nothing.</summary>
internal sealed record SkippedRows(string File, string Action, string Review, int Rows);

internal sealed record RowReject(string File, string RowId, string Reason);

/// <summary>A value <c>ContactDirectoryPolicy.Normalize</c> refuses: the field is left blank (R23).</summary>
internal sealed record HygieneDrop(string File, string RowId, string Column, string Value, string Reason);

/// <summary>One of the values the review did not keep, as <c>rest_listed</c> gives it (R23 "report the rest").</summary>
internal sealed record RestEntry(string File, string RowId, string Entry);

/// <summary>A stored value the load replaces, before and after.</summary>
internal sealed record Overwrite(string File, string RowId, string Field, string Before, string? After);

internal sealed record LinkCounts(int Linked, int Skipped, int Rejected);

internal sealed record LocationCounts(int Send, int Links, int Matched, int Skipped, int Rejected, int LinksRejected);

/// <summary>A location the review matched to a contact, so it writes nothing (R22).</summary>
internal sealed record MatchedLocation(string RowId, string ContactRowId, string ContactKind, bool ContactLoaded);

internal sealed record ContactFields(
    string Name,
    string? ContactPerson,
    string? Email,
    string? Telephone,
    string? Address,
    string? Postcode,
    bool Active,
    string? NotesOnEveryCase,
    string? PrincipalCode,
    string? InspectionMode);

/// <summary>One <c>SaveAsync</c>: step <c>a</c> for a Principal, <c>b</c> for every other type.</summary>
/// <param name="Links">The linked Principals' codes; the apply turns them into ids once step a has run.</param>
internal sealed record ContactWrite(
    string Step,
    string File,
    string RowId,
    string Kind,
    Guid OrganizationId,
    string OperationKey,
    long ExpectedVersion,
    bool Create,
    ContactFields Fields,
    IReadOnlyList<string> Links);

/// <summary>One inspection-location save, step c.</summary>
internal sealed record LocationWrite(
    string RowId,
    string OperationKey,
    string Name,
    string Address,
    string Postcode,
    IReadOnlyList<string> Principals);

internal sealed record LoadOutcome(
    int Written,
    LocationOutcomes Locations,
    IReadOnlyList<RowReject> Rejects,
    IReadOnlyList<WriteResult> Results);

internal sealed record LocationOutcomes(int Created, int Updated, int KeptExisting);

internal sealed record WriteResult(string OperationKey, string Outcome, long? Version);

internal sealed record LoadReportDocument(string PlanHash, LoadPlan Plan, LoadOutcome? Outcome);

internal static class LoadReport
{
    private static readonly JsonSerializerOptions PlanJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReportJson = new(PlanJson)
    {
        WriteIndented = true,
        NewLine = "\n"
    };

    internal static string Hash(LoadPlan plan) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan, PlanJson)));

    /// <summary>The JSON report at <paramref name="path"/> and the same report as text beside it.</summary>
    internal static async Task WriteAsync(
        string path,
        string planHash,
        LoadPlan plan,
        LoadOutcome? outcome,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(
            path,
            JsonSerializer.SerializeToUtf8Bytes(new LoadReportDocument(planHash, plan, outcome), ReportJson),
            cancellationToken);
        await File.WriteAllTextAsync(Path.ChangeExtension(path, ".txt"), Text(planHash, plan, outcome), cancellationToken);
    }

    internal static string Summary(string planHash, string path, LoadOutcome? outcome) =>
        outcome is null
            ? Line($"Plan hash {planHash}; report {path}")
            : Line($"Plan hash {planHash}; report {path}; {outcome.Written} written, {outcome.Rejects.Count} rejected at write");

    private static string Text(string planHash, LoadPlan plan, LoadOutcome? outcome)
    {
        var lines = new List<string>
        {
            "Reference-data load",
            Line($"Plan hash: {planHash}"),
            Line($"Target: {plan.Target}"),
            Line($"Actor staff id: {plan.ActorStaffId}"),
            Line($"Batch: {plan.Batch}; inspection locations reviewed at {plan.ReviewedAt}"),
            Line($"Baseline: {plan.Baseline.Principals} Principals, {plan.Baseline.ClaimSources} Claim Sources"),
            Line($"Stored Principal codes: {string.Join(' ', plan.Baseline.PrincipalCodes)}")
        };
        Section(lines, "Files", plan.Files.Select(file => Line($"  {file.Name}: {file.Rows} rows, SHA-256 {file.Sha256}")));
        Section(lines, "Contacts: create, update, unchanged, reject", plan.Counts.Select(count =>
            Line($"  {count.Kind}: {count.Create}, {count.Update}, {count.Unchanged}, {count.Reject}")));
        lines.Add(Line($"Links: {plan.Links.Linked} linked, {plan.Links.Skipped} not loaded, {plan.Links.Rejected} rejected"));
        lines.Add(Line(
            $"Inspection locations: {plan.Locations.Send} to send with {plan.Locations.Links} links, {plan.Locations.Matched} matched to a contact, {plan.Locations.Skipped} not loaded, {plan.Locations.Rejected} rejected, {plan.Locations.LinksRejected} links rejected"));
        lines.Add(Line($"Writes: {plan.ContactWrites.Count} contact saves, {plan.LocationWrites.Count} inspection-location saves"));
        Section(lines, "Rows not loaded: file, action, review, rows", plan.Skipped.Select(skipped =>
            Line($"  {skipped.File}, {skipped.Action}, {skipped.Review}: {skipped.Rows}")));
        Section(lines, "Rejects", plan.Rejects.Select(reject => Line($"  {reject.File} {reject.RowId}: {reject.Reason}")));
        Section(lines, "Values Normalize refuses, left blank", plan.Hygiene.Select(drop =>
            Line($"  {drop.File} {drop.RowId} {drop.Column} '{drop.Value}': {drop.Reason}")));
        Section(lines, "Stored values overwritten", plan.Overwrites.Select(overwrite =>
            Line($"  {overwrite.File} {overwrite.RowId} {overwrite.Field}: '{overwrite.Before}' -> '{overwrite.After}'")));
        Section(lines, "Inspection locations matched to a contact", plan.MatchedLocations.Select(matched =>
            Line($"  {matched.RowId} -> {matched.ContactKind} {matched.ContactRowId}{(matched.ContactLoaded ? string.Empty : " (not in this load)")}")));
        Section(lines, "The rest, as listed", plan.RestListed.Select(rest => Line($"  {rest.File} {rest.RowId} {rest.Entry}")));
        if (outcome is not null)
        {
            Section(lines, "Applied", [
                Line($"  {outcome.Written} written"),
                Line($"  inspection locations: {outcome.Locations.Created} created, {outcome.Locations.Updated} updated, {outcome.Locations.KeptExisting} kept as stored")
            ]);
            Section(lines, "Rejected at write", outcome.Rejects.Select(reject => Line($"  {reject.File} {reject.RowId}: {reject.Reason}")));
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void Section(List<string> lines, string heading, IEnumerable<string> entries)
    {
        lines.Add(string.Empty);
        lines.Add(heading);
        var before = lines.Count;
        lines.AddRange(entries);
        if (lines.Count == before)
        {
            lines.Add("  none");
        }
    }

    private static string Line(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
