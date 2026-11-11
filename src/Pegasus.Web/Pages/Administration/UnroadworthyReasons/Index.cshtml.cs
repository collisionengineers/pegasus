using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Labels = Pegasus.Web.Presentation.OperatorLabels.UnroadworthyReasons;

namespace Pegasus.Web.Pages.Administration.UnroadworthyReasons;

/// <summary>
/// Maintains the unroadworthy reason bank: the wordings every Case offers an
/// Engineer (operator, 11 November 2026; R952 to R954). A Case copies the
/// wording it inserts, so rewording or removing one here never reaches a
/// reason already recorded.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IUnroadworthyReasonBankStore bank,
    ISaveUnroadworthyReason saveWording,
    RewordUnroadworthyReason rewordWording,
    RemoveUnroadworthyReason removeWording) : AdministrationPageModel
{
    /// <summary>The bank, in the order a Case offers it.</summary>
    public IReadOnlyList<UnroadworthyReason> Wordings { get; private set; } = [];

    /// <summary>The add row's typed wording, kept when the add is refused.</summary>
    public string CreateText { get; private set; } = string.Empty;

    /// <summary>The row whose typed wording a refused Save keeps.</summary>
    public Guid RowId { get; private set; }
    public string RowText { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _)) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostCreateAsync(string? text, CancellationToken cancellationToken)
    {
        CreateText = text ?? string.Empty;
        return RunAsync(
            async actor => await saveWording.ExecuteAsync(new(text ?? string.Empty, actor), cancellationToken) is null
                ? UnroadworthyReasonChange.AlreadyOffered
                : UnroadworthyReasonChange.Done,
            Labels.Added,
            cancellationToken);
    }

    /// <summary>The save form belongs to one row and carries the wording that row rendered.</summary>
    public Task<IActionResult> OnPostSaveAsync(
        Guid wordingId, string? renderedText, string? text, CancellationToken cancellationToken)
    {
        RowId = wordingId;
        RowText = text ?? string.Empty;
        return RunAsync(
            actor => rewordWording.ExecuteAsync(
                new(wordingId, renderedText ?? string.Empty, text ?? string.Empty, actor), cancellationToken),
            Labels.Saved,
            cancellationToken);
    }

    public Task<IActionResult> OnPostRemoveAsync(
        Guid wordingId, string? renderedText, CancellationToken cancellationToken) =>
        RunAsync(
            actor => removeWording.ExecuteAsync(
                new(wordingId, renderedText ?? string.Empty, actor), cancellationToken),
            Labels.Removed,
            cancellationToken);

    private async Task<IActionResult> RunAsync(
        Func<ActionActor, Task<UnroadworthyReasonChange>> operation,
        string confirmation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();

        try
        {
            switch (await operation(actor))
            {
                case UnroadworthyReasonChange.Done:
                    TempData["Confirmation"] = confirmation;
                    return RedirectToPage();
                case UnroadworthyReasonChange.AlreadyOffered:
                    ModelState.AddModelError(string.Empty, Labels.AlreadyOffered);
                    break;
                default:
                    // The row shown now is the current one, not the typed one.
                    RowId = Guid.Empty;
                    ModelState.AddModelError(string.Empty, Labels.Stale);
                    break;
            }
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(string.Empty, Labels.WordingRequired);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken) =>
        Wordings = await bank.ListAsync(cancellationToken);
}
