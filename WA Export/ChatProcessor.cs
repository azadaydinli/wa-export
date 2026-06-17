using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WAExport;

public class ChatProcessor : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // MARK: - Published state

    private ParsedChat? _parsedChat;
    public ParsedChat? ParsedChat { get => _parsedChat; private set { _parsedChat = value; Notify(); Notify(nameof(HasChat)); Notify(nameof(CanTranscribe)); } }

    public bool HasChat => _parsedChat is not null;

    private string _status = "";
    public string Status { get => _status; private set { _status = value; Notify(); } }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set { _errorMessage = value; Notify(); } }

    private bool _isProcessing;
    public bool IsProcessing { get => _isProcessing; private set { _isProcessing = value; Notify(); Notify(nameof(CanTranscribe)); } }

    private double _progress;
    public double Progress { get => _progress; private set { _progress = value; Notify(); } }

    private string? _previewHtmlPath;
    public string? PreviewHtmlPath { get => _previewHtmlPath; private set { _previewHtmlPath = value; Notify(); } }

    private bool _isWhatsAppBusiness;
    public bool IsWhatsAppBusiness { get => _isWhatsAppBusiness; private set { _isWhatsAppBusiness = value; Notify(); } }

    // MARK: - Transcription

    private readonly Dictionary<string, string> _transcriptions = new();
    public IReadOnlyDictionary<string, string> Transcriptions => _transcriptions;

    public bool CanTranscribe => HasChat && !IsProcessing;

    // MARK: - Identity

    private string _mySenderRaw = "";
    public string MySenderRaw { get => _mySenderRaw; private set { _mySenderRaw = value; Notify(); } }

    private string _myDisplayName = "";
    public string MyDisplayName { get => _myDisplayName; set { _myDisplayName = value; Notify(); } }

    private string _myPhone = "";
    public string MyPhone { get => _myPhone; set { _myPhone = value; Notify(); } }

    private string _otherDisplayName = "";
    public string OtherDisplayName { get => _otherDisplayName; set { _otherDisplayName = value; Notify(); } }

    private string _otherPhone = "";
    public string OtherPhone { get => _otherPhone; set { _otherPhone = value; Notify(); } }

    public string OtherSenderRaw =>
        _parsedChat?.Senders.FirstOrDefault(s => s != _mySenderRaw) ?? _otherDisplayName;

    // MARK: - Date range

    private bool _useCustomDateRange;
    public bool UseCustomDateRange
    {
        get => _useCustomDateRange;
        set { _useCustomDateRange = value; Notify(); if (!_suppressRegen) RegeneratePreview(); }
    }

    private DateTime _customStartDate = DateTime.Today;
    public DateTime CustomStartDate
    {
        get => _customStartDate;
        set { _customStartDate = value; Notify(); if (!_suppressRegen) RegeneratePreview(); }
    }

    private DateTime _customEndDate = DateTime.Today;
    public DateTime CustomEndDate
    {
        get => _customEndDate;
        set { _customEndDate = value; Notify(); if (!_suppressRegen) RegeneratePreview(); }
    }

    private bool _suppressRegen;
    private string? _extractedDir;

    // MARK: - Computed

    public (DateTime Min, DateTime Max)? ChatDateRange
    {
        get
        {
            if (_parsedChat is null) return null;
            var dates = _parsedChat.Messages
                .Where(m => m.Content is not MessageContent.System)
                .Select(m => m.Date).ToList();
            if (dates.Count == 0) return null;
            return (dates.Min(), dates.Max());
        }
    }

    public ParsedChat? FilteredChat
    {
        get
        {
            if (_parsedChat is null) return null;
            if (!_useCustomDateRange) return _parsedChat;

            var start = _customStartDate.Date;
            var end   = _customEndDate.Date.AddDays(1).AddSeconds(-1);
            var msgs  = _parsedChat.Messages.Where(m => m.Date >= start && m.Date <= end).ToList();
            return new ParsedChat(msgs, _parsedChat.Senders, _parsedChat.ChatName);
        }
    }

    // MARK: - Import

    public async Task ImportZipAsync(string zipPath, IProgress<double>? progress = null)
    {
        IsProcessing = true;
        Progress     = 0;
        ErrorMessage = null;
        PreviewHtmlPath = null;
        Status = "ZIP açılır…";
        _transcriptions.Clear();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"WAExport_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            var prog = progress ?? new Progress<double>(v => Progress = v);
            await ZipHandler.ExtractAsync(zipPath, tempDir, prog);
            Progress = 1;

            var chatFile =
                Directory.GetFiles(tempDir, "_chat.txt", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? Directory.GetFiles(tempDir, "*.txt", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => !Path.GetFileName(f).StartsWith("."));
            if (chatFile is null) throw new Exception("Chat faylı ZIP-də tapılmadı.");

            Status = "Mesajlar oxunur…";
            var text     = File.ReadAllText(chatFile, System.Text.Encoding.UTF8);
            var (name, isBiz) = DeriveChatDetails(zipPath);
            IsWhatsAppBusiness = isBiz;
            var chat = ChatParser.Parse(text, name);

            _extractedDir = tempDir;
            ParsedChat    = chat;

            // Auto-detect sides
            var contact = chat.Senders.FirstOrDefault(s =>
            {
                var sl = s.ToLowerInvariant();
                var cl = name.ToLowerInvariant();
                return sl == cl || sl.Contains(cl) || cl.Contains(sl);
            });

            if (contact is not null)
            {
                OtherDisplayName = contact;
                MySenderRaw      = chat.Senders.FirstOrDefault(s => s != contact) ?? chat.Senders.LastOrDefault() ?? "";
            }
            else
            {
                MySenderRaw      = chat.Senders.LastOrDefault() ?? "";
                OtherDisplayName = chat.Senders.FirstOrDefault() ?? name;
            }
            MyDisplayName = MySenderRaw;

            // Init date range
            var dates = chat.Messages
                .Where(m => m.Content is not MessageContent.System)
                .Select(m => m.Date).ToList();

            _suppressRegen = true;
            UseCustomDateRange = false;
            CustomStartDate = dates.Count > 0 ? dates.Min() : DateTime.Today;
            CustomEndDate   = dates.Count > 0 ? dates.Max() : DateTime.Today;
            _suppressRegen = false;

            GeneratePreviewHtml(chat, tempDir);

            var msgCount   = chat.Messages.Count(m => m.Content is not MessageContent.System);
            var mediaCount = chat.Messages.Count(m => m.Content is MessageContent.Media);
            Status = $"{msgCount} mesaj, {mediaCount} media faylı tapıldı.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = $"Xəta: {ex.Message}";
        }

        IsProcessing = false;
    }

    private static readonly HashSet<string> AudioExtensions = ["opus"];

    public async Task TranscribeAudioAsync()
    {
        if (_extractedDir is null || _parsedChat is null) return;

        // Collect from parsed messages
        var audioFiles = _parsedChat.Messages
            .Where(m => m.Content is MessageContent.Media { Type: MediaType.Audio })
            .Select(m => ((MessageContent.Media)m.Content).Filename)
            .ToHashSet();

        // Also scan directory directly (catches formats the parser may have missed)
        foreach (var file in Directory.GetFiles(_extractedDir))
        {
            var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (AudioExtensions.Contains(ext))
                audioFiles.Add(Path.GetFileName(file));
        }

        if (audioFiles.Count == 0) { Status = "Transkript ediləcək audio tapılmadı."; return; }

        var audioList = audioFiles.ToList();

        IsProcessing = true;
        ErrorMessage = null;
        var done = 0;

        foreach (var filename in audioList)
        {
            Status = $"Transkript edilir… {done + 1}/{audioList.Count}";
            Progress = (double)done / audioList.Count;

            var path = Path.Combine(_extractedDir, filename);
            if (File.Exists(path))
            {
                try
                {
                    var text = await TranscriptionService.TranscribeAsync(path);
                    if (text is not null) _transcriptions[filename] = text;
                }
                catch { }
            }
            Progress = (double)++done / audioList.Count;
        }

        IsProcessing = false;
        Status = $"✓ {_transcriptions.Count}/{audioList.Count} audio transkript edildi.";
        RegeneratePreview();
    }

    public void RegeneratePreview()
    {
        if (FilteredChat is { } chat && _extractedDir is not null)
            GeneratePreviewHtml(chat, _extractedDir);
    }

    private void GeneratePreviewHtml(ParsedChat chat, string dir)
    {
        var html = HTMLGenerator.Generate(
            chat, MySenderRaw, MyDisplayName, MyPhone,
            OtherDisplayName, OtherPhone, IsWhatsAppBusiness,
            mediaBasePath: "", mediaDir: dir, transcriptions: _transcriptions);

        var path = Path.Combine(dir, "_preview.html");
        File.WriteAllText(path, html, System.Text.Encoding.UTF8);
        PreviewHtmlPath = path;
    }

    public void SwapSides()
    {
        if (_parsedChat is null) return;
        var otherRaw = _parsedChat.Senders.FirstOrDefault(s => s != MySenderRaw) ?? "";
        MySenderRaw = otherRaw;
        (MyDisplayName, OtherDisplayName) = (OtherDisplayName, MyDisplayName);
        (MyPhone, OtherPhone)             = (OtherPhone, MyPhone);
        RegeneratePreview();
    }

    public async Task ExportToFolderAsync(string outputDir)
    {
        if (FilteredChat is not { } chat || _extractedDir is null) return;

        IsProcessing = true;
        ErrorMessage = null;
        Status = "HTML yaradılır…";

        try
        {
            await Task.Run(() =>
            {
                var mediaDir = Path.Combine(outputDir, "Media");
                Directory.CreateDirectory(outputDir);
                Directory.CreateDirectory(mediaDir);

                var referencedMedia = chat.Messages
                    .OfType<ChatMessage>()
                    .Where(m => m.Content is MessageContent.Media)
                    .Select(m => ((MessageContent.Media)m.Content).Filename)
                    .ToHashSet();

                var copied = 0;
                foreach (var file in Directory.GetFiles(_extractedDir))
                {
                    var fn = Path.GetFileName(file);
                    if (fn == "_chat.txt" || fn.StartsWith("_preview")) continue;
                    if (!referencedMedia.Contains(fn)) continue;
                    File.Copy(file, Path.Combine(mediaDir, fn), overwrite: true);
                    copied++;
                }

                var html     = HTMLGenerator.Generate(chat, MySenderRaw, MyDisplayName, MyPhone,
                    OtherDisplayName, OtherPhone, IsWhatsAppBusiness, mediaDir: mediaDir,
                    transcriptions: _transcriptions);
                var htmlPath = Path.Combine(outputDir, "WhatsApp.html");
                File.WriteAllText(htmlPath, html, System.Text.Encoding.UTF8);

                if (copied == 0)
                    Directory.Delete(mediaDir);

                Status = $"✓ Export tamamlandı! {copied} media faylı əlavə edildi.";
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = $"Xəta: {ex.Message}";
        }

        IsProcessing = false;
    }

    private static (string chatName, bool isWhatsAppBusiness) DeriveChatDetails(string zipPath)
    {
        var base_ = Path.GetFileNameWithoutExtension(zipPath);

        // Prefix format (iOS / desktop): "WhatsApp Chat - Name"
        foreach (var p in new[] { "WhatsApp Business Chat - ", "WhatsApp Business Söhbəti - ", "WhatsApp Business Sohbeti - " })
            if (base_.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return (base_[p.Length..], true);
        foreach (var p in new[] { "WhatsApp Chat - ", "WhatsApp Söhbəti - ", "WhatsApp Sohbeti - " })
            if (base_.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return (base_[p.Length..], false);

        // Suffix format (Android Azerbaijani): "Name ilə WhatsApp söhbəti"
        foreach (var s in new[] { " ilə WhatsApp Biznes söhbəti", " ilə WhatsApp biznes söhbəti" })
            if (base_.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return (base_[..^s.Length], true);
        foreach (var s in new[] { " ilə WhatsApp söhbəti", " ile WhatsApp sohbeti" })
            if (base_.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return (base_[..^s.Length], false);

        return (base_, base_.Contains("Business", StringComparison.OrdinalIgnoreCase));
    }
}
