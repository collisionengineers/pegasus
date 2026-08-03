using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// The reverse half of automatic pairing: images commonly arrive before the
/// instruction, so when a Case is accepted this hook checks every
/// unassociated Image intake against the new case under the accepted match
/// rules and associates each one for which the new case is the single
/// unambiguous eligible candidate. Advisory and non-blocking: acceptance
/// stands regardless of any pairing failure, and each intake's automatic
/// association still runs at most once (its operation key is
/// origin-receipt-scoped across both pairing directions).
/// </summary>
public interface IImageIntakeCasePairing
{
    Task PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken);
}

public sealed class ImageIntakeCasePairing(
    IImageIntakeQueries imageIntakeQueries,
    IImageIntakeCaseCandidates caseCandidates,
    IIntakeMutationStore intakeMutationStore,
    TimeProvider timeProvider) : IImageIntakeCasePairing
{
    public async Task PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return;
        }

        var unassociated = await imageIntakeQueries.ListAsync(
            associated: false,
            cancellationToken);
        foreach (var intake in unassociated)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var eligible = await caseCandidates.FindEligibleByRegistrationAsync(
                    intake.NormalizedVehicleRegistration,
                    cancellationToken);
                if (eligible.Count != 1 || eligible[0].CaseId != caseId)
                {
                    continue;
                }

                await intakeMutationStore.AutoLinkAsync(
                    new(
                        intake.OriginReceiptId,
                        caseId,
                        eligible[0].CaseVersion,
                        ActionActor.SystemWorker(ImageIntakeAutomation.ActorId),
                        $"image-intake-associate:{intake.OriginReceiptId:N}",
                        $"Automatic association: the newly accepted case {eligible[0].CaseReference} matches this Image intake's confirmed registration unambiguously."),
                    timeProvider.GetUtcNow(),
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // One intake failing to pair (version race, staff lease, an
                // already-consumed automatic association) never affects the
                // others or the acceptance itself.
            }
        }
    }
}
