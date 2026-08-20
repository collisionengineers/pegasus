using Pegasus.Core.Intake;

namespace Pegasus.EmailEvaluation.Desktop;

public sealed record EmailCategory(string Family, string Name)
{
    public string DisplayName => $"{Family} / {Name}";
}

/// <summary>
/// The reviewer-facing taxonomy, sourced from the same settled mailbox taxonomy
/// Pegasus.Core owns (<see cref="MailTaxonomy"/>) rather than a standalone copy —
/// one list per concept. Reply is context on its underlying category (ADR-0016)
/// and never becomes a third family here.
/// </summary>
public sealed class CategoryCatalog
{
    public CategoryCatalog(IReadOnlyList<EmailCategory> categories)
    {
        Categories = categories;
        if (Categories.Count != 12
            || Categories.Count(category => category.Family == "Received") != 8
            || Categories.Count(category => category.Family == "Sent") != 4)
        {
            throw new InvalidDataException("The email taxonomy must contain exactly eight Received and four Sent categories.");
        }
    }

    public IReadOnlyList<EmailCategory> Categories { get; }

    public static CategoryCatalog Load()
    {
        var categories = Enum.GetValues<ReceivedMailFamily>()
            .Select(family => new EmailCategory("Received", MailTaxonomy.CategoryName(family)))
            .Concat(Enum.GetValues<SentMailFamily>()
                .Select(family => new EmailCategory("Sent", MailTaxonomy.CategoryName(family))))
            .ToList();

        return new CategoryCatalog(categories);
    }

    public EmailCategory? Find(string family, string name) =>
        Categories.FirstOrDefault(category =>
            string.Equals(category.Family, family, StringComparison.OrdinalIgnoreCase)
            && string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));
}
