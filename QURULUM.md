# WA Export — Windows

## Tələblər
- Windows 10 (build 17763) və ya Windows 11
- Visual Studio 2022 — "Windows application development" workload
- .NET 8 SDK
- WebView2 Runtime (Windows 11-də quraşdırılmış gəlir)

## Qurulum addımları

1. Bu qovluğu Windows maşına köçürün
2. `WA Export.sln` faylını Visual Studio 2022-də açın
3. NuGet paketləri avtomatik yüklənəcək
4. **Build → Build Solution** (Ctrl+Shift+B)
5. **Debug → Start Without Debugging** (Ctrl+F5)

## Qeydlər
- App icon üçün `WA Export/Assets/AppIcon.ico` faylı əlavə edin
- İlk işə salındıqda Windows Defender xəbərdarlığı çıxa bilər — "More info → Run anyway" seçin
