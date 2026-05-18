using System.IO;

namespace MusicShell.Models;

public sealed class UploadFileItem
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string SizeText { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;

    public static UploadFileItem FromFileInfo(FileInfo fileInfo)
    {
        return new UploadFileItem
        {
            FileName = fileInfo.Name,
            FullPath = fileInfo.FullName,
            SizeBytes = fileInfo.Length,
            SizeText = FormatSize(fileInfo.Length),
            Extension = fileInfo.Extension.TrimStart('.').ToUpperInvariant()
        };
    }

    public static string FormatSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.##} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0.##} KB";
        }

        return $"{bytes} B";
    }
}
