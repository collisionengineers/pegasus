using System.IO;
using System.Runtime.InteropServices;
using CollisionRenderer.Gui.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CollisionRenderer.Gui;

/// <summary>
/// The application window. Hosts a Frame whose start page is the
/// <see cref="Pages.TemplateGalleryPage"/>. Sizes itself to the content layout, DPI-scaled.
/// </summary>
public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Unpackaged apps do not have a guaranteed working directory, so resolve the
        // window/taskbar icon from the deployed app folder rather than a relative path.
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        ResizeForLayout();

        RootFrame.Navigate(typeof(TemplateGalleryPage));
    }

    private void ResizeForLayout()
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1220 * scale), (int)(820 * scale)));
    }
}
