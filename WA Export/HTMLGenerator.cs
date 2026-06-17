using System.Net;
using System.Text;

namespace WAExport;

public static class HTMLGenerator
{
    public static string Generate(
        ParsedChat chat,
        string mySenderRaw,
        string myDisplayName,
        string myPhone,
        string otherDisplayName,
        string otherPhone,
        bool isWhatsAppBusiness,
        string mediaBasePath = "Media",
        string? mediaDir = null)
    {
        var dateFmt = "dd.MM.yyyy";
        var timeFmt = "HH:mm";

        var msgDates = chat.Messages
            .Where(m => m.Content is not MessageContent.System)
            .Select(m => m.Date)
            .ToList();

        string dateRangeStr = msgDates.Count > 0
            ? (msgDates.Min().ToString(dateFmt) == msgDates.Max().ToString(dateFmt)
                ? msgDates.Min().ToString(dateFmt)
                : $"{msgDates.Min().ToString(dateFmt)} – {msgDates.Max().ToString(dateFmt)}")
            : "—";

        var myLabel    = string.IsNullOrEmpty(myDisplayName) ? mySenderRaw : myDisplayName;
        var otherLabel = string.IsNullOrEmpty(otherDisplayName) ? chat.ChatName : otherDisplayName;
        var platform   = isWhatsAppBusiness ? "WhatsApp Business" : "WhatsApp";

        var otherPhoneRow = string.IsNullOrEmpty(otherPhone) ? "" : $"<div class=\"p-phone\">{H(otherPhone)}</div>";
        var myPhoneRow    = string.IsNullOrEmpty(myPhone)    ? "" : $"<div class=\"p-phone\">{H(myPhone)}</div>";

        var participantsBar = $"""
            <div class="participants-bar">
              <div class="participants-inner">
                <div class="participant left">
                  <div class="p-label">DİGƏR TƏRƏF</div>
                  <div class="p-name">{H(otherLabel)}</div>
                  {otherPhoneRow}
                </div>
                <div class="participant center">
                  <div class="p-label">TARİX ARALIĞI</div>
                  <div class="p-name">{H(dateRangeStr)}</div>
                </div>
                <div class="participant right">
                  <div class="p-label">BİRİNCİ TƏRƏF</div>
                  <div class="p-name">{H(myLabel)}</div>
                  {myPhoneRow}
                </div>
              </div>
            </div>
            """;

        var body = new StringBuilder();
        int? lastDay = null;

        foreach (var msg in chat.Messages)
        {
            if (msg.Content is MessageContent.Text { Value: "" }) continue;

            string contentHtml;
            switch (msg.Content)
            {
                case MessageContent.Text t:
                    contentHtml = $"<div class=\"text\">{H(t.Value).Replace("\n", "<br>")}</div>";
                    break;
                case MessageContent.Media m:
                    contentHtml = MediaHtml(m.Filename, m.Type, mediaBasePath, mediaDir);
                    break;
                case MessageContent.System s:
                    if (ParseCall(s.Value) is { } call)
                        contentHtml = CallBubbleHtml(call);
                    else
                        continue;
                    break;
                default:
                    continue;
            }

            var day = msg.Date.DayOfYear + msg.Date.Year * 400;
            if (day != lastDay)
            {
                lastDay = day;
                body.AppendLine($"<div class=\"date-sep\"><span>{msg.Date.ToString(dateFmt)}</span></div>");
            }

            var isMe = msg.Sender == mySenderRaw;
            var side = isMe ? "out" : "in";
            var time = msg.Date.ToString(timeFmt);

            body.AppendLine($"""
                <div class="row {side}">
                  <div class="bubble">
                    {contentHtml}
                    <div class="time">{time}</div>
                  </div>
                </div>
                """);
        }

        return $$"""
            <!DOCTYPE html>
            <html lang="tr">
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{H(chat.ChatName)}}</title>
            <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
            body { background: #e5ddd5; font-family: -apple-system, "Segoe UI", Arial, sans-serif; font-size: 14px; color: #111b21; }
            .sticky-top { position: sticky; top: 0; z-index: 10; box-shadow: 0 2px 6px rgba(0,0,0,.18); }
            header { background: #075e54; color: #fff; padding: 13px 20px; font-size: 17px; font-weight: 600; text-align: center; }
            .participants-bar { background: #fff; border-bottom: 1px solid #e9edef; }
            .participants-inner { display: flex; max-width: 780px; margin: 0 auto; }
            .participant { flex: 1; padding: 11px 18px; }
            .participant.left   { border-right: 1px solid #e9edef; }
            .participant.center { text-align: center; border-right: 1px solid #e9edef; }
            .participant.right  { text-align: right; }
            .p-label { font-size: 10px; font-weight: 600; color: #8696a0; text-transform: uppercase; letter-spacing: 0.6px; margin-bottom: 3px; }
            .p-name  { font-size: 14px; font-weight: 600; color: #111b21; }
            .p-phone { font-size: 12px; color: #667781; margin-top: 1px; }
            .container { max-width: 780px; margin: 0 auto; padding: 16px 10px 32px; }
            .date-sep { text-align: center; margin: 14px 0 8px; }
            .date-sep span { background: rgba(255,255,255,.85); color: #54656f; font-size: 12px; padding: 4px 12px; border-radius: 8px; }
            .row { display: flex; margin: 2px 0; }
            .row.in  { justify-content: flex-start; padding-right: 20%; }
            .row.out { justify-content: flex-end;   padding-left:  20%; }
            .bubble { padding: 6px 10px 5px; border-radius: 8px; word-break: break-word; max-width: 100%; }
            .in  .bubble { background: #fff; }
            .out .bubble { background: #d9fdd3; }
            .text { line-height: 1.45; white-space: pre-wrap; }
            .time { font-size: 11px; color: #8696a0; text-align: right; margin-top: 3px; }
            .media-img { display: block; width: 240px; height: auto; border-radius: 6px; cursor: pointer; margin-bottom: 2px; }
            audio { display: block; margin: 4px 0; width: 360px; }
            video { display: block; width: 240px; height: auto; border-radius: 6px; margin-bottom: 2px; }
            .doc-link { display: flex; align-items: center; gap: 10px; text-decoration: none; background: rgba(0,0,0,.05); border-radius: 8px; padding: 8px 10px; margin: 2px 0; }
            .doc-link:hover { background: rgba(0,0,0,.09); }
            .doc-icon { font-size: 28px; line-height: 1; }
            .doc-info { display: flex; flex-direction: column; min-width: 0; }
            .doc-name { font-size: 13px; font-weight: 500; color: #111b21; word-break: break-all; }
            .doc-ext  { font-size: 11px; color: #8696a0; margin-top: 2px; text-transform: uppercase; }
            .vcf-card { background: rgba(0,0,0,.04); border-radius: 8px; padding: 10px 12px; margin: 2px 0; min-width: 200px; display: table; }
            .vcf-row  { display: table-row; }
            .vcf-icon { display: table-cell; font-size: 15px; width: 24px; padding: 2px 8px 2px 0; vertical-align: middle; text-align: center; }
            .vcf-cell { display: table-cell; vertical-align: middle; padding: 2px 0; }
            .vcf-name  { font-size: 14px; font-weight: 600; color: #111b21; }
            .vcf-phone { font-size: 13px; color: #667781; }
            .call-card { display: flex; align-items: center; gap: 7px; padding: 2px 2px 3px; min-width: 160px; }
            .call-icon { width: 22px; height: 22px; border-radius: 6px; background: #1f2937; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
            .call-icon.missed { background: #e8003d; }
            .call-info-text { display: flex; flex-direction: column; }
            .call-title { font-weight: 700; font-size: 14px; color: #111b21; }
            .call-sub   { font-size: 12px; color: #667781; margin-top: 2px; }
            @page { margin: 0.8cm 0.8cm 0.8cm 2cm; }
            @media print {
              *, *::before, *::after { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
              body { background: #e5ddd5 !important; }
              .sticky-top { position: static !important; box-shadow: none; }
              header { background: #075e54 !important; color: #fff !important; }
              .participants-bar { background: #fff !important; border-bottom: 1px solid #e9edef !important; }
              .participants-inner { max-width: 100%; }
              .container { max-width: 100%; }
              .in  .bubble { background: #fff !important; }
              .out .bubble { background: #d9fdd3 !important; }
              .date-sep span { background: rgba(255,255,255,.85) !important; }
              .row    { page-break-inside: avoid; break-inside: avoid; }
              .bubble { page-break-inside: avoid; break-inside: avoid; }
              .date-sep { page-break-after: avoid; break-after: avoid; }
              audio { max-width: 280px; }
              video { max-width: 200px; }
            }
            </style>
            </head>
            <body>
            <div class="sticky-top">
              <header>{{H(platform)}}</header>
              {{participantsBar}}
            </div>
            <div class="container">
            {{body}}
            </div>
            <script>
            document.querySelectorAll('video').forEach(function(v){v.addEventListener('loadedmetadata',function(){v.currentTime=0.001;});});
            document.querySelectorAll('audio').forEach(function(a){a.addEventListener('play',function(){document.querySelectorAll('audio').forEach(function(o){if(o!==a)o.pause();});});});
            </script>
            </body>
            </html>
            """;
    }

