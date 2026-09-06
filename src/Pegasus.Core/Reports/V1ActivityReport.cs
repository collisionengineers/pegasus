using Pegasus.Core.Identity;
using System.Text;

namespace Pegasus.Core.Reports;

public sealed record PrincipalReportActivity(
    Guid PrincipalId,
    string PrincipalCode,
    int GenerationEvents,
    int GeneratedArtifacts,
    int Sent,
    int Ready,
    int MissingOriginForGeneratedTurnaround,
    int MissingOriginForReadyTurnaround,
    int MissingOriginForSentTurnaround,
    int MissingSentActor,
    TimeSpan? AverageReceivedToGeneration,
    TimeSpan? AverageReceivedToGeneratedArtifact,
    TimeSpan? AverageReceivedToReady,
    TimeSpan? AverageReceivedToSent,
    int CurrentTriage,
    DateTimeOffset? OldestCurrentTriageCreatedAtUtc,
    int CurrentHeldCases,
    DateTimeOffset? OldestHeldAtUtc,
    int HeldWithoutRecordedHoldEvent,
    IReadOnlyList<PrincipalReportArtifactTypeActivity> ArtifactTypes);

public sealed record PrincipalReportArtifactTypeActivity(
    string Kind,
    int Generated,
    int PendingOrFailed);

public sealed record PrincipalReportActivityReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<PrincipalReportActivity> Rows);

public interface IV1ActivityReportQueries
{
    Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public static class PrincipalReportActivityCsv
{
    public const string Header =
        "Principal,Generation events,Generated artifacts,Pending or failed,Sent,Ready transitions,Missing origin for generated turnaround,Missing origin for Ready turnaround,Missing origin for Sent turnaround,Missing sender attribution,Received to generation,Received to generated artifact,Received to Ready,Received to Sent,Current Triage,Oldest current Triage created UTC,Current held cases,Oldest held UTC,Held without recorded hold event,Report types";

    public static string ToCsv(IReadOnlyList<PrincipalReportActivity> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder(Header).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(EngineerActivityReportCsv.EscapeField(row.PrincipalCode)).Append(',')
                .Append(row.GenerationEvents).Append(',')
                .Append(row.GeneratedArtifacts).Append(',')
                .Append(row.ArtifactTypes.Sum(x => x.PendingOrFailed)).Append(',')
                .Append(row.Sent).Append(',')
                .Append(row.Ready).Append(',')
                .Append(row.MissingOriginForGeneratedTurnaround).Append(',')
                .Append(row.MissingOriginForReadyTurnaround).Append(',')
                .Append(row.MissingOriginForSentTurnaround).Append(',')
                .Append(row.MissingSentActor).Append(',')
                .Append(Format(row.AverageReceivedToGeneration)).Append(',')
                .Append(Format(row.AverageReceivedToGeneratedArtifact)).Append(',')
                .Append(Format(row.AverageReceivedToReady)).Append(',')
                .Append(Format(row.AverageReceivedToSent)).Append(',')
                .Append(row.CurrentTriage).Append(',')
                .Append(row.OldestCurrentTriageCreatedAtUtc?.ToString("O") ?? string.Empty).Append(',')
                .Append(row.CurrentHeldCases).Append(',')
                .Append(row.OldestHeldAtUtc?.ToString("O") ?? string.Empty).Append(',')
                .Append(row.HeldWithoutRecordedHoldEvent).Append(',')
                .Append(EngineerActivityReportCsv.EscapeField(string.Join("; ", row.ArtifactTypes
                    .Select(x => $"{x.Kind}: {x.Generated} generated, {x.PendingOrFailed} pending or failed"))))
                .Append("\r\n");
        }

        return builder.ToString();
    }

    private static string Format(TimeSpan? value) => value?.ToString("c") ?? string.Empty;

}

public sealed class GetV1ActivityReport(IV1ActivityReportQueries queries)
{
    public async Task<PrincipalReportActivityReport> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc));
        }

        var rows = await queries.GetAsync(fromUtc, toUtc, cancellationToken);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Any(IsInvalid)
            || rows.Select(x => x.PrincipalId).Distinct().Count() != rows.Count)
        {
            throw new InvalidDataException("The principal report query returned an invalid row.");
        }

        return new(fromUtc, toUtc, rows
            .OrderBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PrincipalId)
            .ToArray());
    }

    private static bool IsInvalid(PrincipalReportActivity row) =>
        row.PrincipalId == Guid.Empty
        || string.IsNullOrWhiteSpace(row.PrincipalCode)
        || row.GenerationEvents < 0
        || row.GeneratedArtifacts < 0
        || row.Sent < 0
        || row.Ready < 0
        || row.MissingOriginForGeneratedTurnaround < 0
        || row.MissingOriginForGeneratedTurnaround > row.GeneratedArtifacts
        || row.MissingOriginForReadyTurnaround < 0
        || row.MissingOriginForReadyTurnaround > row.Ready
        || row.MissingOriginForSentTurnaround < 0
        || row.MissingOriginForSentTurnaround > row.Sent
        || row.MissingSentActor < 0
        || row.MissingSentActor > row.Sent
        || row.AverageReceivedToGeneration < TimeSpan.Zero
        || row.AverageReceivedToGeneratedArtifact < TimeSpan.Zero
        || row.AverageReceivedToReady < TimeSpan.Zero
        || row.AverageReceivedToSent < TimeSpan.Zero
        || row.CurrentTriage < 0
        || row.CurrentHeldCases < 0
        || row.HeldWithoutRecordedHoldEvent < 0
        || row.HeldWithoutRecordedHoldEvent > row.CurrentHeldCases
        || row.ArtifactTypes is null
        || row.ArtifactTypes.Any(x => string.IsNullOrWhiteSpace(x.Kind)
            || x.Generated < 0 || x.PendingOrFailed < 0);
}
