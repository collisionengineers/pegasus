using System.Text.RegularExpressions;

namespace Pegasus.EmailEvaluation.Desktop;

public sealed record EmailCategory(string Family, string Name)
{
    public string DisplayName => $"{Family} / {Name}";
}

public sealed class CategoryCatalog
{
    private static readonly Regex FamilyRegex = new(
        "^\\s*[12]\\.\\s+(Received|Sent)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CategoryRegex = new(
        "^\\s*-\\s+([^\\[]+?)(?:\\s*\\[.*)?\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    public static CategoryCatalog Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "docs", "reference", "CollisionSPikeCurrenttree.txt");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The retained email taxonomy could not be found.", path);
        }

        var categories = new List<EmailCategory>();
        string? family = null;
        var inCategorySection = false;
        foreach (var line in File.ReadLines(path))
        {
            var familyMatch = FamilyRegex.Match(line);
            if (familyMatch.Success)
            {
                family = familyMatch.Groups[1].Value;
                inCategorySection = true;
                continue;
            }

            if (line.TrimStart().StartsWith("3.", StringComparison.Ordinal))
            {
                inCategorySection = false;
                continue;
            }

            if (family is null || !inCategorySection)
            {
                continue;
            }

            var categoryMatch = CategoryRegex.Match(line);
            if (categoryMatch.Success)
            {
                var name = categoryMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    categories.Add(new(family, name));
                }
            }
        }

        return new CategoryCatalog(categories);
    }

    public EmailCategory? Find(string family, string name) =>
        Categories.FirstOrDefault(category =>
            string.Equals(category.Family, family, StringComparison.OrdinalIgnoreCase)
            && string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));
}
