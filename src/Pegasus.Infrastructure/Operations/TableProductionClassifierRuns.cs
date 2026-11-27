using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Email;

namespace Pegasus.Infrastructure.Operations;

/// <summary>
/// The live classifier's runs, read by the hosted test instance from the
/// <c>runs</c> table the live classifier writes in its own storage account
/// (ADR-0088 as amended; operator, 27 November 2026; R1318, R1319). It reads
/// only: one query over the days of the period, partitioned by the UTC date a
/// run started on, and one point read for a run's messages. It authenticates
/// with the same <c>CollisionSpike Graph Intake</c> registration and secret
/// as the instance's Graph mail and OCR, which holds <c>Storage Table Data
/// Reader</c> on that account alone. Nothing read is kept (R696). The Table
/// service's REST protocol is called directly, so the read needs no client
/// library the application does not already carry.
/// </summary>
internal sealed class TableProductionClassifierRuns(HttpClient httpClient, TokenCredential credential, Uri tableEndpoint)
    : IProductionClassifierRuns
{
    private const string Table = "runs";
    private const string ServiceVersion = "2019-02-02";
    private static readonly string[] StorageScopes = ["https://storage.azure.com/.default"];

    /// <summary>The columns the list reads: never a run's messages or its summary.</summary>
    private const string ListColumns =
        "PartitionKey,RowKey,startedAt,finishedAt,dryRun,outcome,eligible,awaitingDecision,"
        + "candidatesHidden,hiddenRuns,costUsd,durationMs,counts";

    public async Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var filter = string.Create(
            CultureInfo.InvariantCulture,
            $"PartitionKey ge '{Day(fromUtc)}' and PartitionKey le '{Day(toUtc)}'");
        var runs = new List<ClassifierRunSummary>();
        string? nextPartition = null;
        string? nextRow = null;
        do
        {
            var query = "$filter=" + Uri.EscapeDataString(filter) + "&$select=" + Uri.EscapeDataString(ListColumns);
            if (nextPartition is not null)
            {
                query += "&NextPartitionKey=" + Uri.EscapeDataString(nextPartition);
                if (nextRow is not null)
                {
                    query += "&NextRowKey=" + Uri.EscapeDataString(nextRow);
                }
            }

            using var response = await SendAsync(new Uri(tableEndpoint, Table + "()?" + query), cancellationToken);
            using var body = await ReadAsync(response, cancellationToken);
            if (body.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in value.EnumerateArray())
                {
                    if (Summary(entity) is { } run && run.StartedAtUtc >= fromUtc && run.StartedAtUtc < toUtc)
                    {
                        runs.Add(run);
                    }
                }
            }

            nextPartition = Header(response, "x-ms-continuation-NextPartitionKey");
            nextRow = Header(response, "x-ms-continuation-NextRowKey");
        }
        while (nextPartition is not null);

        return runs;
    }

    public async Task<ClassifierRunDetail?> GetAsync(Guid runId, DateOnly startedOnUtc, CancellationToken cancellationToken)
    {
        var address = new Uri(
            tableEndpoint,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Table}(PartitionKey='{startedOnUtc:yyyy-MM-dd}',RowKey='{runId:D}')"));
        using var response = await SendAsync(address, cancellationToken, allowNotFound: true);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        using var body = await ReadAsync(response, cancellationToken);
        var entity = body.RootElement;
        if (Summary(entity) is not { } summary)
        {
            return null;
        }

        var (items, itemsCut) = Items(entity);
        return new(
            summary with
            {
                Actions = items
                    .GroupBy(item => item.Item.Action)
                    .ToDictionary(group => group.Key, group => group.Count())
            },
            new(
                Boolean(entity, "dryRun"),
                Text(entity, "model") ?? string.Empty,
                Int(entity, "batch") ?? 0,
                Int(entity, "scanPages") ?? 0,
                Decimal(entity, "maxBudgetUsd") ?? 0m,
                Text(entity, "agentVersion") ?? string.Empty),
            Int(entity, "verdicts"),
            Int(entity, "failed"),
            Int(entity, "deferred"),
            Int(entity, "turns"),
            Text(entity, "error"),
            Text(entity, "summary"),
            RecordedBy: null,
            items,
            itemsCut || Boolean(entity, "itemsTruncated"));
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri address,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        HttpResponseMessage response;
        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(StorageScopes), cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.Add("x-ms-version", ServiceVersion);
            request.Headers.Add("x-ms-date", DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));
            request.Headers.Add("DataServiceVersion", "3.0;NetFx");
            request.Headers.Add("MaxDataServiceVersion", "3.0;NetFx");
            request.Headers.Accept.ParseAdd("application/json;odata=nometadata");
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (AuthenticationFailedException exception)
        {
            throw new ProductionClassifierRunsUnavailableException(refused: true, "The live runs' sign-in was refused.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new ProductionClassifierRunsUnavailableException(refused: false, "The live runs could not be reached.", exception);
        }

        if (response.IsSuccessStatusCode || (allowNotFound && response.StatusCode == HttpStatusCode.NotFound))
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        throw new ProductionClassifierRunsUnavailableException(
            status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            string.Create(CultureInfo.InvariantCulture, $"The live runs' table answered {(int)status}."));
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ProductionClassifierRunsUnavailableException(refused: false, "The live runs' table answered something that is not a table entity.", exception);
        }
    }

    private static ClassifierRunSummary? Summary(JsonElement entity)
    {
        if (!Guid.TryParse(Text(entity, "RowKey"), out var id) || Time(entity, "startedAt") is not { } startedAt)
        {
            return null;
        }

        var finishedAt = Time(entity, "finishedAt");
        var outcome = Text(entity, "outcome");
        return new(
            id,
            ClassifierRunEnvironment.Live,
            startedAt,
            finishedAt,
            Boolean(entity, "dryRun"),
            outcome,
            finishedAt is not null && outcome is not null ? ClassifierRunPolicy.OutcomeOf(outcome) : null,
            Int(entity, "eligible"),
            Int(entity, "awaitingDecision"),
            Boolean(entity, "candidatesHidden"),
            Int(entity, "hiddenRuns") ?? 0,
            Decimal(entity, "costUsd"),
            Int(entity, "durationMs"),
            Counts(Text(entity, "counts")));
    }

    private static Dictionary<ClassifierRunAction, int> Counts(string? json)
    {
        var counts = new Dictionary<ClassifierRunAction, int>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return counts;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return counts;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ClassifierRunPolicy.ActionFromWire(property.Name) is { } action
                    && property.Value.TryGetInt32(out var count) && count > 0)
                {
                    counts[action] = count;
                }
            }
        }
        catch (JsonException)
        {
            // A count the classifier wrote badly is a run with no counts, not a page that fails.
        }

        return counts;
    }

    private static (List<ClassifierRunItemView> Items, bool Cut) Items(JsonElement entity)
    {
        var json = Text(entity, "items");
        if (string.IsNullOrWhiteSpace(json))
        {
            return ([], false);
        }

        var items = new List<ClassifierRunItemView>();
        var cut = false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], true);
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!Guid.TryParse(Text(item, "messageId"), out var messageId)
                    || ClassifierRunPolicy.ActionFromWire(Text(item, "action")) is not { } action)
                {
                    cut = true;
                    continue;
                }

                items.Add(new(
                    new(
                        messageId,
                        action,
                        Text(item, "category"),
                        Guid.TryParse(Text(item, "caseId"), out var caseId) ? caseId : null,
                        Text(item, "caseReference"),
                        Text(item, "reason"),
                        Text(item, "refusal"),
                        Time(item, "at")),
                    Text(item, "subject"),
                    Text(item, "sender"),
                    MessageIsHere: false));
            }
        }
        catch (JsonException)
        {
            return ([], true);
        }

        return (items, cut);
    }

    private static string Day(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string? Text(JsonElement entity, string name) =>
        entity.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Boolean(JsonElement entity, string name) =>
        entity.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? Time(JsonElement entity, string name) =>
        DateTimeOffset.TryParse(
            Text(entity, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var time)
            ? time
            : null;

    // A JavaScript number is kept as an Int32 when it is whole and a Double
    // when it is not; a large one arrives as a string.
    private static decimal? Decimal(JsonElement entity, string name)
    {
        if (!entity.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static int? Int(JsonElement entity, string name) =>
        Decimal(entity, name) is { } number
        && number == decimal.Truncate(number)
        && number >= int.MinValue
        && number <= int.MaxValue
            ? (int)number
            : null;
}

/// <summary>
/// The hosted test instance's read of the live classifier's runs
/// (<c>Features:LiveClassifierRuns</c>; R1319), a DevelopmentOffline opt-in
/// like the others: the Web refuses it in Production, and a missing endpoint
/// or Graph key fails the start naming it.
/// </summary>
public static class LiveClassifierRuns
{
    public const string FeatureKey = "Features:LiveClassifierRuns";
    public const string TableEndpointKey = "ClassifierRuns:TableEndpoint";

    /// <summary>A page waits this long for the live runs before it shows them as unavailable.</summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddLiveClassifierRuns(this IServiceCollection services, Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(read);
        var endpoint = read(TableEndpointKey);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"{TableEndpointKey} is required for {FeatureKey}.");
        }

        if (!Uri.TryCreate(endpoint.EndsWith('/') ? endpoint : endpoint + "/", UriKind.Absolute, out var tableEndpoint)
            || tableEndpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{TableEndpointKey} must be an absolute HTTPS URI.");
        }

        // Handed to the reader alone: no other client picks up this credential.
        var credential = LocalGraphCredential.Create(read, FeatureKey);
        services.AddHttpClient<IProductionClassifierRuns, TableProductionClassifierRuns>(
            (HttpClient httpClient, IServiceProvider _) =>
            {
                httpClient.Timeout = ReadTimeout;
                return new TableProductionClassifierRuns(httpClient, credential, tableEndpoint);
            });
        return services;
    }
}