    // MARK: - Media

    private static string MediaHtml(string filename, MediaType type, string mediaBasePath, string? mediaDir)
    {
        var path        = string.IsNullOrEmpty(mediaBasePath) ? filename : $"{mediaBasePath}/{filename}";
        var escaped     = H(path);
        var nameEscaped = H(filename);

        return type switch
        {
            MediaType.Image => $"""
                <a href="{escaped}" target="_blank">
                  <img class="media-img" src="{escaped}" alt="{nameEscaped}" loading="lazy">
                </a>
                """,
            MediaType.Audio => $"""
                <audio controls preload="metadata">
                  <source src="{escaped}">
                </audio>
                """,
            MediaType.Video => $"""
                <video controls preload="auto">
                  <source src="{escaped}">
                </video>
                """,
            MediaType.Document => DocumentHtml(filename, escaped, nameEscaped, mediaDir),
            _ => ""
        };
    }

    private static string DocumentHtml(string filename, string escaped, string nameEscaped, string? mediaDir)
    {
        var extLower = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
        var extUpper = extLower.ToUpperInvariant();

        if (extLower == "vcf" && mediaDir is not null)
        {
            var vcfPath = Path.Combine(mediaDir, filename);
            if (File.Exists(vcfPath))
            {
                var text = File.ReadAllText(vcfPath);
                return VcfCardHtml(text);
            }
        }

        return $"""
            <a class="doc-link" href="{escaped}" target="_blank">
              <span class="doc-icon">📄</span>
              <span class="doc-info">
                <span class="doc-name">{nameEscaped}</span>
                <span class="doc-ext">{(string.IsNullOrEmpty(extUpper) ? "FAYL" : extUpper)}</span>
              </span>
            </a>
            """;
    }

