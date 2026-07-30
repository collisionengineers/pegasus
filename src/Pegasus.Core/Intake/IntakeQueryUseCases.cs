using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public sealed class ListIntake(IIntakeReceiptQueries queries) : IListIntake
{
    private readonly IIntakeReceiptQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<IntakeListPage> ExecuteAsync(
        ListIntakeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.Page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The requested page is outside the supported range.");
        }
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The requested page size is outside the supported range.");
        }
        if (query.Decision is { } decision && !Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The intake decision is not recognized.");
        }

        var matches = await queries.ListAsync(query.Decision, cancellationToken);
        var items = matches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        return new(items, query.Page, query.PageSize, matches.Count);
    }
}

public sealed class GetIntake(IIntakeReceiptQueries queries) : IGetIntake
{
    private readonly IIntakeReceiptQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<IntakeReceipt?> ExecuteAsync(
        GetIntakeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "An intake receipt identifier is required.",
                nameof(query));
        }

        return queries.GetAsync(query.ReceiptId, cancellationToken);
    }
}
