namespace WAExport;

public static class TranscriptionService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static async Task<string?> TranscribeAsync(string audioPath, string apiKey, CancellationToken ct = default)
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
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whisper-1"), "model");
        form.Add(new StringContent("text"),      "response_format");
        // Azerbaijani hint — auto-detection still runs, but model prioritises az
        form.Add(new StringContent("Azərbaycan dilindədir."), "prompt");

        var bytes   = await File.ReadAllBytesAsync(audioPath, ct);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        // API doesn't accept .opus extension — send as .ogg (same container)
        var uploadName = Path.GetExtension(audioPath).ToLowerInvariant() == ".opus"
            ? Path.GetFileNameWithoutExtension(audioPath) + ".ogg"
            : Path.GetFileName(audioPath);
        form.Add(content, "file", uploadName);

        request.Content = form;

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"HTTP {(int)response.StatusCode}: {err}");
        }
        return (await response.Content.ReadAsStringAsync(ct)).Trim();
    }
}
