using Microsoft.AspNetCore.Mvc;

namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// The answer to a retained intake download or view while durable storage is
/// not confirmed: one 409 plain-text response for the asset, image and source
/// routes.
/// </summary>
internal static class IntakeCustodyUnavailableResult
{
    public static ContentResult Create() => new()
    {
        StatusCode = StatusCodes.Status409Conflict,
        ContentType = "text/plain; charset=utf-8",
        Content = "The retained file is not available until durable storage is confirmed. Refresh this record and try again."
    };
}
