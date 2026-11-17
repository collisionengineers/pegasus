using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Intake.Unidentified;

/// <summary>
/// Dismiss on a registration reading of an Unidentified item, for the record
/// and the Automation Actor alike. A pending reading is dismissed only where
/// <see cref="UnidentifiedItemContext.OffersDismiss"/> offers it (R1092); a
/// reading already decided goes to the store, which answers the key that
/// decided it as done and refuses any other.
/// </summary>
public sealed class DismissRegistrationReading(IVrmSuggestionStore suggestions)
{
    private readonly IVrmSuggestionStore suggestions =
        suggestions ?? throw new ArgumentNullException(nameof(suggestions));

    public Task<ImageVrmSuggestion> ExecuteAsync(
        UnidentifiedItemContext context,
        Guid readingId,
        ActionActor actor,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        context.RequireReading(readingId);
        var reading = context.RegistrationReadings.First(candidate => candidate.Id == readingId);
        if (reading.Disposition == ImageVrmSuggestionDisposition.Pending && !context.OffersDismiss(reading))
        {
            throw new InvalidOperationException("Dismiss is not offered on this reading.");
        }

        return suggestions.SetDispositionAsync(
            new(readingId, ImageVrmSuggestionDisposition.Dismissed, actor, reason, operationKey),
            cancellationToken);
    }
}
