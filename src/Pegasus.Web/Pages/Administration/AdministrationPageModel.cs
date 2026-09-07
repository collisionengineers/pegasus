namespace Pegasus.Web.Pages.Administration;

public abstract class AdministrationPageModel : StaffPageModel
{
    protected static bool IsOperationKeyValid(string? value) =>
        Guid.TryParseExact(value, "N", out var operationId) && operationId != Guid.Empty;
}
