using CollisionRenderer.Gui.Models;
using CollisionRenderer.Gui.Services;
using CollisionRenderer.Gui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CollisionRenderer.Gui.Pages;

/// <summary>
/// The start screen: a searchable, category-grouped gallery of document templates plus
/// recent documents. Selecting a card navigates to the <see cref="DesignPage"/>.
/// </summary>
public sealed partial class TemplateGalleryPage : Page
{
    public TemplateGalleryViewModel ViewModel { get; } = new();

    public TemplateGalleryPage()
    {
        InitializeComponent();
        GroupedTemplates.Source = ViewModel.Groups;
        TemplateGrid.ItemsSource = GroupedTemplates.View;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ReloadRecents();
    }

    private void OnTemplateClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TemplateItem item)
        {
            Frame.Navigate(typeof(DesignPage), new DesignNavArgs { AuthoringTemplateId = item.Id });
        }
    }

    private void OnRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentDocument recent })
        {
            Frame.Navigate(typeof(DesignPage), new DesignNavArgs
            {
                AuthoringTemplateId = recent.AuthoringTemplateId,
                RestoreJson = recent.DraftJson,
            });
        }
    }
}
