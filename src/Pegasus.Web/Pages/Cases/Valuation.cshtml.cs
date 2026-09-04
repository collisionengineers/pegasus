using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ValuationModel(
    ISaveValuation saveValuation,
    ILogger<ValuationModel> logger) : CaseMutationPageModel(logger)
{
    public Task<IActionResult> OnPostAddAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        ValuationSource source,
        DateOnly date,
        TimeOnly time,
        string? guideMonth,
        long mileage,
        decimal retailValue,
        decimal tradeValue,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "add_valuation",
            actor => saveValuation.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    "Valuation recorded.",
                    editLeaseToken,
                    new(
                        source,
                        date,
                        time,
                        mileage,
                        retailValue,
                        tradeValue,
                        ParseGuideMonth(guideMonth))),
                cancellationToken),
            "The valuation was recorded.");

    private static DateOnly? ParseGuideMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            throw new ArgumentException("The guide month is invalid.", nameof(value));
        }
        return new DateOnly(month.Year, month.Month, 1);
    }
}
