using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MusicShell.Infrastructure;
using MusicShell.Models;

namespace MusicShell.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly string BcDownloadWatcherSettingsStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "bc-invoice-hotfolder-settings.json");

    private static readonly TimeSpan BcProcessedFileCooldown = TimeSpan.FromMinutes(10);

    private FileSystemWatcher? _bcDownloadWatcher;
    private readonly object _bcDownloadWatcherSync = new();
    private readonly HashSet<string> _bcQueuedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _bcRecentlyProcessedFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _isBcDownloadWatcherEnabled;
    private bool _bcArchiveAfterPrint = true;
    private string _bcWatchFolder = GetDefaultBcWatchFolder();
    private string _bcFileNameFilters = "*.pdf";
    private string _bcDownloadWatcherStatusMessage = "BC fatura hot folder pasif. Aktif hale getirip klasoru kontrol edin.";
    private Brush _bcDownloadWatcherStatusBrush = Solid("#777783");

    public ICommand BrowseBcWatchFolderCommand { get; private set; } = null!;
    public ICommand SaveBcDownloadWatcherSettingsCommand { get; private set; } = null!;
    public ICommand ToggleBcDownloadWatcherCommand { get; private set; } = null!;
    public ICommand OpenBcWatchFolderCommand { get; private set; } = null!;

    public bool IsBcDownloadWatcherEnabled
    {
        get => _isBcDownloadWatcherEnabled;
        set
        {
            if (SetProperty(ref _isBcDownloadWatcherEnabled, value))
            {
                SaveBcDownloadWatcherSettings();
                RestartBcDownloadWatcher();
                OnPropertyChanged(nameof(BcDownloadWatcherModeText));
            }
        }
    }

    public bool BcArchiveAfterPrint
    {
        get => _bcArchiveAfterPrint;
        set
        {
            if (SetProperty(ref _bcArchiveAfterPrint, value))
            {
                SaveBcDownloadWatcherSettings();
            }
        }
    }

    public string BcWatchFolder
    {
        get => _bcWatchFolder;
        set
        {
            if (SetProperty(ref _bcWatchFolder, value ?? string.Empty))
            {
                SaveBcDownloadWatcherSettings();
            }
        }
    }

    public string BcFileNameFilters
    {
        get => _bcFileNameFilters;
        set
        {
            if (SetProperty(ref _bcFileNameFilters, string.IsNullOrWhiteSpace(value) ? "*.pdf" : value))
            {
                SaveBcDownloadWatcherSettings();
            }
        }
    }

    public string BcDownloadWatcherStatusMessage
    {
        get => _bcDownloadWatcherStatusMessage;
        private set => SetProperty(ref _bcDownloadWatcherStatusMessage, value ?? string.Empty);
    }

    public Brush BcDownloadWatcherStatusBrush
    {
        get => _bcDownloadWatcherStatusBrush;
        private set => SetProperty(ref _bcDownloadWatcherStatusBrush, value);
    }

    public string BcDownloadWatcherModeText => IsBcDownloadWatcherEnabled ? "Aktif" : "Pasif";

    public string BcDownloadWatcherPrinterText
    {
        get
        {
            var printer = ResolveDefaultPrintPrinter();
            return printer is null
                ? "Yazici: secilmedi"
                : $"Yazici: {printer.QueueValue}";
        }
    }

    private void InitializeBcDownloadWatcher()
    {
        BrowseBcWatchFolderCommand = new RelayCommand(_ => BrowseBcWatchFolder());
        SaveBcDownloadWatcherSettingsCommand = new RelayCommand(_ => SaveAndRestartBcDownloadWatcher());
        ToggleBcDownloadWatcherCommand = new RelayCommand(_ => IsBcDownloadWatcherEnabled = !IsBcDownloadWatcherEnabled);
        OpenBcWatchFolderCommand = new RelayCommand(_ => OpenBcWatchFolder());

        LoadBcDownloadWatcherSettings();
        EnsureBcHotFolderExists();
        RestartBcDownloadWatcher();
    }

    private void BrowseBcWatchFolder()
    {
        var initialDirectory = Directory.Exists(BcWatchFolder)
            ? BcWatchFolder
            : GetDefaultBcWatchFolder();

        var dialog = new OpenFolderDialog
        {
            Title = "BC fatura hot folder klasorunu secin",
            Multiselect = false,
            InitialDirectory = initialDirectory
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            BcWatchFolder = dialog.FolderName;
            SaveAndRestartBcDownloadWatcher();
        }
    }

    private void OpenBcWatchFolder()
    {
        try
        {
            EnsureBcHotFolderExists();

            Process.Start(new ProcessStartInfo
            {
                FileName = BcWatchFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC fatura hot folder acilamadi", ex);
            SetBcDownloadWatcherStatus($"Klasor acilamadi: {ex.Message}", true);
        }
    }

    private void SaveAndRestartBcDownloadWatcher()
    {
        BcFileNameFilters = "*.pdf";
        SaveBcDownloadWatcherSettings();
        RestartBcDownloadWatcher();
    }

    private void LoadBcDownloadWatcherSettings()
    {
        try
        {
            if (!File.Exists(BcDownloadWatcherSettingsStorePath))
            {
                BcFileNameFilters = "*.pdf";
                SaveBcDownloadWatcherSettings();
                return;
            }

            var json = File.ReadAllText(BcDownloadWatcherSettingsStorePath);
            var settings = JsonSerializer.Deserialize<BcDownloadWatcherSettings>(json);
            if (settings is null)
            {
                return;
            }

            _isBcDownloadWatcherEnabled = settings.IsEnabled;
            _bcWatchFolder = string.IsNullOrWhiteSpace(settings.WatchFolder) ? GetDefaultBcWatchFolder() : settings.WatchFolder;
            _bcFileNameFilters = "*.pdf";
            _bcArchiveAfterPrint = settings.ArchiveAfterPrint;

            OnPropertyChanged(nameof(IsBcDownloadWatcherEnabled));
            OnPropertyChanged(nameof(BcWatchFolder));
            OnPropertyChanged(nameof(BcFileNameFilters));
            OnPropertyChanged(nameof(BcArchiveAfterPrint));
            OnPropertyChanged(nameof(BcDownloadWatcherModeText));
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC fatura hot folder ayarlari okunamadi", ex);
            SetBcDownloadWatcherStatus($"BC fatura ayarlari okunamadi: {ex.Message}", true);
        }
    }

    private void SaveBcDownloadWatcherSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BcDownloadWatcherSettingsStorePath) ?? string.Empty);
            var settings = new BcDownloadWatcherSettings
            {
                IsEnabled = IsBcDownloadWatcherEnabled,
                WatchFolder = BcWatchFolder,
                FileNameFilters = "*.pdf",
                ArchiveAfterPrint = BcArchiveAfterPrint
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BcDownloadWatcherSettingsStorePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC fatura hot folder ayarlari kaydedilemedi", ex);
        }
    }

    private void RestartBcDownloadWatcher()
    {
        DisposeBcDownloadWatcher();

        if (!IsBcDownloadWatcherEnabled)
        {
            SetBcDownloadWatcherStatus("BC fatura hot folder pasif.", false);
            return;
        }

        if (string.IsNullOrWhiteSpace(BcWatchFolder))
        {
            BcWatchFolder = GetDefaultBcWatchFolder();
        }

        try
        {
            EnsureBcHotFolderExists();

            _bcDownloadWatcher = new FileSystemWatcher(BcWatchFolder)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                Filter = "*.pdf"
            };

            _bcDownloadWatcher.Created += BcDownloadWatcher_FileDetected;
            _bcDownloadWatcher.Changed += BcDownloadWatcher_FileDetected;
            _bcDownloadWatcher.Renamed += BcDownloadWatcher_FileRenamed;
            _bcDownloadWatcher.Error += BcDownloadWatcher_Error;

            SetBcDownloadWatcherStatus($"Aktif. Bu klasore gelen PDF faturalar otomatik yazdirilir: {BcWatchFolder}", false);
            AppLogger.Log($"BC Invoice Hot Folder aktif edildi. Folder={BcWatchFolder}; Filter=*.pdf");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC fatura hot folder baslatilamadi", ex);
            SetBcDownloadWatcherStatus($"BC fatura izleme baslatilamadi: {ex.Message}", true);
        }
    }

    private void BcDownloadWatcher_FileDetected(object sender, FileSystemEventArgs e)
    {
        EnqueueBcDownloadedFile(e.FullPath);
    }

    private void BcDownloadWatcher_FileRenamed(object sender, RenamedEventArgs e)
    {
        EnqueueBcDownloadedFile(e.FullPath);
    }

    private void BcDownloadWatcher_Error(object sender, ErrorEventArgs e)
    {
        AppLogger.LogException("BC fatura hot folder hatasi", e.GetException());
        Application.Current.Dispatcher.BeginInvoke(new Action(() => SetBcDownloadWatcherStatus("BC fatura izleme hatasi olustu. Kaydet/Yeniden baslat ile tekrar deneyin.", true)));
    }

    private void EnqueueBcDownloadedFile(string filePath)
    {
        if (!IsBcDownloadWatcherEnabled || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!IsBcWatcherCandidate(filePath))
        {
            return;
        }

        lock (_bcDownloadWatcherSync)
        {
            CleanupBcRecentlyProcessedFiles();

            if (_bcQueuedFiles.Contains(filePath))
            {
                return;
            }

            if (_bcRecentlyProcessedFiles.TryGetValue(filePath, out var processedAt) && DateTime.Now - processedAt < BcProcessedFileCooldown)
            {
                return;
            }

            _bcQueuedFiles.Add(filePath);
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(async () => await ProcessBcDownloadedFileAsync(filePath)));
    }

    private async Task ProcessBcDownloadedFileAsync(string filePath)
    {
        try
        {
            if (!IsBcDownloadWatcherEnabled)
            {
                return;
            }

            var fileName = Path.GetFileName(filePath);
            SetBcDownloadWatcherStatus($"PDF fatura bekleniyor: {fileName}", false);

            if (!await WaitForBcFileReadyAsync(filePath, TimeSpan.FromMinutes(3)))
            {
                SetBcDownloadWatcherStatus($"Dosya hazir hale gelmedi: {fileName}", true);
                AppLogger.Log($"BC Invoice Hot Folder dosya hazir degil: {filePath}");
                return;
            }

            var printer = ResolveDefaultPrintPrinter();
            if (printer is null)
            {
                SetBcDownloadWatcherStatus("Varsayilan yazici bulunamadi. Ayarlar bolumunden yazici secin.", true);
                return;
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                SetBcDownloadWatcherStatus($"Dosya bulunamadi: {fileName}", true);
                return;
            }

            var uploadItem = UploadFileItem.FromFileInfo(fileInfo);
            var printerQueueName = printer.QueueValue;
            var job = CreateOperationJob("BC Fatura Otomatik Yazdirma", "Hot folder PDF fatura yazdirma", $"{fileName} -> {printerQueueName}");
            if (!await WaitForJobSlotAsync(job))
            {
                return;
            }

            IsPrinting = true;
            SetBcDownloadWatcherStatus($"PDF fatura yazdiriliyor: {fileName}", false);

            bool isPrinted;
            string printError;
            try
            {
                var printResult = await RunOnStaThreadAsync(() =>
                {
                    var ok = TryPrintFileSilently(filePath, printer, out var error);
                    return (ok, error);
                }, job.CancellationToken);

                isPrinted = printResult.Item1;
                printError = printResult.Item2;
            }
            catch (Exception ex)
            {
                isPrinted = false;
                printError = ex.Message;
            }
            finally
            {
                IsPrinting = false;
                ReleaseJobSlot();
                CommandManager.InvalidateRequerySuggested();
            }

            if (!isPrinted)
            {
                AddPrintHistory(uploadItem, printerQueueName, "Hata");
                var errorMessage = $"PDF fatura yazdirilamadi: {fileName}. {printError}";
                job.MarkError(errorMessage);
                SetBcDownloadWatcherStatus(errorMessage, true);
                AppLogger.Log(errorMessage);
                return;
            }

            AddPrintHistory(uploadItem, printerQueueName, "Yazdirildi");
            job.MarkCompleted($"PDF fatura yaziciya gonderildi: {fileName}");
            SetBcDownloadWatcherStatus($"PDF fatura yaziciya gonderildi: {fileName}", false);
            AppLogger.Log($"BC Invoice Hot Folder yazdirdi. File={filePath}; Printer={printerQueueName}; Size={fileInfo.Length}");

            MarkBcFileProcessed(filePath);

            if (BcArchiveAfterPrint)
            {
                TryArchiveBcPrintedFile(filePath);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC fatura dosyasi islenemedi", ex);
            SetBcDownloadWatcherStatus($"BC fatura dosyasi islenemedi: {ex.Message}", true);
        }
        finally
        {
            lock (_bcDownloadWatcherSync)
            {
                _bcQueuedFiles.Remove(filePath);
            }
        }
    }

    private async Task<bool> WaitForBcFileReadyAsync(string filePath, TimeSpan timeout)
    {
        var startedAt = DateTime.Now;
        long lastLength = -1;
        var stableCount = 0;

        while (DateTime.Now - startedAt < timeout)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    await Task.Delay(500);
                    continue;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 0 && fileInfo.Length == lastLength)
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    if (stream.Length == fileInfo.Length)
                    {
                        stableCount++;
                        if (stableCount >= 1)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    stableCount = 0;
                }

                lastLength = fileInfo.Length;
            }
            catch
            {
                stableCount = 0;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private bool IsBcWatcherCandidate(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".tmp" or ".download" or ".partial" or ".crdownload")
        {
            return false;
        }

        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var filters = (BcFileNameFilters ?? "*.pdf")
            .Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (filters.Length == 0)
        {
            return true;
        }

        return filters.Any(pattern => WildcardMatch(fileName, pattern));
    }

    private static bool WildcardMatch(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var regex = "^" + Regex.Escape(pattern.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private void TryArchiveBcPrintedFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var archiveRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ToolBridge",
                "bc-invoice-printed-archive",
                DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(archiveRoot);

            var targetPath = Path.Combine(archiveRoot, Path.GetFileName(filePath));
            targetPath = EnsureUniqueBcArchivePath(targetPath);
            File.Move(filePath, targetPath);
            AppLogger.Log($"BC Invoice Hot Folder arsivledi. Source={filePath}; Target={targetPath}");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC yazdirilan fatura arsivlenemedi", ex);
        }
    }

    private static string EnsureUniqueBcArchivePath(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var folder = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);

        for (var index = 1; index < 1000; index++)
        {
            var candidate = Path.Combine(folder, $"{name}-{index:000}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{name}-{Guid.NewGuid():N}{extension}");
    }

    private void MarkBcFileProcessed(string filePath)
    {
        lock (_bcDownloadWatcherSync)
        {
            _bcRecentlyProcessedFiles[filePath] = DateTime.Now;
            CleanupBcRecentlyProcessedFiles();
        }
    }

    private void CleanupBcRecentlyProcessedFiles()
    {
        var threshold = DateTime.Now - BcProcessedFileCooldown;
        foreach (var key in _bcRecentlyProcessedFiles.Where(item => item.Value < threshold).Select(item => item.Key).ToList())
        {
            _bcRecentlyProcessedFiles.Remove(key);
        }
    }

    private void SetBcDownloadWatcherStatus(string message, bool isError)
    {
        BcDownloadWatcherStatusMessage = message;
        BcDownloadWatcherStatusBrush = isError ? Solid("#EF4444") : Solid("#22C55E");
        OnPropertyChanged(nameof(BcDownloadWatcherPrinterText));
    }

    private void DisposeBcDownloadWatcher()
    {
        if (_bcDownloadWatcher is null)
        {
            return;
        }

        try
        {
            _bcDownloadWatcher.EnableRaisingEvents = false;
            _bcDownloadWatcher.Created -= BcDownloadWatcher_FileDetected;
            _bcDownloadWatcher.Changed -= BcDownloadWatcher_FileDetected;
            _bcDownloadWatcher.Renamed -= BcDownloadWatcher_FileRenamed;
            _bcDownloadWatcher.Error -= BcDownloadWatcher_Error;
            _bcDownloadWatcher.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.LogException("BC fatura hot folder kapatilamadi", ex);
        }
        finally
        {
            _bcDownloadWatcher = null;
        }
    }

    private void EnsureBcHotFolderExists()
    {
        if (string.IsNullOrWhiteSpace(BcWatchFolder))
        {
            BcWatchFolder = GetDefaultBcWatchFolder();
        }

        if (!Directory.Exists(BcWatchFolder))
        {
            Directory.CreateDirectory(BcWatchFolder);
        }
    }

    private static string GetDefaultBcWatchFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "ToolBridge", "BC-Faturalar");
    }
}
