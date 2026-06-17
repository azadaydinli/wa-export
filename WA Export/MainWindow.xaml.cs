using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Velopack;
using Velopack.Sources;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WAExport;

public sealed partial class MainWindow : Window
{
    private readonly ChatProcessor _proc = new();
    private bool _suppressDateEvents;
    private UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        SubscribeToProcessor();
        _ = CheckForUpdatesAsync();
    }

    // MARK: - Window setup

    private void ConfigureWindow()
    {
        var hwnd      = WindowNative.GetWindowHandle(this);
        var windowId  = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new SizeInt32(560, 580));
        appWindow.Title = "WA Export";

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable    = false;
            presenter.IsMaximizable  = false;
        }
    }

    // MARK: - Processor binding

    private void SubscribeToProcessor()
    {
        _proc.PropertyChanged += (_, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(ChatProcessor.HasChat):
                        UpdateChatVisibility();
                        break;
                    case nameof(ChatProcessor.ParsedChat):
                        UpdateChatInfo();
                        break;
                    case nameof(ChatProcessor.UseCustomDateRange):
                        DateRangeCard.Visibility = _proc.UseCustomDateRange ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case nameof(ChatProcessor.IsProcessing):
                        OpenFileButton.IsEnabled = !_proc.IsProcessing;
                        ProgressPanel.Visibility = _proc.IsProcessing ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case nameof(ChatProcessor.Progress):
                        ProgressBar.Value = _proc.Progress;
                        break;
                    case nameof(ChatProcessor.Status):
                        StatusText.Text = _proc.Status;
                        break;
                    case nameof(ChatProcessor.MyDisplayName):
                        if (MyNameBox.Text != _proc.MyDisplayName) MyNameBox.Text = _proc.MyDisplayName;
                        break;
                    case nameof(ChatProcessor.OtherDisplayName):
                        if (OtherNameBox.Text != _proc.OtherDisplayName) OtherNameBox.Text = _proc.OtherDisplayName;
                        break;
                    case nameof(ChatProcessor.MyPhone):
                        if (MyPhoneBox.Text != _proc.MyPhone) MyPhoneBox.Text = _proc.MyPhone;
                        break;
                    case nameof(ChatProcessor.OtherPhone):
                        if (OtherPhoneBox.Text != _proc.OtherPhone) OtherPhoneBox.Text = _proc.OtherPhone;
                        break;
                }
            });
        };
    }

    private void UpdateChatVisibility()
    {
        var vis = _proc.HasChat ? Visibility.Visible : Visibility.Collapsed;
        ChatInfoCard.Visibility = vis;
        PartiesCard.Visibility  = vis;
        ActionRow.Visibility    = vis;
    }

    private void UpdateChatInfo()
    {
        if (_proc.ParsedChat is not { } chat) return;

        ChatNameText.Text = chat.ChatName;
        var msgs  = chat.Messages.Count(m => m.Content is not MessageContent.System);
        var media = chat.Messages.Count(m => m.Content is MessageContent.Media);
        ChatStatsText.Text = $"{msgs} mesaj · {media} media";

        MyNameBox.Text    = _proc.MyDisplayName;
        OtherNameBox.Text = _proc.OtherDisplayName;

        _suppressDateEvents = true;
        if (_proc.ChatDateRange is { } range)
        {
            StartDatePicker.MinDate = range.Min;
            StartDatePicker.MaxDate = range.Max;
            EndDatePicker.MinDate   = range.Min;
            EndDatePicker.MaxDate   = range.Max;
            StartDatePicker.Date    = range.Min;
            EndDatePicker.Date      = range.Max;
        }
        _suppressDateEvents = false;

        MyNameBox.PlaceholderText    = _proc.MySenderRaw;
        OtherNameBox.PlaceholderText = _proc.OtherSenderRaw;
    }

    // MARK: - File actions

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".zip");
        picker.SuggestedStartLocation = PickerLocationId.Downloads;

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        await _proc.ImportZipAsync(file.Path, new Progress<double>(v =>
            DispatcherQueue.TryEnqueue(() => ProgressBar.Value = v)));
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_proc.ParsedChat is null) return;

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = $"WA Export - {_proc.ParsedChat.ChatName}";
        picker.FileTypeChoices.Add("ZIP Arxivi", new List<string> { ".zip" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        await _proc.ExportZipAsync(file.Path);
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        _proc.RegeneratePreview();
        if (_proc.PreviewHtmlPath is not { } path) return;
        var preview = new PreviewWindow(path);
        preview.Activate();
    }

    // MARK: - Date range toggle

    private void DateToggle_Click(object sender, RoutedEventArgs e)
    {
        var useRange = (sender as FrameworkElement)?.Tag?.ToString() == "range";
        AllPeriodToggle.IsChecked  = !useRange;
        DateRangeToggle.IsChecked  = useRange;
        _proc.UseCustomDateRange   = useRange;
    }

    // MARK: - Date pickers

    private void StartDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_suppressDateEvents || args.NewDate is not { } d) return;
        _proc.CustomStartDate = d.DateTime;
    }

    private void EndDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_suppressDateEvents || args.NewDate is not { } d) return;
        _proc.CustomEndDate = d.DateTime;
    }

    // MARK: - Text fields

    private void MyNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_proc.MyDisplayName != MyNameBox.Text)
        {
            _proc.MyDisplayName = MyNameBox.Text;
            _proc.RegeneratePreview();
        }
    }

    private void OtherNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_proc.OtherDisplayName != OtherNameBox.Text)
        {
            _proc.OtherDisplayName = OtherNameBox.Text;
            _proc.RegeneratePreview();
        }
    }

    private void MyPhoneBox_TextChanged(object sender, TextChangedEventArgs e)
        => _proc.MyPhone = MyPhoneBox.Text;

    private void OtherPhoneBox_TextChanged(object sender, TextChangedEventArgs e)
        => _proc.OtherPhone = OtherPhoneBox.Text;

    // MARK: - Swap

    private void SwapButton_Click(object sender, RoutedEventArgs e) => _proc.SwapSides();

    // MARK: - Auto-update

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _updateManager = new UpdateManager(
                new GithubSource("https://github.com/azadaydinli/wa-export-updates", null, false));
            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            if (_pendingUpdate is not null)
                UpdateInfoBar.IsOpen = true;
        }
        catch { /* Offline və ya xəta — laqeyd keç */ }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateManager is null || _pendingUpdate is null) return;

        UpdateButton.IsEnabled = false;
        UpdateButton.Content   = "Yüklənir…";

        try
        {
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate, p =>
                DispatcherQueue.TryEnqueue(() =>
                    UpdateButton.Content = $"Yüklənir… {p}%"));

            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch
        {
            UpdateButton.IsEnabled = true;
            UpdateButton.Content   = "Yenilə və yenidən başlat";
        }
    }
}
