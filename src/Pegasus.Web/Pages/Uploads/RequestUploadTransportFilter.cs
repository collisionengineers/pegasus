using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Pages.Uploads;

/// <summary>Admits public upload bodies before antiforgery reads the form.</summary>
public sealed class RequestUploadTransportFilter(IGetRequestUpload getRequestUpload)
    : IAsyncAuthorizationFilter
{
    public const long MaximumMultipartOverheadBytes = 64 * 1024;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsPost(request.Method))
        {
            return;
        }

        var token = context.RouteData.Values["token"] as string ?? string.Empty;
        var policy = await getRequestUpload.ExecuteAsync(
            token, context.HttpContext.RequestAborted);
        if (policy is null)
        {
            context.Result = new NotFoundResult();
            return;
        }

        var maximumBodyBytes = checked(policy.MaximumFileBytes + MaximumMultipartOverheadBytes);
        if (request.ContentLength > maximumBodyBytes)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
            return;
        }

        var serverLimit = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (serverLimit is { IsReadOnly: true })
        {
            // Another component already consumed bytes before admission.
            context.Result = new BadRequestResult();
            return;
        }
        if (serverLimit is not null)
        {
            serverLimit.MaxRequestBodySize = maximumBodyBytes;
        }

        // Buffer the bounded body once: FormFeature uses offsets into it for
        // files instead of creating a separate buffer for each multipart file.
        // This also bounds actual bytes when Content-Length is absent or false,
        // and on a host without a native request-size feature (e.g. TestServer).
        context.HttpContext.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
        {
            BufferBody = true,
            BufferBodyLengthLimit = maximumBodyBytes,
            MultipartBodyLengthLimit = policy.MaximumFileBytes,
            MemoryBufferThreshold = 64 * 1024
        }));
        context.HttpContext.Features.Set(policy);
    }
}
