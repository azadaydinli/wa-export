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

        WebView.CoreWebView2.NewWindowRequested += (sender, e) =>
        {
            e.Handled = true;
            Windows.System.Launcher.LaunchUriAsync(new Uri(e.Uri));
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
