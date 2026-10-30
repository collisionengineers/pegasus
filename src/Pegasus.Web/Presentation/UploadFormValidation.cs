using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The checks a staff upload's files pass before anything is stored, and the
/// files as the grouped submission takes them. Upload and Create case's Files
/// panel are two surfaces of the one declared upload (FRD-18; operator,
/// 30 October 2026, R574), so they share these checks and their sentences.
/// </summary>
public static class UploadFormValidation
{
    /// <summary>The batch's count and total, and each file's emptiness and size.</summary>
    public static void CheckLimits(
        IReadOnlyList<IFormFile> upload,
        ModelStateDictionary modelState,
        string key)
    {
        if (upload.Count > IntakeEnvelopeLimits.MaximumBatchFileCount)
        {
            modelState.AddModelError(
                key,
                $"You selected {upload.Count} files. Submit {IntakeEnvelopeLimits.MaximumBatchFileCount} "
                + "or fewer at a time.");
        }
        if (upload.Sum(file => file.Length) > IntakeEnvelopeLimits.MaximumBatchFileContentLength)
        {
            modelState.AddModelError(
                key,
                $"The selected files exceed the {OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumBatchFileContentLength)} total upload limit.");
        }
        for (var index = 0; index < upload.Count; index++)
        {
            var file = upload[index];
            if (file.Length == 0)
            {
                modelState.AddModelError(
                    key,
                    upload.Count == 1 ? "That file is empty." : $"File {index + 1} is empty.");
            }
            else if (file.Length > IntakeEnvelopeLimits.MaximumContentLength)
            {
                modelState.AddModelError(
                    key,
                    $"File {index + 1} is {OperatorLabels.FileSize(file.Length)}. "
                    + $"Files must be {OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumContentLength)} or smaller.");
            }
        }
    }

    /// <summary>
    /// Each file as the grouped submission takes it: its header must match an
    /// accepted type, and on a declared upload it carries the type chosen for
    /// it by its place in the post. A file that fails adds its error and is
    /// left out, so the caller submits nothing while the model state is invalid.
    /// </summary>
    public static async Task<IReadOnlyList<StreamedGroupedIntakeFile>> ReadAsync(
        IReadOnlyList<IFormFile> upload,
        IFormCollection form,
        bool declared,
        string externalReceiptToken,
        string uploader,
        DateTimeOffset receivedAtUtc,
        ModelStateDictionary modelState,
        string key,
        CancellationToken cancellationToken)
    {
        var files = new List<StreamedGroupedIntakeFile>(upload.Count);
        for (var index = 0; index < upload.Count; index++)
        {
            var file = upload[index];
            var header = new byte[Math.Min(file.Length, 40)];
            await using (var stream = file.OpenReadStream())
            {
                await stream.ReadExactlyAsync(header, cancellationToken);
            }
            if (!IntakeUploadFilePolicy.IsAccepted(file.FileName, file.ContentType, header))
            {
                modelState.AddModelError(
                    key,
                    $"File {index + 1} has an unsupported type or its contents do not match the selected file type.");
                continue;
            }
            // The type chosen for this file on a declared upload, by the
            // file's place in the post (FRD-18): none posted files as Other;
            // a value the page never offered is a malformed post.
            DocumentSemanticRole? declaredType = null;
            if (declared
                && form.TryGetValue($"DocumentTypes[{index}]", out var posted)
                && !string.IsNullOrWhiteSpace(posted))
            {
                if (!TryParseUploadType(posted.ToString(), out var parsed))
                {
                    modelState.AddModelError(key, $"File {index + 1} has a type that is not offered.");
                    continue;
                }
                declaredType = parsed;
            }
            files.Add(new(
                index,
                new(
                    Path.GetFileName(file.FileName),
                    string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    file.Length,
                    _ => ValueTask.FromResult<Stream>(file.OpenReadStream()),
                    receivedAtUtc,
                    uploader,
                    new(IntakeSourceChannel.ManualUpload, externalReceiptToken),
                    DeclaredDocumentType: declaredType)));
        }
        return files;
    }

    /// <summary>A posted type by name only, and one the page offers (<see cref="DocumentTypePolicy.UploadChoices"/>); a number is not a type.</summary>
    private static bool TryParseUploadType(string posted, out DocumentSemanticRole type)
    {
        var parsed = Enum.TryParse<DocumentSemanticRole>(posted.Trim(), ignoreCase: true, out type)
            && !int.TryParse(posted, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && DocumentTypePolicy.UploadChoices.Contains(type);
        if (!parsed)
        {
            type = default;
        }
        return parsed;
    }
}