    private static string VcfCardHtml(string text)
    {
        var name   = "";
        var phones = new List<string>();
        var emails = new List<string>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("FN:", StringComparison.OrdinalIgnoreCase))
                name = line[3..].Trim();
            else if (line.Contains("TEL", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                var val = line[(line.IndexOf(':') + 1)..].Trim();
                if (!string.IsNullOrEmpty(val)) phones.Add(val);
            }
            else if (line.Contains("EMAIL", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                var val = line[(line.IndexOf(':') + 1)..].Trim();
                if (!string.IsNullOrEmpty(val)) emails.Add(val);
            }
        }

        var sb = new StringBuilder();
        var displayName = string.IsNullOrEmpty(name) ? "Kontakt" : H(name);
        sb.Append($"<div class=\"vcf-row\"><span class=\"vcf-icon\">👤</span><span class=\"vcf-cell vcf-name\">{displayName}</span></div>");
        foreach (var p in phones)
            sb.Append($"<div class=\"vcf-row\"><span class=\"vcf-icon\">📞</span><span class=\"vcf-cell vcf-phone\">{H(p)}</span></div>");
        foreach (var e in emails)
            sb.Append($"<div class=\"vcf-row\"><span class=\"vcf-icon\">✉️</span><span class=\"vcf-cell vcf-phone\">{H(e)}</span></div>");

        return $"<div class=\"vcf-card\">{sb}</div>";
    }

