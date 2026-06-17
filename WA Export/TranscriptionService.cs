namespace WAExport;

public static class TranscriptionService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private const string ApiKey = "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";

    public static async Task<string?> TranscribeAsync(string audioPath, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(audioPath).ToLowerInvariant().TrimStart('.');
        var mime = ext switch
        {
            "mp3"  => "audio/mpeg",
            "mp4"  => "audio/mp4",
            "m4a"  => "audio/mp4",
            "wav"  => "audio/wav",
            "ogg"  => "audio/ogg",
            "opus" => "audio/ogg",
            "webm" => "audio/webm",
            _      => "audio/mpeg"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        request.Headers.Add("Authorization", $"Bearer {ApiKey}");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whisper-1"), "model");
        form.Add(new StringContent("text"),      "response_format");
        // Azerbaijani hint — auto-detection still runs, but model prioritises az
        form.Add(new StringContent("Azərbaycan dilindədir."), "prompt");

        var bytes   = await File.ReadAllBytesAsync(audioPath, ct);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        form.Add(content, "file", Path.GetFileName(audioPath));

        request.Content = form;

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadAsStringAsync(ct)).Trim();
    }
}
