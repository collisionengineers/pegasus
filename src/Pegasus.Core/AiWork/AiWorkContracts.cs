using Pegasus.Core.Identity;

namespace Pegasus.Core.AiWork;

/// <summary>
/// The Administrator-held Send to AI switch, mirroring the Automation client
/// kill switch: disabling refuses new AI jobs immediately with attributable
/// permanent history. An absent row means enabled.
/// </summary>
public interface ISendToAiControl
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);

    Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken);
}
