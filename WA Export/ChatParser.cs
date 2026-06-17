using System.Text.RegularExpressions;

namespace WAExport;

public static class ChatParser
{
    private const char LtrMark = '‎';
    private static readonly Regex LineRegex = new(@"^‎?\[(\d{2}\.\d{2}\.\d{2},? \d{2}:\d{2}:\d{2})\] ([^:]+): (.*)");
    private static readonly string[] DateFormats = ["dd.MM.yy HH:mm:ss", "dd.MM.yy, HH:mm:ss"];

    public static ParsedChat Parse(string text, string chatName)
    {
        var lines = text.Split('\n');
        var messages = new List<ChatMessage>();
        var seenSenders = new List<string>();

        DateTime pendingDate = default;
        string pendingShender = "";
        List<string>? pendingLines = null;

        void Flush()
        {
            if (pendingLines is null) return;
            var raw = string.Join("\n", pendingLines);
            var content = ParseContent(raw);
            if (content is not MessageContent.System)
            {
                if (!seenSenders.Contains(pendingShender))
                    seenSenders.Add(pendingShender);
            }
            messages.Add(new ChatMessage(pendingDate, pendingShender, content));
            pendingLines = null;
        }

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(trimmed)) continue;

            var match = LineRegex.Match(trimmed);
            if (match.Success)
            {
                Flush();
                var dateStr = match.Groups[1].Value;
                var sender  = match.Groups[2].Value;
                var msgRaw  = match.Groups[3].Value;

                if (DateTime.TryParseExact(dateStr, DateFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
                {
                    pendingDate    = date;
                    pendingShender = sender;
                    pendingLines   = [msgRaw];
                }
            }
            else if (pendingLines is not null)
            {
                var cont = trimmed.TrimStart(LtrMark);
                pendingLines.Add(cont);
            }
        }
        Flush();

        return new ParsedChat(messages, seenSenders, chatName);
    }

    private static MessageContent ParseContent(string raw)
    {
        foreach (var line in raw.Split('\n'))
            if (ExtractMediaFilename(line) is { } fn)
                return new MessageContent.Media(fn, GetMediaType(fn));

        var stripped = raw.TrimStart(LtrMark);
        if (raw.StartsWith(LtrMark))
            return new MessageContent.System(stripped);

        return new MessageContent.Text(raw);
    }

    private static string? ExtractMediaFilename(string line)
    {
        var s = line.TrimStart(LtrMark);
        var openIdx  = s.LastIndexOf('<');
        var closeIdx = s.LastIndexOf('>');
        if (openIdx < 0 || closeIdx < 0 || openIdx >= closeIdx) return null;

        var inner = s[(openIdx + 1)..closeIdx];
        var parts = inner.Split(' ');
        if (parts.Length < 2) return null;

        var filename = string.Join(" ", parts[..^1]);
        var ext = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(ext) || string.IsNullOrEmpty(filename)) return null;
        return filename;
    }

    private static MediaType GetMediaType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant().TrimStart('.');
        return ext switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "webp" or "heic" or "heif" => MediaType.Image,
            "mp4" or "mov" or "avi" or "m4v" or "3gp"                       => MediaType.Video,
            "mp3" or "m4a" or "aac" or "opus" or "ogg" or "wav" or "flac"   => MediaType.Audio,
            _                                                                  => MediaType.Document,
        };
    }
}
