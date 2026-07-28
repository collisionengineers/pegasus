using CollisionRenderer.Core;

namespace CollisionRenderer.Gui.Models;

/// <summary>
/// A document template surfaced in the gallery / design screen. Thin view wrapper over
/// <see cref="AuthoringTemplateDescriptor"/> so XAML can bind Name/Description directly.
/// </summary>
public sealed class TemplateItem
{
    public TemplateItem(AuthoringTemplateDescriptor descriptor)
    {
        Id = descriptor.Id;
        Name = descriptor.Name;
        Description = descriptor.Description;
        RenderTemplateId = descriptor.RenderTemplateId;
        Category = descriptor.Category;
        AttachmentPolicy = descriptor.AttachmentPolicy;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string RenderTemplateId { get; }

    public string Category { get; }

    public AttachmentPolicy AttachmentPolicy { get; }

    /// <summary>True when this template accepts image and/or PDF attachments.</summary>
    public bool SupportsAttachments => AttachmentPolicy.AllowsImages || AttachmentPolicy.AllowsPdfs;

    /// <summary>Short hint shown on the gallery card, e.g. "Supports images".</summary>
    public string AttachmentHint => (AttachmentPolicy.AllowsImages, AttachmentPolicy.AllowsPdfs) switch
    {
        (true, true) => "Supports images & PDFs",
        (true, false) => "Supports images",
        (false, true) => "Supports PDFs",
        _ => string.Empty,
    };

    public bool HasAttachmentHint => AttachmentHint.Length > 0;
}
