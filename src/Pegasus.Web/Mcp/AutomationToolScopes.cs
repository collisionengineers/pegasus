using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Numerics;
using System.Security.Claims;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The one declaration of the scope each Automation MCP tool's call requires
/// (ADR-0080 decision 3): <see cref="AutomationActorResolver"/> gates a call on
/// it and <see cref="AutomationToolListing"/> filters tools/list by it. The
/// three edit-lease tools are in the cases and the intake rows; their call
/// takes the scope of the record kind.
/// </summary>
internal static class AutomationToolScopes
{
    private static readonly (string Scope, string[] Tools)[] Rows =
    [
        (AutomationMcp.CasesScope,
        [
            "pegasus_case_search",
            "pegasus_case_get",
            "pegasus_case_update_details",
            "pegasus_case_note_add",
            "pegasus_vocabulary_get",
            "pegasus_directory_search",
            "pegasus_case_action",
            "pegasus_work_centre_dismiss",
            "pegasus_edit_begin",
            "pegasus_edit_renew",
            "pegasus_edit_end"
        ]),
        (AutomationMcp.IntakeScope,
        [
            "pegasus_intake_queue_list",
            "pegasus_intake_submit",
            "pegasus_intake_get",
            "pegasus_intake_action",
            "pegasus_case_create",
            "pegasus_unidentified_list",
            "pegasus_unidentified_get",
            "pegasus_unidentified_resolve",
            "pegasus_unidentified_reopen",
            "pegasus_unidentified_source_download",
            "pegasus_triage_list",
            "pegasus_triage_get",
            "pegasus_triage_source_download",
            "pegasus_triage_await_information",
            "pegasus_triage_record_finding",
            "pegasus_triage_response_evidence",
            "pegasus_triage_complete",
            "pegasus_triage_cancel",
            "pegasus_triage_reopen",
            "pegasus_triage_case_link",
            "pegasus_triage_assign",
            "pegasus_triage_note_add",
            "pegasus_edit_begin",
            "pegasus_edit_renew",
            "pegasus_edit_end"
        ]),
        (AutomationMcp.DocumentsScope,
        [
            "pegasus_document_add",
            "pegasus_document_download",
            "pegasus_document_download_batch",
            "pegasus_document_action",
            "pegasus_report_list",
            "pegasus_report_action",
            "pegasus_report_wording_get",
            "pegasus_report_wording_save",
            "pegasus_image_preparation_get",
            "pegasus_image_prepare"
        ]),
        (AutomationMcp.AssessmentScope,
        [
            "pegasus_valuation_list",
            "pegasus_valuation_save",
            "pegasus_valuation_get",
            "pegasus_estimate_list",
            "pegasus_estimate_get",
            "pegasus_estimate_save",
            "pegasus_estimate_act",
            "pegasus_estimate_import",
            "pegasus_assessment_get",
            "pegasus_assessment_update"
        ]),
        (AutomationMcp.MailScope,
        [
            "pegasus_mail_list",
            "pegasus_mail_get",
            "pegasus_mail_correct_classification",
            "pegasus_mail_action",
            "pegasus_mail_link_destinations",
            "pegasus_mail_link_case"
        ]),
        (AutomationMcp.JobsScope,
        [
            "pegasus_ai_job_list",
            "pegasus_ai_job_create",
            "pegasus_ai_job_transition",
            "pegasus_ai_job_complete_market_research"
        ]),
        (AutomationMcp.SendScope,
        [
            "pegasus_report_send",
            "pegasus_mail_send"
        ])
    ];

    // One bit per scope, in the order of AutomationMcp.Scopes.
    private static readonly FrozenDictionary<string, int> ScopeBits =
        AutomationMcp.Scopes.Select((scope, index) => (scope, bit: 1 << index))
            .ToFrozenDictionary(pair => pair.scope, pair => pair.bit, StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, int> ToolScopes = Rows
        .SelectMany(row => row.Tools, (row, tool) => (tool, bit: ScopeBits[row.Scope]))
        .GroupBy(pair => pair.tool, StringComparer.Ordinal)
        .ToFrozenDictionary(
            group => group.Key,
            group => group.Aggregate(0, (bits, pair) => bits | pair.bit),
            StringComparer.Ordinal);

    /// <summary>Every tool the table declares.</summary>
    internal static IEnumerable<string> Tools => ToolScopes.Keys;

    /// <summary>The one scope the tool's call requires.</summary>
    internal static string RequiredScope(string toolName) =>
        ToolScopes.TryGetValue(toolName, out var bits) && BitOperations.PopCount((uint)bits) == 1
            ? AutomationMcp.Scopes[BitOperations.TrailingZeroCount(bits)]
            : throw new InvalidOperationException($"No single scope is declared for the tool '{toolName}'.");

    /// <summary>The record kind's scope, when the tool's row lists it.</summary>
    internal static string RequiredScope(string toolName, string recordScope) =>
        ToolScopes.TryGetValue(toolName, out var bits)
        && ScopeBits.TryGetValue(recordScope, out var bit)
        && (bits & bit) != 0
            ? recordScope
            : throw new InvalidOperationException($"The tool '{toolName}' is not declared for '{recordScope}'.");

    /// <summary>The scopes the token holds, one bit each, read as the gate reads them.</summary>
    internal static int Held(ClaimsPrincipal? user)
    {
        var held = 0;
        if (user is null)
        {
            return held;
        }

        foreach (var (scope, bit) in ScopeBits)
        {
            if (user.HasScope(scope))
            {
                held |= bit;
            }
        }

        return held;
    }

    /// <summary>
    /// Whether a token holding <paramref name="held"/> lists the tool. A tool
    /// the table does not declare is never listed, as its call is refused.
    /// </summary>
    internal static bool IsListed(string toolName, int held) =>
        ToolScopes.TryGetValue(toolName, out var bits) && (bits & held) != 0;
}

/// <summary>
/// tools/list filtered by the token's scopes (ADR-0080 decision 3, Stage 1).
/// The list is disclosure: <see cref="AutomationActorResolver"/> still gates
/// every call. A filtered list is built once per scope set and protocol
/// version (the SDK shapes a tool's output schema by version) from the SDK's
/// full list, then served as built. It varies by token, so it is marked
/// private to the caller.
/// </summary>
internal sealed class AutomationToolListing
{
    private readonly ConcurrentDictionary<(int Held, string? ProtocolVersion), Tool[]> lists = new();

    public McpRequestHandler<ListToolsRequestParams, ListToolsResult> Filter(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next) =>
        async (request, cancellationToken) =>
        {
            // The SDK lists every registered tool on the first page; a later
            // page holds none of them.
            if (request.Params?.Cursor is not null)
            {
                return await next(request, cancellationToken);
            }

            (int Held, string? ProtocolVersion) key =
                (AutomationToolScopes.Held(request.User), request.Server.NegotiatedProtocolVersion);
            if (!lists.TryGetValue(key, out var tools))
            {
                var all = await next(request, cancellationToken);
                Tool[] listed = [.. all.Tools.Where(tool => AutomationToolScopes.IsListed(tool.Name, key.Held))];
                tools = lists.GetOrAdd(key, listed);
            }

            return new ListToolsResult { Tools = tools, CacheScope = CacheScope.Private };
        };
}
