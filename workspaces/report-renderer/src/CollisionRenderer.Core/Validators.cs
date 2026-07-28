using CollisionRenderer.Core.Models;

namespace CollisionRenderer.Core;

/// <summary>Schema/policy checks run before a payload is rendered.</summary>
public interface IPayloadValidator
{
    ValidationResult Validate(
        string templateId,
        object model,
        bool allowLocalFilePaths = true,
        IReadOnlySet<string>? trustedLocalFilePaths = null);
}

public sealed class PayloadValidator : IPayloadValidator
{
    private const long DefaultMaxAttachmentBytes = 15_000_000;

    public ValidationResult Validate(
        string templateId,
        object model,
        bool allowLocalFilePaths = true,
        IReadOnlySet<string>? trustedLocalFilePaths = null)
    {
        var r = new ValidationResult();

        switch (model)
        {
            case MarketValuationEvidenceDocument d:
                RequireSubject(d.Subject, r);
                if (string.IsNullOrWhiteSpace(d.AssessedRetailValue))
                {
                    r.Errors.Add("assessedRetailValue is required.");
                }

                if (d.Adverts.Count == 0)
                {
                    r.Warnings.Add("No comparable adverts supplied — the evidence table will be empty.");
                }

                if (string.IsNullOrWhiteSpace(d.Conclusion))
                {
                    r.Warnings.Add("No conclusion paragraph supplied.");
                }

                break;

            case AdvertEvidencePackDocument d:
                RequireSubject(d.Subject, r);
                if (d.Adverts.Count == 0)
                {
                    r.Errors.Add("An advert evidence pack needs at least one advert.");
                }

                foreach (var (advert, i) in d.Adverts.Select((a, i) => (a, i)))
                {
                    ValidateImagePath(advert.ScreenshotPath, $"adverts[{i}].screenshotPath", r, allowLocalFilePaths, trustedLocalFilePaths);
                    ValidatePdfPath(advert.CapturedPdfPath, $"adverts[{i}].capturedPdfPath", r, allowLocalFilePaths, trustedLocalFilePaths);
                }

                break;

            case FeeNoteDocument d:
                if (string.IsNullOrWhiteSpace(d.FeeNoteNumber))
                {
                    r.Errors.Add("feeNoteNumber is required.");
                }

                if (string.IsNullOrWhiteSpace(d.BillTo.Name))
                {
                    r.Errors.Add("billTo.name is required.");
                }

                if (d.Items.Count == 0)
                {
                    r.Errors.Add("A fee note needs at least one line item.");
                }

                if (string.IsNullOrWhiteSpace(d.VatNumber))
                {
                    r.Warnings.Add("No VAT number supplied — the fee-note footer expects one.");
                }

                break;

            case ExpertReportDocument d:
                // The blank letterhead is a minimal branded page — a heading and body
                // sections are optional, so those structural checks are relaxed for it.
                var isLetterhead = string.Equals(templateId, "blank-letterhead", StringComparison.OrdinalIgnoreCase);

                if (!isLetterhead && string.IsNullOrWhiteSpace(d.Title))
                {
                    r.Errors.Add("title is required.");
                }

                if (!isLetterhead && d.Sections.Count == 0)
                {
                    r.Errors.Add("An expert report needs at least one section.");
                }

                foreach (var (section, i) in d.Sections.Select((s, i) => (s, i)))
                {
                    foreach (var (block, j) in section.Blocks.Select((b, j) => (b, j)))
                    {
                        if (!KnownBlockTypes.Contains(block.Type))
                        {
                            r.Errors.Add(
                                $"sections[{i}].blocks[{j}] has unknown type '{block.Type}'. " +
                                $"Allowed: {string.Join(", ", KnownBlockTypes)}.");
                        }

                        if (string.Equals(block.Type, "mediarow", StringComparison.OrdinalIgnoreCase) &&
                            block.Media is not null)
                        {
                            foreach (var (media, k) in block.Media.Select((m, k) => (m, k)))
                            {
                                ValidateImagePath(media.ImagePath, $"sections[{i}].blocks[{j}].media[{k}].imagePath", r, allowLocalFilePaths, trustedLocalFilePaths);
                            }
                        }
                    }
                }

                ValidateImagePath(d.Signature?.CustomSignaturePath, "signature.customSignaturePath", r, allowLocalFilePaths, trustedLocalFilePaths);
                break;

            default:
                r.Errors.Add($"No validator registered for template '{templateId}'.");
                break;
        }

        return r;
    }

