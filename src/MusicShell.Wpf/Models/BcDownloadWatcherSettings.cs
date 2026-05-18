namespace MusicShell.Models;

public sealed class BcDownloadWatcherSettings
{
    public bool IsEnabled { get; set; }

    public string WatchFolder { get; set; } = string.Empty;

    public string FileNameFilters { get; set; } = string.Empty;

    public bool ArchiveAfterPrint { get; set; } = true;
}
