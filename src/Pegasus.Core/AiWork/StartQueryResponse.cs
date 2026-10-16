using Pegasus.Core.Identity;

namespace Pegasus.Core.AiWork;

/// <summary>
/// Draft reply with AI (FRD-27): the message must be a retained Query linked
/// to its Case, and to <paramref name="CaseId"/> when the caller names one.
/// </summary>
public sealed record StartQueryResponseCommand(
    ActionActor Actor,
    Guid RetainedMessageId,
    string OperationKey,
    Guid? CaseId = null);

/// <summary>
/// The one owner of starting a Query response job, called by the message
/// record, the Case's Queries row and the Automation Actor's create tool.
/// </summary>
public interface IStartQueryResponse
{
    Task<AiJobRecord> ExecuteAsync(StartQueryResponseCommand command, CancellationToken cancellationToken);
}

public sealed class StartQueryResponse(
    Intake.GetRetainedMail mail,
    ICreateAiJob create) : IStartQueryResponse
{
    public const string NotASource = "This message is not a linked post-report message.";

    public async Task<AiJobRecord> ExecuteAsync(
        StartQueryResponseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var detail = await mail.ExecuteAsync(command.Actor, command.RetainedMessageId, cancellationToken)
            ?? throw new KeyNotFoundException("The retained message was not found.");
        if (!AiJobPolicy.IsQueryResponseSource(detail)
            || detail.Summary.CaseId is not { } caseId
            || (command.CaseId is { } named && named != caseId))
        {
            throw new InvalidOperationException(NotASource);
        }

        // The instruction is the message the job answers; QueryResponseMessageId reads it back.
        return await create.ExecuteAsync(
            new(
                AiJobKind.QueryResponse,
                caseId,
                detail.Summary.CaseReference,
                command.RetainedMessageId.ToString("D"),
                null,
                command.Actor,
                command.OperationKey),
            cancellationToken);
    }
}