    private static readonly HashSet<string> KnownBlockTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "paragraph", "bullets", "datatable", "keyvalue", "evidencetable", "valuebox", "mediarow",
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp",
    };

    private static readonly HashSet<string> ImageDataPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/webp;base64,",
    };

    private static void RequireSubject(SubjectVehicle subject, ValidationResult r)
    {
        if (string.IsNullOrWhiteSpace(subject.Registration))
        {
            r.Errors.Add("subject.registration is required.");
        }

        if (string.IsNullOrWhiteSpace(subject.Make) && string.IsNullOrWhiteSpace(subject.VehicleDescription))
        {
            r.Warnings.Add("Subject vehicle has neither make/model nor a description.");
        }
    }

    private static void ValidateImagePath(
        string? value,
        string path,
        ValidationResult r,
        bool allowLocalFilePaths,
        IReadOnlySet<string>? trustedLocalFilePaths)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (ImageDataPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (IsRemoteUrl(value))
        {
            if (!allowLocalFilePaths)
            {
                r.Errors.Add($"{path} must be an uploaded file or a data: image URI; remote image URLs are not accepted here.");
            }
            else
            {
                r.Warnings.Add($"{path} is a remote image URL; local existence and size could not be checked before render.");
            }
            return;
        }

        if (!allowLocalFilePaths && trustedLocalFilePaths?.Contains(value) != true)
        {
            r.Errors.Add($"{path} must be an uploaded file or a data: image URI; raw local file paths are not accepted here.");
            return;
        }

        ValidateLocalFile(value, path, ImageExtensions, "PNG, JPEG or WebP image", r);
    }

    private static void ValidatePdfPath(
        string? value,
        string path,
        ValidationResult r,
        bool allowLocalFilePaths,
        IReadOnlySet<string>? trustedLocalFilePaths)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        const string pdfDataPrefix = "data:application/pdf;base64,";
        if (value.StartsWith(pdfDataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Reject malformed base64 here so render doesn't later fail with an uncaught
            // FormatException (which would surface as a 500 from the API render endpoints).
            var payload = value[pdfDataPrefix.Length..];
            if (payload.Length == 0 || !IsBase64(payload))
            {
                r.Errors.Add($"{path} is not valid base64 PDF data.");
            }

            return;
        }

        if (IsRemoteUrl(value))
        {
            r.Errors.Add($"{path} must be a local PDF path or data:application/pdf base64 value; remote PDFs cannot be appended.");
            return;
        }

        if (!allowLocalFilePaths && trustedLocalFilePaths?.Contains(value) != true)
        {
            r.Errors.Add($"{path} must be an uploaded file or a data:application/pdf base64 value; raw local file paths are not accepted here.");
            return;
        }

        ValidateLocalFile(value, path, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" }, "PDF", r);
    }

    private static void ValidateLocalFile(
        string value,
        string path,
        HashSet<string> allowedExtensions,
        string label,
        ValidationResult r)
    {
        if (!File.Exists(value))
        {
            r.Errors.Add($"{path} file was not found: {value}");
            return;
        }

        var ext = Path.GetExtension(value);
        if (!allowedExtensions.Contains(ext))
        {
            r.Errors.Add($"{path} must reference a {label}; got '{ext}'.");
            return;
        }

        var length = new FileInfo(value).Length;
        if (length > DefaultMaxAttachmentBytes)
        {
            r.Warnings.Add($"{path} is {length:N0} bytes, above the recommended {DefaultMaxAttachmentBytes:N0}-byte attachment limit.");
        }
    }

    private static bool IsRemoteUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static bool IsBase64(string value)
    {
        try
        {
            _ = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
