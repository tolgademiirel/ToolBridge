using System;
using System.IO;
using System.Linq;
using MusicShell.Infrastructure;

namespace MusicShell.Services;

public static class TransferStagingCleanupService
{
    public static string IncomingTransferStagingRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "incoming-staging");

    public static void CleanupOldIncomingStagingFolders(TimeSpan maxAge)
    {
        try
        {
            if (maxAge <= TimeSpan.Zero)
            {
                maxAge = TimeSpan.FromHours(24);
            }

            if (!Directory.Exists(IncomingTransferStagingRoot))
            {
                return;
            }

            var thresholdUtc = DateTime.UtcNow.Subtract(maxAge);
            foreach (var folder in Directory.EnumerateDirectories(IncomingTransferStagingRoot, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new DirectoryInfo(folder);
                    var newestWriteUtc = GetNewestWriteTimeUtc(info);
                    if (newestWriteUtc < thresholdUtc)
                    {
                        CleanupStagingFolder(folder);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogException($"Staging klasörü temizlenemedi: {folder}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Gelen transfer staging temizliği başarısız", ex);
        }
    }

    public static void CleanupStagingFolder(string? stagingFolder)
    {
        if (string.IsNullOrWhiteSpace(stagingFolder) || !IsPathInsideDirectory(stagingFolder, IncomingTransferStagingRoot))
        {
            return;
        }

        try
        {
            if (Directory.Exists(stagingFolder))
            {
                Directory.Delete(stagingFolder, recursive: true);
                AppLogger.Log($"Gelen transfer staging klasörü temizlendi: {stagingFolder}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException($"Gelen transfer staging klasörü silinemedi: {stagingFolder}", ex);
        }
    }

    private static DateTime GetNewestWriteTimeUtc(DirectoryInfo folder)
    {
        var newest = folder.LastWriteTimeUtc;

        try
        {
            foreach (var file in folder.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (file.LastWriteTimeUtc > newest)
                {
                    newest = file.LastWriteTimeUtc;
                }
            }

            foreach (var directory in folder.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                if (directory.LastWriteTimeUtc > newest)
                {
                    newest = directory.LastWriteTimeUtc;
                }
            }
        }
        catch
        {
            // Klasör içeriği okunamazsa üst klasör zamanı ile karar verilir.
        }

        return newest;
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
