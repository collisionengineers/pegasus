using System.IO;
using System.Text.Json;

namespace CollisionRenderer.Gui.Services;

/// <summary>A document the user worked on recently, shown on the gallery.</summary>
public sealed record RecentDocument
{
    public required string AuthoringTemplateId { get; init; }
    public required string TemplateName { get; init; }
    public required string SavedUtc { get; init; }
    public string? DraftJson { get; init; }
}

/// <summary>
/// Lightweight per-user persistence for an unpackaged app (no ApplicationData identity).
/// Stores recent documents and per-template autosave drafts as JSON under
/// %APPDATA%\CollisionRenderer. All operations are best-effort and never throw to the UI.
/// </summary>
public sealed class DesktopStateService
{
    private const int MaxRecents = 10;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CollisionRenderer");

    private string RecentsPath => Path.Combine(_root, "recents.json");

    private string AutosaveDir => Path.Combine(_root, "autosave");

    public IReadOnlyList<RecentDocument> LoadRecents()
    {
        try
        {
            if (!File.Exists(RecentsPath))
            {
                return Array.Empty<RecentDocument>();
            }

            var list = JsonSerializer.Deserialize<List<RecentDocument>>(File.ReadAllText(RecentsPath), Json);
            return list ?? new List<RecentDocument>();
        }
        catch
        {
            return Array.Empty<RecentDocument>();
        }
    }

    public void AddRecent(string authoringTemplateId, string templateName, string draftJson, string nowUtc)
    {
        try
        {
            var recents = LoadRecents()
                .Where(r => !string.Equals(r.AuthoringTemplateId, authoringTemplateId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            recents.Insert(0, new RecentDocument
            {
                AuthoringTemplateId = authoringTemplateId,
                TemplateName = templateName,
                SavedUtc = nowUtc,
                DraftJson = draftJson,
            });

            Directory.CreateDirectory(_root);
            File.WriteAllText(RecentsPath, JsonSerializer.Serialize(recents.Take(MaxRecents).ToList(), Json));
        }
        catch
        {
            // best-effort persistence; ignore IO failures
        }
    }

    public void SaveAutosave(string authoringTemplateId, string draftJson)
    {
        try
        {
            Directory.CreateDirectory(AutosaveDir);
            File.WriteAllText(AutosavePath(authoringTemplateId), draftJson);
        }
        catch
        {
            // ignore
        }
    }

    public string? LoadAutosave(string authoringTemplateId)
    {
        try
        {
            var path = AutosavePath(authoringTemplateId);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public void ClearAutosave(string authoringTemplateId)
    {
        try
        {
            var path = AutosavePath(authoringTemplateId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private string AutosavePath(string authoringTemplateId)
    {
        var safe = string.Concat(authoringTemplateId.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
        return Path.Combine(AutosaveDir, $"{safe}.json");
    }
}
