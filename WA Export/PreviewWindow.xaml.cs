using Microsoft.UI;
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
        var windowId  = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        var scale = Content.XamlRoot?.RasterizationScale ?? 1.0;
        appWindow.Resize(new SizeInt32((int)(820 * scale), (int)(900 * scale)));
        appWindow.Title = "Önizləmə";
    }

    private async Task InitWebViewAsync()
    {
        try
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
        catch
        {
            WebView.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://go.microsoft.com/fwlink/p/?LinkId=2124703"));
    }
}
