using System.IO.Compression;

namespace WAExport;

public static class ZipHandler
{
    public static async Task ExtractAsync(string zipPath, string targetDir, IProgress<double>? progress = null)
    {
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var total = archive.Entries.Count;
            var done  = 0;
            foreach (var entry in archive.Entries)
            {
                var dest = Path.Combine(targetDir, entry.FullName);
                var dir  = Path.GetDirectoryName(dest)!;
                Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(entry.Name))
                    entry.ExtractToFile(dest, overwrite: true);
                progress?.Report((double)++done / total);
            }
        });
    }

    public static async Task CreateAsync(string sourceDir, IEnumerable<string> items, string outputPath)
    {
        await Task.Run(() =>
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
            foreach (var item in items)
            {
                var fullPath = Path.Combine(sourceDir, item);
                if (File.Exists(fullPath))
                {
                    archive.CreateEntryFromFile(fullPath, item, CompressionLevel.Optimal);
                }
                else if (Directory.Exists(fullPath))
                {
                    foreach (var file in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
                    {
                        var entryName = Path.GetRelativePath(sourceDir, file);
                        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                    }
                }
            }
        });
    }
}
