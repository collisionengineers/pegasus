using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum ValuationSource
{
    Glasses,
    Cazana,
    EngineersValue,
    AiMarketResearch,
    Brego,
    SuperCap,
    Cap,
}

/// <summary>
/// The one question asked of the valuation-source vocabulary. The enum above
/// is that vocabulary: persistence stores its member name and generates the
/// table's check constraint from it, so there is no second list. The
/// operator-facing name of a source belongs to the presentation layer, beside
/// every other source label.
/// </summary>
public static class ValuationSources
{
    public static bool IsSupported(ValuationSource source) => Enum.IsDefined(source);

    /// <summary>The published guides, one card each on the Case.</summary>
    public static bool IsGuide(ValuationSource source) =>
        source is ValuationSource.Glasses
            or ValuationSource.Brego
            or ValuationSource.SuperCap
            or ValuationSource.Cap
            or ValuationSource.Cazana;
}

/// <summary>
/// One recorded valuation. The operator-entered local date and time are
/// retained as entered, and are the order the Valuations table is read in.
/// <see cref="GuideMonth"/> is the month the guide figure was published,
/// which is a different fact from the day it was recorded here; it is held
/// as the first day of that month so two cards for the same month sort and
/// compare as one value.
/// An <see cref="ValuationSource.EngineersValue"/> row additionally writes
/// the <c>assessment.values.engineer</c> field, which stays the one
/// owner of the Engineer's Value the product consumes.
/// A guide source's card holds whatever staff entered or Get valuation brought
/// back, so any of its mileage, retail, trade and guide month may be absent
/// (operator, 23 September 2026); an Engineer's Value or AI market research
/// row always carries its figures.
/// </summary>
public sealed record ValuationDetails(
    ValuationSource Source,
    DateOnly Date,
    TimeOnly Time,
    long? Mileage,
    decimal? RetailValue,
    decimal? TradeValue,
    DateOnly? GuideMonth = null);

public sealed record CaseValuation(
    Guid ValuationId,
    Guid CaseId,
    ValuationDetails Details,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc,
    string? LastEditedBy = null,
    DateTimeOffset? LastEditedAtUtc = null);

public static class ValuationPolicy
{
    public const string PolicyKey = "case-valuation";
    public const int PolicyVersion = 1;

    /// <summary>A valuation source as the operator reads it, on the page and in the Case history alike.</summary>
    public static string SourceName(ValuationSource source) => source switch
    {
        ValuationSource.Glasses => "Glass's",
        ValuationSource.Cazana => "Cazana",
        ValuationSource.EngineersValue => "Engineer's Value",
        ValuationSource.AiMarketResearch => "AI market research",
        ValuationSource.Brego => "Brego",
        ValuationSource.SuperCap => "Super CAP",
        ValuationSource.Cap => "CAP",
        _ => source.ToString(),
    };

    public static ValuationDetails ValidateDetails(ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (!ValuationSources.IsSupported(details.Source))
        {
            throw new ArgumentOutOfRangeException(
                nameof(details),
                "The valuation source is not supported.");
        }
        if (details.Mileage < 0)
        {
            throw new ArgumentException("Mileage cannot be negative.", nameof(details));
        }
        if (details.GuideMonth is { Day: not 1 })
        {
            throw new ArgumentException(
                "The valuation guide month must be represented by the first day of the month.",
                nameof(details));
        }
        if (!ValuationSources.IsGuide(details.Source)
            && (details.Mileage is null || details.RetailValue is null || details.TradeValue is null))
        {
            throw new ArgumentException(
                "An Engineer's Value or market research valuation carries its mileage, retail and trade values.",
                nameof(details));
        }
        Money(details.RetailValue, "retail value");
        Money(details.TradeValue, "trade value");

        // An Engineer's Value row is the entry surface of
        // assessment.values.engineer, so a row that cannot be written to that
        // field is refused here rather than persisted and silently dropped.
        EngineersValueField(details);
        return details;
    }

    /// <summary>
    /// The sources staff may type in. Collision Engineers reads the Glass's,
    /// Brego, Super CAP, CAP and Cazana guides and records the figure by hand
    /// (v28 P8 and P13, 18 September 2026): none of them has a live provider
    /// here, and the guide is evidence rather than a call. AI market research
    /// is written only by the automation completion, so staff never record it.
    /// </summary>
    public static bool IsManuallyRecordable(ValuationSource source) =>
        source is ValuationSource.Glasses
            or ValuationSource.Brego
            or ValuationSource.SuperCap
            or ValuationSource.Cap
            or ValuationSource.Cazana
            or ValuationSource.EngineersValue;

