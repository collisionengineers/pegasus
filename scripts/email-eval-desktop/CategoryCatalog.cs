using Pegasus.Core.Intake;

namespace Pegasus.EmailEvaluation.Desktop;

public sealed record EmailCategory(string Family, string Name)
{
    public string DisplayName => $"{Family} / {Name}";
}

/// <summary>
/// The reviewer-facing categories, sourced from the flat mail categories
/// Pegasus.Core owns (<see cref="MailCategories"/>) rather than a standalone copy —
/// one list per concept. Sent mail carries no category, so every one files under
/// Received; reply is context on its underlying category (ADR-0016), never a category.
/// </summary>
public sealed class CategoryCatalog
{
    public CategoryCatalog(IReadOnlyList<EmailCategory> categories)
    {
        Categories = categories;
        if (Categories.Count != MailCategories.All.Count
            || Categories.Any(category => category.Family != "Received"))
        {
            throw new InvalidDataException("The email taxonomy must contain exactly the Received mail categories.");
        }
    }

    public IReadOnlyList<EmailCategory> Categories { get; }

    public static CategoryCatalog Load()
    {
        var categories = MailCategories.All
            .Select(category => new EmailCategory("Received", MailCategories.Code(category)))
            .ToList();

        return new CategoryCatalog(categories);
    }

    public EmailCategory? Find(string family, string name) =>
        Categories.FirstOrDefault(category =>
            string.Equals(category.Family, family, StringComparison.OrdinalIgnoreCase)
            && string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));
}