    // MARK: - Call events

    private record CallInfo(bool IsVideo, bool IsMissed, string? Duration)
    {
        public string Label => (IsMissed, IsVideo) switch
        {
            (true,  true)  => "Cavabsız görüntülü zəng",
            (true,  false) => "Cavabsız səsli zəng",
            (false, true)  => "Görüntülü zəng",
            (false, false) => "Səsli zəng",
        };
    }

    private static CallInfo? ParseCall(string text)
    {
        var lower   = text.ToLowerInvariant();
        var isVideo = lower.Contains("görüntülü") || lower.Contains("video") || lower.Contains("видео");
        var isVoice = lower.Contains("sesli") || lower.Contains("səsli") || lower.Contains("voice")
                   || lower.Contains("аудио") || lower.Contains("дозвониться") || lower.Contains("call failed");
        var isMissed = lower.Contains("cevapsız") || lower.Contains("cavabsız") || lower.Contains("missed")
                    || lower.Contains("пропущен") || lower.Contains("нет ответа") || lower.Contains("call failed");
        var isCallKw = lower.Contains("arama") || lower.Contains("zəng") || lower.Contains("zang")
                    || lower.Contains("call")   || lower.Contains("вызов") || lower.Contains("звонок");

        if (!(isVideo || isVoice) || !(isCallKw || isMissed)) return null;

        string? duration = null;
        var durationMatch = System.Text.RegularExpressions.Regex.Match(text, @"\d+:\d{2}(?::\d{2})?");
        if (durationMatch.Success)
            duration = FormatDuration(durationMatch.Value);

        return new CallInfo(isVideo, isMissed, duration);
    }

    private static string FormatDuration(string raw)
    {
        if (raw.Contains(':'))
        {
            var parts = raw.Split(':').Select(p => int.TryParse(p.Trim(), out var n) ? n : 0).ToArray();
            return parts.Length switch
            {
                3 => BuildDur(parts[0], parts[1], parts[2]),
                2 => BuildDur(0, parts[0], parts[1]),
                _ => raw
            };
        }
        return raw;
    }

    private static string BuildDur(int h, int m, int s)
    {
        var parts = new List<string>();
        if (h > 0) parts.Add($"{h} saat");
        if (m > 0) parts.Add($"{m} dəqiqə");
        if (s > 0 || parts.Count == 0) parts.Add($"{s} saniyə");
        return string.Join(" ", parts);
    }

    private static string CallBubbleHtml(CallInfo info)
    {
        const string videoSvg = """<svg viewBox="0 0 24 24" width="11" height="11"><path d="M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11l-4 4z" fill="white"/></svg>""";
        const string phoneSvg = """<svg viewBox="0 0 24 24" width="11" height="11"><path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z" fill="white"/></svg>""";

        var icon      = info.IsVideo ? videoSvg : phoneSvg;
        var iconClass = info.IsMissed ? "call-icon missed" : "call-icon";
        var sub       = info.Duration is not null ? $"<div class=\"call-sub\">{H(info.Duration)}</div>" : "";

        return $"""
            <div class="call-card">
              <div class="{iconClass}">{icon}</div>
              <div class="call-info-text">
                <div class="call-title">{H(info.Label)}</div>
                {sub}
              </div>
            </div>
            """;
    }

    private static string H(string s) => WebUtility.HtmlEncode(s);
}
