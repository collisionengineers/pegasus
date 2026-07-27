using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace CollisionRenderer.Gui;

/// <summary>
/// Application entry point. Owns the single main window and exposes a few statics
/// (window, dispatcher, HWND, WindowId) used by file pickers and UI-thread marshalling.
/// </summary>
public partial class App : Application
{
    /// <summary>The main application window.</summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>The UI-thread dispatcher for marshalling Core callbacks back to the UI.</summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>Native window handle (HWND) for interop.</summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>WindowId used by the Windows App SDK storage pickers (unpackaged-safe).</summary>
    public static WindowId WindowId =>
        Win32Interop.GetWindowIdFromWindow(WindowHandle);

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
    }
}
