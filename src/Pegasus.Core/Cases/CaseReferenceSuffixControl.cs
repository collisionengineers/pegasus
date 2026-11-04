using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>
/// The Administrator's temporary switch that ends every newly minted Case/PO
/// with <see cref="CaseReferenceFormat.ReferenceSuffix"/>, so a Pegasus
/// reference never collides with an old-system case number. It ships on;
/// switching it off mints without the suffix and leaves every Case that
/// already carries it as it is (operator, 4 November 2026; R656).
/// </summary>
public interface ICaseReferenceSuffixControl
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);

    Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken);
}
