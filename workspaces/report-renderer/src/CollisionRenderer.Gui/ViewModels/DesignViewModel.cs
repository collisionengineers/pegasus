using System.Collections.ObjectModel;
using CollisionRenderer.Core;
using CollisionRenderer.Gui.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollisionRenderer.Gui.ViewModels;

/// <summary>
/// Outcome of a render attempt, surfaced to the view so it can drive the preview,
/// status strip, and validation messaging without the view knowing Core internals.
/// </summary>
public enum RenderOutcomeKind
{
    Success,
    ValidationFailed,
    BrowserMissing,
    Failed,
}

/// <summary>Typed result of <see cref="DesignViewModel.RenderAsync"/>.</summary>
public sealed record RenderOutcome
{
    public required RenderOutcomeKind Kind { get; init; }
    public RenderResult? Result { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string? Message { get; init; }
}

/// <summary>
/// State and Core orchestration for the design screen. Holds the chosen template,
/// density presets, the working JSON payload, and the render pipeline. The view owns
/// pickers / WebView2 / dialogs; this view-model owns data and Core calls.
/// </summary>
public partial class DesignViewModel : ObservableObject
{
    private readonly IAuthoringTemplateCatalog _authoringCatalog = CollisionRendererFactory.AuthoringCatalog;

    public DesignViewModel()
    {
        foreach (var option in DensityOption.All)
        {
            DensityOptions.Add(option);
        }

        SelectedDensity = DensityOptions[0];
    }

    /// <summary>Point the screen at a specific authoring template chosen in the gallery.</summary>
    public void Initialize(string authoringTemplateId)
    {
        var descriptor = _authoringCatalog.Get(authoringTemplateId);
        SelectedTemplate = new TemplateItem(descriptor);
    }

    /// <summary>Auto / Normal / Compact / Ultra presets.</summary>
    public ObservableCollection<DensityOption> DensityOptions { get; } = new();

    [ObservableProperty]
    public partial TemplateItem? SelectedTemplate { get; set; }

    [ObservableProperty]
    public partial DensityOption? SelectedDensity { get; set; }

    /// <summary>The editable JSON payload shown in the editor and live preview.</summary>
    [ObservableProperty]
    public partial string PayloadJson { get; set; } = string.Empty;

    /// <summary>True while a render is in flight; gates the UI and shows the progress ring.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyMessage { get; set; } = string.Empty;

    /// <summary>Short status line shown in the bottom strip after a render.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Fill in the form — the preview updates as you type.";

    /// <summary>Whether a rendered PDF currently exists (enables Save As / Open).</summary>
    [ObservableProperty]
    public partial bool HasRendered { get; set; }

    public bool IsNotBusy => !IsBusy;

    /// <summary>The most recent successful render, kept for Save As / Open.</summary>
    public RenderResult? LastResult { get; private set; }

    /// <summary>Load the starter draft (placeholder prompts + example text) for the template.</summary>
    public void LoadStarter()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        PayloadJson = _authoringCatalog.GetStarterJson(SelectedTemplate.Id);
        StatusText = $"Started a {SelectedTemplate.Name} from the template — overwrite the highlighted prompts.";
    }

    /// <summary>Load the Core-owned empty draft for the selected authoring template.</summary>
    public void LoadBlank()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        PayloadJson = _authoringCatalog.GetBlankJson(SelectedTemplate.Id);
        StatusText = $"Started a blank {SelectedTemplate.Name} draft.";
    }


    /// <summary>
    /// Run the Core render pipeline off the calling context. Translates the exception
    /// contract (validation, missing Chromium, generic failure) into a typed outcome.
    /// </summary>
    public async Task<RenderOutcome> RenderAsync(CancellationToken ct = default)
    {
        if (SelectedTemplate is null)
        {
            return new RenderOutcome { Kind = RenderOutcomeKind.Failed, Message = "Select a document type first." };
        }

        var request = new RenderRequest
        {
            TemplateId = SelectedTemplate.RenderTemplateId,
            Json = PayloadJson ?? string.Empty,
            Options = (SelectedDensity ?? DensityOption.All[0]).ToOptions(),
        };

        try
        {
            await using var renderer = CollisionRendererFactory.CreateRenderer();
            var result = await renderer.RenderAsync(request, ct).ConfigureAwait(false);

            LastResult = result;
            return new RenderOutcome { Kind = RenderOutcomeKind.Success, Result = result };
        }
        catch (RenderValidationException ex)
        {
            return new RenderOutcome { Kind = RenderOutcomeKind.ValidationFailed, Errors = ex.Errors };
        }
        catch (InvalidOperationException ex) when (LooksLikeMissingBrowser(ex.Message))
        {
            return new RenderOutcome { Kind = RenderOutcomeKind.BrowserMissing, Message = ex.Message };
        }
        catch (Exception ex)
        {
            return new RenderOutcome { Kind = RenderOutcomeKind.Failed, Message = ex.Message };
        }
    }

    private static bool LooksLikeMissingBrowser(string message) =>
        message.Contains("Chromium", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Playwright", StringComparison.OrdinalIgnoreCase)
        || message.Contains("playwright install", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedTemplateChanged(TemplateItem? value)
    {
        // A new document type invalidates any previously rendered preview.
        HasRendered = false;
        LastResult = null;
    }

    partial void OnPayloadJsonChanged(string value)
    {
        // Any edit to the draft invalidates the last rendered PDF, so Save As / Open PDF
        // can't hand back a stale document from before the change.
        HasRendered = false;
        LastResult = null;
    }
}
