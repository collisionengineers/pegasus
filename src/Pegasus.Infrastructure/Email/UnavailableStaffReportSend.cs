using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Email;

public sealed class UnavailableStaffReportSend : IStaffReportSend
{
    public Task<StaffMailOperation> SendAsync(
        StaffReportSendCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<StaffMailOperation>(new InvalidOperationException(
            "Staff mail delivery is unavailable in the DevelopmentOffline runtime profile."));
    }
}