    /// <summary>
    /// One guide source's card as the Case save records it (23 September
    /// 2026: the source cards have no Save of their own), with whatever of
    /// its boxes were entered. The Engineer's Value is adopted by a Case Save
    /// that changes the valuation calculation and AI market research is the
    /// automation's, so neither is a guide card.
    /// </summary>
    public static ValuationDetails ValidateGuideEntry(ActionActor actor, ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(details);
        RequireManuallyRecordableSource(details.Source);
        if (details.Source == ValuationSource.EngineersValue)
        {
            throw new InvalidOperationException(
                "The Engineer's Value is adopted by saving a changed valuation calculation, not recorded as a guide card.");
        }
        RequireActor(actor, details);
        return ValidateDetails(details);
    }

    private static void RequireManuallyRecordableSource(ValuationSource source)
    {
        if (!IsManuallyRecordable(source))
        {
            throw new InvalidOperationException(
                "This valuation source cannot be recorded as a guide card.");
        }
    }

    /// <summary>
    /// A valuation fetched or typed for the same source and guide month replaces
    /// the earlier card rather than sitting beside it (planning, 13 September): the
    /// guide publishes one figure per month, so two cards for one month would be
    /// two answers to one question. A card without a guide month replaces the
    /// source's card without one (operator, 23 September 2026: the Case save
    /// carries every card, so a card left without a month is still one card), and
    /// a different month is a new card.
    /// </summary>
    public static bool Replaces(ValuationDetails incoming, ValuationDetails existing)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(existing);
        return incoming.Source == existing.Source
            && incoming.GuideMonth == existing.GuideMonth;
    }

    /// <summary>The card <paramref name="incoming"/> replaces among <paramref name="existing"/>, if any.</summary>
    public static CaseValuation? FindReplaced(ValuationDetails incoming, IEnumerable<CaseValuation> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing.FirstOrDefault(valuation => Replaces(incoming, valuation.Details));
    }

    /// <summary>
    /// Whether a card the Case save carries says nothing new over the card it
    /// replaces: the same mileage, retail and trade. Such a card writes
    /// nothing, so an untouched card never re-stamps the Apply basis.
    /// </summary>
    public static bool IsUnchanged(ValuationDetails incoming, ValuationDetails replaced)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(replaced);
        return Replaces(incoming, replaced)
            && incoming.Mileage == replaced.Mileage
            && incoming.RetailValue == replaced.RetailValue
            && incoming.TradeValue == replaced.TradeValue;
    }

    public static ValuationDetails ValidateAutomationMarketResearch(ValuationDetails details)
    {
        details = ValidateDetails(details);
        if (details.Source != ValuationSource.AiMarketResearch)
        {
            throw new InvalidOperationException(
                "Automation may record only an AI market research valuation.");
        }

        return details;
    }

    /// <summary>
    /// Recording or correcting a valuation is ordinary casework. An
    /// Engineer's Value row carries the <c>assessment.values.engineer</c>
    /// professional finding, so every staff actor who records it passes that
    /// field's finding-authority rule.
    /// </summary>
    private static void RequireActor(ActionActor actor, ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new InvalidOperationException(
                "A guide card is recorded by a staff actor.");
        }
        if (details.Source == ValuationSource.EngineersValue)
        {
            AssessmentPolicy.RequireFindingAuthority(actor);
        }
    }

    /// <summary>
    /// The <c>assessment.values.engineer</c> value an Engineer's
    /// Value row carries: its retail figure, which is the pre-accident value
    /// a settlement is measured from (FRD-11 total-loss report). Null for
    /// every other source, which writes no assessment field.
    /// </summary>
    public static string? EngineersValueField(ValuationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return details.Source == ValuationSource.EngineersValue
            ? AssessmentPolicy.NormalizeFieldValue(
                AssessmentVocabulary.ValueEngineer,
                details.RetailValue!.Value.ToString(CultureInfo.InvariantCulture))
            : null;
    }

    private static void Money(decimal? value, string description)
    {
        if (value is { } amount && (amount < 0 || decimal.Round(amount, 2) != amount))
        {
            throw new ArgumentException(
                $"The {description} must be a non-negative amount with at most two decimal places.",
                nameof(value));
        }
    }
}

public interface IValuationStore
{
    Task<IReadOnlyList<CaseValuation>> ListForCaseAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken);
}

public interface IListCaseValuations
{
    Task<IReadOnlyList<CaseValuation>> ExecuteAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken);
}

public sealed class ListCaseValuations(IValuationStore store) : IListCaseValuations
{
    public Task<IReadOnlyList<CaseValuation>> ExecuteAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return store.ListForCaseAsync(caseId, work, cancellationToken);
    }
}
