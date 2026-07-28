using System.Collections.ObjectModel;
using CollisionRenderer.Core;
using CollisionRenderer.Gui.Models;
using CollisionRenderer.Gui.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollisionRenderer.Gui.ViewModels;

/// <summary>A category of templates, used to drive the grouped gallery GridView.</summary>
public sealed class TemplateGroup : List<TemplateItem>
{
    public TemplateGroup(string key, IEnumerable<TemplateItem> items) : base(items) => Key = key;

    public string Key { get; }
}

/// <summary>
/// Backs the start screen: the catalogue of templates grouped by category, a search
/// filter, and the list of recent documents. Selection happens here; the design screen
/// receives the chosen template by navigation parameter.
/// </summary>
public partial class TemplateGalleryViewModel : ObservableObject
{
    // Categories shown first-to-last; anything else falls in after these, alphabetically.
    private static readonly string[] CategoryOrder = { "General", "Valuation", "Reports", "Fee Notes" };

    private readonly DesktopStateService _state = new();
    private readonly List<TemplateItem> _all;

    public TemplateGalleryViewModel()
    {
        _all = CollisionRendererFactory.AuthoringCatalog.List()
            .Select(d => new TemplateItem(d))
            .ToList();

        Rebuild();
        ReloadRecents();
    }

    public ObservableCollection<TemplateGroup> Groups { get; } = new();

    public ObservableCollection<RecentDocument> Recents { get; } = new();

    public bool HasRecents => Recents.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoResults))]
    public partial string SearchText { get; set; } = string.Empty;

    public bool NoResults => Groups.Count == 0;

    partial void OnSearchTextChanged(string value) => Rebuild();

    public void ReloadRecents()
    {
        Recents.Clear();
        foreach (var recent in _state.LoadRecents())
        {
            Recents.Add(recent);
        }

        OnPropertyChanged(nameof(HasRecents));
    }

    private void Rebuild()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        Groups.Clear();
        foreach (var group in filtered
                     .GroupBy(t => t.Category)
                     .OrderBy(g => CategoryRank(g.Key))
                     .ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            Groups.Add(new TemplateGroup(group.Key, group.OrderBy(t => t.Name, StringComparer.Ordinal)));
        }

        OnPropertyChanged(nameof(NoResults));
    }

    private static int CategoryRank(string category)
    {
        var index = Array.IndexOf(CategoryOrder, category);
        return index < 0 ? CategoryOrder.Length : index;
    }
}
