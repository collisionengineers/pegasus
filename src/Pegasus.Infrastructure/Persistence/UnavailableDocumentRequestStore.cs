using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class UnavailableDocumentRequestStore :
    ICreateRequestUploadLink,
    IRevokeRequestUploadLink,
    IUploadToRequest,
    IGetRequestUpload
{

    public Task<CreateRequestUploadLinkResult> ExecuteAsync(
        CreateRequestUploadLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        throw new DocumentRequestUnavailableException();
    }

    public Task ExecuteAsync(
        RevokeRequestUploadLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        throw new DocumentRequestUnavailableException();
    }

    public Task<UploadToRequestResult> ExecuteAsync(
        UploadToRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Task.FromResult(
            new UploadToRequestResult(RequestUploadDecision.Unavailable, null, false));
    }

    public Task<FinalizeRequestUploadResult> FinalizeAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FinalizeRequestUploadResult(
            RequestUploadDecision.Unavailable,
            false));

    public Task<RequestUploadPublicView?> ExecuteAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<RequestUploadPublicView?>(null);
}
