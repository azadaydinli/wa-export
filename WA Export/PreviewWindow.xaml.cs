using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;
using WinRT.Interop;

namespace WAExport;

public sealed partial class PreviewWindow : Window
{
    private readonly string _htmlPath;

    public PreviewWindow(string htmlPath)
    {
        _htmlPath = htmlPath;
        InitializeComponent();
        ConfigureWindow();
        WebView.NavigationCompleted += WebView_NavigationCompleted;
        _ = InitWebViewAsync();
    }

    private void ConfigureWindow()
    {
        var hwnd      = WindowNative.GetWindowHandle(this);
        var windowId  = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(820, 900));
        appWindow.Title = "Önizləmə";
    }

    private async Task InitWebViewAsync()
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "waexport.local",
            Path.GetDirectoryName(_htmlPath)!,
            CoreWebView2HostResourceAccessKind.Allow);
        WebView.CoreWebView2.Navigate($"https://waexport.local/{Path.GetFileName(_htmlPath)}");

        // Open external links in default browser
        WebView.CoreWebView2.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(e.Uri));
        };
    }

    private void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs e) { }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
