using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Printing;
using Microsoft.Win32;
using MusicShell.Infrastructure;
using MusicShell.Models;
using MusicShell.Services;

namespace MusicShell.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const long MaxUploadSizeBytes = 50L * 1024 * 1024;
    private const string PagePrint = "Yazdırma";
    private const string PageTransfer = "Transfer";
    private const string PageConvert = "Convert";
    private const string PageSettings = "Ayarlar";
    private const int TransferChunkSizeBytes = 4 * 1024 * 1024;
    private static readonly string PrinterStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "printers.json");
    private static readonly string PrintSettingsStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "print-settings.json");
    private static readonly string ConvertSettingsStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "convert-settings.json");
    private static readonly string TransferHistoryStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "transfer-history.json");
    private static readonly string IncomingTransferStagingRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "incoming-staging");

    private static readonly string[] DocumentConvertFormats =
    {
        "PDF", "DOC", "DOCX", "ODT", "RTF", "TXT", "HTML", "CSV", "XLS", "XLSX", "ODS", "PPT", "PPTX", "ODP"
    };

    private static readonly string[] ImageConvertFormats =
    {
        "JPG", "JPEG", "PNG", "BMP", "GIF", "TIF", "TIFF", "ICO", "WEBP", "SVG", "AVIF", "HEIC", "TGA"
    };

    private string _selectedPage = PagePrint;
    private string _uploadStatusMessage = "Dosya yüklemek için sürükleyip bırakın veya dosya seçin.";
    private Brush _uploadStatusBrush = Solid("#777783");
    private PrinterDeviceItem? _selectedRegisteredPrinter;
    private PrinterDeviceItem? _selectedPrintPrinter;
    private string _manualPrinterName = string.Empty;
    private string _manualPrinterIp = string.Empty;
    private string _manualPrinterQueue = string.Empty;
    private string _settingsStatusMessage = "Durum: Ready";
    private Brush _settingsStatusBrush = Solid("#777783");
    private Brush _activeAccentBrush = Solid("#FA233B");
    private Brush _activeAccentSoftBrush = Solid("#FFE8EC");
    private Brush _activeAccentBorderBrush = Solid("#FF9AA8");
    private Brush _activeAccentRingBrush = Solid("#80FA233B");
    private bool _isTransferReceiveEnabled = true;
    private bool _isDarkModeEnabled;
    private bool _isPrinting;
    private bool _isConverting;
    private bool _isMergingPdf;
    private string _selectedPrintColor = "Siyah / Beyaz";
    private string _selectedPrintSide = "Tek taraflı";
    private string _selectedPaperSize = "A4 - 21 cm x 29,7 cm";
    private string _selectedMarginProfile = "Normal";
    private string _selectedScaleMode = "Ölçeklendirme Yok";
    private string _selectedOrientation = "Dikey";
    private string _printCopyCount = "1";
    private string _printSplitLimit = "300";
    private string _printPageRange = string.Empty;
    private bool _isLoadingPrintSettings;
    private bool _isLoadingConvertSettings;
    private string _convertStatusMessage = "Dönüştürmek için dosya seçin ve personel varsayılan kayıt klasörünü seçin.";
    private Brush _convertStatusBrush = Solid("#777783");
    private string _pdfMergeStatusMessage = "PDF birleştirmek için en az 2 PDF dosyası seçin.";
    private Brush _pdfMergeStatusBrush = Solid("#777783");
    private string _pdfMergeOutputFileName = "Birlesik_PDF.pdf";
    private string _selectedConvertTargetFormat = "PDF";
    private bool _isConvertFormatPopupOpen;
    private bool _isConvertSideFormatPopupOpen;
    private string _convertFormatSearchText = string.Empty;
    private string _convertOutputFolder = string.Empty;
    private string _transferDownloadFolder = string.Empty;
    private string _convertTransferStatusMessage = string.Empty;
    private Brush _convertTransferStatusBrush = Solid("#777783");
    private OnlineUserItem? _selectedTransferRecipient;
    private string _leftOnlineUserSearchText = string.Empty;
    private string _transferOnlineUserSearchText = string.Empty;
    private string _convertOnlineUserSearchText = string.Empty;
    private string _transferStatusMessage = "Dosya göndermek için önce ağdaki personel listesinden alıcı seçin.";
    private Brush _transferStatusBrush = Solid("#777783");
    private PendingTransferItem? _selectedIncomingTransfer;
    private bool _isIncomingTransferModalOpen;
    private bool _isTransferInProgress;
    private readonly SemaphoreSlim _jobQueueGate = new(1, 1);
    private readonly Dictionary<string, OnlineUserItem> _onlineUsersByPresenceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _currentPresenceId = CreateLocalPresenceId();
    private readonly string _currentUserDisplayName = CreateLocalUserDisplayName();
    private readonly System.Threading.Timer _onlineUserCleanupTimer;
    private LanPresenceService? _presenceService;
    private LanFileTransferService? _fileTransferService;
    private static readonly TimeSpan OnlineUserTimeout = TimeSpan.FromSeconds(12);

    public MainViewModel()
    {
        PrimaryNavigation = new ObservableCollection<NavItem>
        {
            new()
            {
                Icon = "\uE749",
                Title = PagePrint,
                IconBrush = Solid("#F59E0B"),
                GlowBrush = RadialGlow("#72F59E0B", "#38D97706", "#00924500"),
                AccentBrush = Solid("#F59E0B"),
                AccentSoftBrush = Solid("#FFFBEB"),
                AccentBorderBrush = Solid("#FCD34D"),
                AccentRingBrush = Solid("#80F59E0B"),
                IsActive = true
            },
            new()
            {
                Icon = "\uE8AB",
                Title = PageTransfer,
                IconBrush = Solid("#3B82F6"),
                GlowBrush = RadialGlow("#723B82F6", "#382563EB", "#001D4ED8"),
                AccentBrush = Solid("#3B82F6"),
                AccentSoftBrush = Solid("#EFF6FF"),
                AccentBorderBrush = Solid("#93C5FD"),
                AccentRingBrush = Solid("#803B82F6")
            },
            new()
            {
                Icon = "\uE8B7",
                Title = PageConvert,
                IconBrush = Solid("#EF4444"),
                GlowBrush = RadialGlow("#72EF4444", "#38DC2626", "#00B91C1C"),
                AccentBrush = Solid("#EF4444"),
                AccentSoftBrush = Solid("#FEF2F2"),
                AccentBorderBrush = Solid("#FCA5A5"),
                AccentRingBrush = Solid("#80EF4444")
            },
            new()
            {
                Icon = "\uE713",
                Title = PageSettings,
                IconBrush = Solid("#22C55E"),
                GlowBrush = RadialGlow("#7222C55E", "#3816A34A", "#0015803D"),
                AccentBrush = Solid("#22C55E"),
                AccentSoftBrush = Solid("#F0FDF4"),
                AccentBorderBrush = Solid("#86EFAC"),
                AccentRingBrush = Solid("#8022C55E")
            }
        };

        ApplyActiveAccent(PrimaryNavigation.First());

        OnlineUsers = new ObservableCollection<OnlineUserItem>();
        AddLocalOnlineUser();
        FilteredOnlineUsers = new ObservableCollection<OnlineUserItem>();
        LeftFilteredOnlineUsers = new ObservableCollection<OnlineUserItem>();
        TransferFilteredOnlineUsers = new ObservableCollection<OnlineUserItem>();
        ConvertFilteredOnlineUsers = new ObservableCollection<OnlineUserItem>();
        ConvertEngineStatuses = new ObservableCollection<SystemStatusItem>();
        OperationJobs = new ObservableCollection<OperationJobItem>();
        OperationJobs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(OperationQueueCountText));
            OnPropertyChanged(nameof(OperationQueueEmptyVisibility));
            OnPropertyChanged(nameof(OperationQueueListVisibility));
        };
        RefreshFilteredOnlineUsers();
        RefreshConvertEngineStatuses();

        PrintColorOptions = new ObservableCollection<string> { "Renkli", "Siyah / Beyaz" };
        PrintSideOptions = new ObservableCollection<string> { "Tek taraflı", "Çift taraflı" };
        PaperSizeOptions = new ObservableCollection<string>
        {
            "A0 - 84,1 cm x 118,9 cm",
            "A1 - 59,4 cm x 84,1 cm",
            "A2 - 42 cm x 59,4 cm",
            "A3 - 29,7 cm x 42 cm",
            "A4 - 21 cm x 29,7 cm",
            "A5 - 14,8 cm x 21 cm",
            "A6 - 10,5 cm x 14,8 cm",
            "B1 - 72,8 cm x 103 cm",
            "B2 - 51,5 cm x 72,8 cm",
            "B3 - 36,4 cm x 51,5 cm",
            "B4 - 25,7 cm x 36,4 cm",
            "Tüm Kağıt Boyutları..."
        };
        MarginProfileOptions = new ObservableCollection<string>
        {
            "Son Özel Ayar",
            "Normal",
            "Geniş",
            "Dar",
            "Özel Kenar Boşlukları..."
        };
        ScaleModeOptions = new ObservableCollection<string>
        {
            "Ölçeklendirme Yok",
            "Sayfayı Bir Sayfaya Sığdır",
            "Tüm Sütunları Bir Sayfaya Sığdır",
            "Tüm Satırları Bir Sayfaya Sığdır",
            "Özel Ölçeklendirme Seçenekleri..."
        };
        OrientationOptions = new ObservableCollection<string> { "Dikey", "Yatay" };
        ConvertTargetFormats = new ObservableCollection<string>(DocumentConvertFormats.Concat(ImageConvertFormats));
        ConvertFormatGroups = new ObservableCollection<ConvertFormatGroup>
        {
            new("Document", DocumentConvertFormats),
            new("Image", ImageConvertFormats)
        };
        FilteredConvertFormatGroups = new ObservableCollection<ConvertFormatGroup>();
        RefreshFilteredConvertFormatGroups();

        LoadPrintSettings();
        LoadConvertSettings();
        RefreshCurrentUserTransferSettings();
        RefreshOnlineUserDownloadFolders();

        _onlineUserCleanupTimer = new System.Threading.Timer(_ => PruneOfflineUsers(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        StartLanPresence();
        StartLanFileTransfer();

        UploadFiles = new ObservableCollection<UploadFileItem>();
        UploadFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(UploadFileCountText));
            CommandManager.InvalidateRequerySuggested();
        };

        ConvertFiles = new ObservableCollection<ConvertFileItem>();
        ConvertFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ConvertFileCountText));
            CommandManager.InvalidateRequerySuggested();
        };

        PdfMergeFiles = new ObservableCollection<ConvertFileItem>();
        PdfMergeFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PdfMergeFileCountText));
            CommandManager.InvalidateRequerySuggested();
        };

        ConvertTransferFiles = new ObservableCollection<TransferFileItem>();
        ConvertTransferFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ConvertTransferFileCountText));
            OnPropertyChanged(nameof(ConvertTransferDropHintText));
            OnPropertyChanged(nameof(TransferFileCountText));
            OnPropertyChanged(nameof(TransferSendHintText));
            OnPropertyChanged(nameof(TransferSendButtonText));
            CommandManager.InvalidateRequerySuggested();
        };

        PrintHistory = new ObservableCollection<PrintHistoryItem>();
        PrintHistory.CollectionChanged += (_, _) => CommandManager.InvalidateRequerySuggested();
        TransferHistory = new ObservableCollection<TransferHistoryItem>();
        LoadTransferHistory();
        TransferHistory.CollectionChanged += (_, _) =>
        {
            SaveTransferHistory();
            CommandManager.InvalidateRequerySuggested();
        };

        PendingIncomingTransfers = new ObservableCollection<PendingTransferItem>();
        PendingIncomingTransfers.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PendingIncomingTransferCountText));
            OnPropertyChanged(nameof(PendingIncomingTransferListVisibility));
            OnPropertyChanged(nameof(PendingIncomingTransferEmptyVisibility));
            CommandManager.InvalidateRequerySuggested();
        };

        RegisteredPrinters = new ObservableCollection<PrinterDeviceItem>();
        PrinterDevices = new ObservableCollection<ToolCard>();

        LibraryNavigation = new ObservableCollection<NavItem>();

        LoadRegisteredPrinters();

        SelectNavCommand = new RelayCommand(parameter => { AppLogger.Audit("NAVIGATION", System.Convert.ToString(parameter)); SelectNavigation(parameter); });
        BrowseUploadFilesCommand = new RelayCommand(_ => { AppLogger.Audit("PRINT_BROWSE_FILES"); BrowseUploadFiles(); });
        RemoveUploadFileCommand = new RelayCommand(RemoveUploadFile);
        ClearUploadFilesCommand = new RelayCommand(_ => { AppLogger.Audit("PRINT_CLEAR_FILES", $"Count={UploadFiles.Count}"); ClearUploadFiles(); }, _ => UploadFiles.Count > 0);
        BrowseConvertFilesCommand = new RelayCommand(_ => { AppLogger.Audit("CONVERT_BROWSE_FILES"); BrowseConvertFiles(); });
        RemoveConvertFileCommand = new RelayCommand(RemoveConvertFile);
        ClearConvertFilesCommand = new RelayCommand(_ => { AppLogger.Audit("CONVERT_CLEAR_FILES", $"Count={ConvertFiles.Count}"); ClearConvertFiles(); }, _ => ConvertFiles.Count > 0);
        BrowseConvertOutputFolderCommand = new RelayCommand(_ => { AppLogger.Audit("CONVERT_SELECT_OUTPUT_FOLDER"); BrowseConvertOutputFolder(); });
        BrowsePdfMergeFilesCommand = new RelayCommand(_ => { AppLogger.Audit("PDF_MERGE_BROWSE_FILES"); BrowsePdfMergeFiles(); });
        RemovePdfMergeFileCommand = new RelayCommand(RemovePdfMergeFile);
        ClearPdfMergeFilesCommand = new RelayCommand(_ => { AppLogger.Audit("PDF_MERGE_CLEAR_FILES", $"Count={PdfMergeFiles.Count}"); ClearPdfMergeFiles(); }, _ => PdfMergeFiles.Count > 0);
        MergePdfFilesCommand = new RelayCommand(_ => { AppLogger.Audit("PDF_MERGE_START", $"Files={PdfMergeFiles.Count}; OutputFolder={ConvertOutputFolder}"); _ = MergePdfFilesAsync(); }, _ => CanMergePdfFiles());
        BrowseTransferDownloadFolderCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_SELECT_DOWNLOAD_FOLDER"); BrowseTransferDownloadFolder(); });
        BrowseTransferFilesCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_BROWSE_FILES"); BrowseTransferFiles(); });
        RemoveTransferFileCommand = new RelayCommand(RemoveConvertTransferFile);
        ClearTransferFilesCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_CLEAR_FILES", $"Count={ConvertTransferFiles.Count}"); ClearConvertTransferFiles(); }, _ => ConvertTransferFiles.Count > 0);
        SelectTransferRecipientCommand = new RelayCommand(parameter => { AppLogger.Audit("TRANSFER_SELECT_RECIPIENT", parameter is OnlineUserItem user ? user.DisplayName : "-"); SelectTransferRecipient(parameter); });
        SendSelectedTransferFilesCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_SEND_START", $"Recipient={SelectedTransferRecipient?.DisplayName ?? string.Empty}; Files={ConvertTransferFiles.Count}"); _ = SendConvertTransferFilesToUserAsync(SelectedTransferRecipient); }, _ => CanSendConvertTransferFilesToUser(SelectedTransferRecipient));
        ClearTransferHistoryCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_CLEAR_HISTORY", $"Count={TransferHistory.Count}"); ClearTransferHistory(); }, _ => TransferHistory.Count > 0);
        OpenIncomingTransferCommand = new RelayCommand(OpenIncomingTransfer, parameter => parameter is PendingTransferItem);
        AcceptIncomingTransferCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_ACCEPT_INCOMING", SelectedIncomingTransfer?.PrimaryFileName); _ = AcceptSelectedIncomingTransferAsync(); }, _ => SelectedIncomingTransfer is not null && !IsTransferInProgress);
        RejectIncomingTransferCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_REJECT_INCOMING", SelectedIncomingTransfer?.PrimaryFileName); RejectSelectedIncomingTransfer(); }, _ => SelectedIncomingTransfer is not null);
        CloseIncomingTransferCommand = new RelayCommand(_ => CloseIncomingTransferModal());
        ClearPendingIncomingTransfersCommand = new RelayCommand(_ => { AppLogger.Audit("TRANSFER_CLEAR_PENDING", $"Count={PendingIncomingTransfers.Count}"); ClearPendingIncomingTransfers(); }, _ => PendingIncomingTransfers.Count > 0);
        OpenTransferHistoryFileCommand = new RelayCommand(OpenTransferHistoryFile, CanUseTransferHistoryFile);
        SendTransferHistoryToPrintPoolCommand = new RelayCommand(SendTransferHistoryToPrintPool, CanUseTransferHistoryFile);
        PrintTransferHistoryFileCommand = new RelayCommand(parameter => _ = PrintTransferHistoryFileAsync(parameter), CanUseTransferHistoryFile);
        OpenConvertResultCommand = new RelayCommand(OpenConvertResult);
        PrintConvertResultCommand = new RelayCommand(parameter => _ = PrintConvertResultAsync(parameter), CanUseConvertResult);
        SendConvertResultToPrintPoolCommand = new RelayCommand(SendConvertResultToPrintPool, CanUseConvertResult);
        RemoveConvertTransferFileCommand = new RelayCommand(RemoveConvertTransferFile);
        ClearConvertTransferFilesCommand = new RelayCommand(_ => ClearConvertTransferFiles(), _ => ConvertTransferFiles.Count > 0);
        SendConvertTransferFilesCommand = new RelayCommand(parameter => _ = SendConvertTransferFilesToUserAsync(parameter), CanSendConvertTransferFilesToUser);
        SelectConvertTargetFormatCommand = new RelayCommand(SelectConvertTargetFormat);
        ConvertDocumentsCommand = new RelayCommand(_ => { AppLogger.Audit("CONVERT_START", $"Target={SelectedConvertTargetFormat}; Files={ConvertFiles.Count}; OutputFolder={ConvertOutputFolder}"); _ = ConvertDocumentsAsync(); }, _ => CanConvertDocuments());
        PrintDocumentsCommand = new RelayCommand(_ => { AppLogger.Audit("PRINT_START", $"Printer={SelectedPrintPrinter?.Name ?? string.Empty}; Files={UploadFiles.Count}"); _ = PrintUploadedDocumentsAsync(); }, _ => CanPrintUploadedDocuments());
        ClearPrintHistoryCommand = new RelayCommand(_ => { AppLogger.Audit("PRINT_CLEAR_HISTORY", $"Count={PrintHistory.Count}"); ClearPrintHistory(); }, _ => PrintHistory.Count > 0);
        SelectRegisteredPrinterCommand = new RelayCommand(SelectRegisteredPrinter);
        SelectPrintPrinterCommand = new RelayCommand(SelectPrintPrinter);
        SetDefaultPrinterCommand = new RelayCommand(_ => { AppLogger.Audit("PRINTER_SET_DEFAULT", SelectedRegisteredPrinter?.Name); SetDefaultPrinter(); }, _ => SelectedRegisteredPrinter is not null);
        RemoveRegisteredPrinterCommand = new RelayCommand(_ => { AppLogger.Audit("PRINTER_REMOVE", SelectedRegisteredPrinter?.Name); RemoveRegisteredPrinter(); }, _ => SelectedRegisteredPrinter is not null);
        SavePrinterCommand = new RelayCommand(_ => { AppLogger.Audit("PRINTER_SAVE", ManualPrinterName); SaveManualPrinter(); }, _ => CanSaveManualPrinter());
        RefreshSystemStatusCommand = new RelayCommand(_ => RefreshConvertEngineStatuses());
        CancelOperationJobCommand = new RelayCommand(parameter => { AppLogger.Audit("JOB_CANCEL", parameter is OperationJobItem job ? job.Title : "-"); CancelOperationJob(parameter); }, parameter => parameter is OperationJobItem job && job.CancelButtonVisibility == Visibility.Visible);
        ClearCompletedJobsCommand = new RelayCommand(_ => { AppLogger.Audit("JOB_CLEAR_COMPLETED"); ClearCompletedJobs(); }, _ => OperationJobs.Any(job => job.IsCompleted || job.IsCancelled));
        ToggleTransferReceiveCommand = new RelayCommand(_ => { IsTransferReceiveEnabled = !IsTransferReceiveEnabled; AppLogger.Audit("SETTING_TRANSFER_RECEIVE", $"Enabled={IsTransferReceiveEnabled}"); });
        ToggleDarkModeCommand = new RelayCommand(_ => { IsDarkModeEnabled = !IsDarkModeEnabled; AppLogger.Audit("SETTING_DARK_MODE", $"Enabled={IsDarkModeEnabled}"); });
    
        InitializeBcDownloadWatcher();
}

    public ObservableCollection<NavItem> PrimaryNavigation { get; }
    public ObservableCollection<NavItem> LibraryNavigation { get; }
    public ObservableCollection<OnlineUserItem> OnlineUsers { get; }
    public ObservableCollection<OnlineUserItem> FilteredOnlineUsers { get; }
    public ObservableCollection<OnlineUserItem> LeftFilteredOnlineUsers { get; }
    public ObservableCollection<OnlineUserItem> TransferFilteredOnlineUsers { get; }
    public ObservableCollection<OnlineUserItem> ConvertFilteredOnlineUsers { get; }
    public ObservableCollection<SystemStatusItem> ConvertEngineStatuses { get; }
    public ObservableCollection<OperationJobItem> OperationJobs { get; }
    public ObservableCollection<UploadFileItem> UploadFiles { get; }
    public ObservableCollection<ConvertFileItem> ConvertFiles { get; }
    public ObservableCollection<ConvertFileItem> PdfMergeFiles { get; }
    public ObservableCollection<TransferFileItem> ConvertTransferFiles { get; }
    public ObservableCollection<string> ConvertTargetFormats { get; }
    public ObservableCollection<ConvertFormatGroup> ConvertFormatGroups { get; }
    public ObservableCollection<ConvertFormatGroup> FilteredConvertFormatGroups { get; }
    public ObservableCollection<PrintHistoryItem> PrintHistory { get; }
    public ObservableCollection<TransferHistoryItem> TransferHistory { get; }
    public ObservableCollection<PendingTransferItem> PendingIncomingTransfers { get; }
    public ObservableCollection<PrinterDeviceItem> RegisteredPrinters { get; }
    public ObservableCollection<string> PrintColorOptions { get; }
    public ObservableCollection<string> PrintSideOptions { get; }
    public ObservableCollection<string> PaperSizeOptions { get; }
    public ObservableCollection<string> MarginProfileOptions { get; }
    public ObservableCollection<string> ScaleModeOptions { get; }
    public ObservableCollection<string> OrientationOptions { get; }
    public ObservableCollection<ToolCard> PrinterDevices { get; }

    public ICommand SelectNavCommand { get; }
    public ICommand BrowseUploadFilesCommand { get; }
    public ICommand RemoveUploadFileCommand { get; }
    public ICommand ClearUploadFilesCommand { get; }
    public ICommand BrowseConvertFilesCommand { get; }
    public ICommand RemoveConvertFileCommand { get; }
    public ICommand ClearConvertFilesCommand { get; }
    public ICommand BrowseConvertOutputFolderCommand { get; }
    public ICommand BrowsePdfMergeFilesCommand { get; }
    public ICommand RemovePdfMergeFileCommand { get; }
    public ICommand ClearPdfMergeFilesCommand { get; }
    public ICommand MergePdfFilesCommand { get; }
    public ICommand BrowseTransferDownloadFolderCommand { get; }
    public ICommand BrowseTransferFilesCommand { get; }
    public ICommand RemoveTransferFileCommand { get; }
    public ICommand ClearTransferFilesCommand { get; }
    public ICommand SelectTransferRecipientCommand { get; }
    public ICommand SendSelectedTransferFilesCommand { get; }
    public ICommand ClearTransferHistoryCommand { get; }
    public ICommand OpenIncomingTransferCommand { get; }
    public ICommand AcceptIncomingTransferCommand { get; }
    public ICommand RejectIncomingTransferCommand { get; }
    public ICommand CloseIncomingTransferCommand { get; }
    public ICommand ClearPendingIncomingTransfersCommand { get; }
    public ICommand OpenTransferHistoryFileCommand { get; }
    public ICommand SendTransferHistoryToPrintPoolCommand { get; }
    public ICommand PrintTransferHistoryFileCommand { get; }
    public ICommand OpenConvertResultCommand { get; }
    public ICommand PrintConvertResultCommand { get; }
    public ICommand SendConvertResultToPrintPoolCommand { get; }
    public ICommand RemoveConvertTransferFileCommand { get; }
    public ICommand ClearConvertTransferFilesCommand { get; }
    public ICommand SendConvertTransferFilesCommand { get; }
    public ICommand SelectConvertTargetFormatCommand { get; }
    public ICommand ConvertDocumentsCommand { get; }
    public ICommand PrintDocumentsCommand { get; }
    public ICommand ClearPrintHistoryCommand { get; }
    public ICommand SelectRegisteredPrinterCommand { get; }
    public ICommand SelectPrintPrinterCommand { get; }
    public ICommand SetDefaultPrinterCommand { get; }
    public ICommand RemoveRegisteredPrinterCommand { get; }
    public ICommand SavePrinterCommand { get; }
    public ICommand RefreshSystemStatusCommand { get; }
    public ICommand CancelOperationJobCommand { get; }
    public ICommand ClearCompletedJobsCommand { get; }
    public ICommand ToggleTransferReceiveCommand { get; }
    public ICommand ToggleDarkModeCommand { get; }

    public OnlineUserItem? SelectedTransferRecipient
    {
        get => _selectedTransferRecipient;
        set
        {
            if (ReferenceEquals(_selectedTransferRecipient, value))
            {
                return;
            }

            if (_selectedTransferRecipient is not null)
            {
                _selectedTransferRecipient.IsSelectedForTransfer = false;
            }

            _selectedTransferRecipient = value;

            if (_selectedTransferRecipient is not null)
            {
                _selectedTransferRecipient.IsSelectedForTransfer = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTransferRecipientText));
            OnPropertyChanged(nameof(TransferSendButtonText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string OnlineUserSearchText
    {
        get => LeftOnlineUserSearchText;
        set => LeftOnlineUserSearchText = value;
    }

    public string LeftOnlineUserSearchText
    {
        get => _leftOnlineUserSearchText;
        set
        {
            if (SetProperty(ref _leftOnlineUserSearchText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(OnlineUserSearchText));
                RefreshLeftFilteredOnlineUsers();
            }
        }
    }

    public string TransferOnlineUserSearchText
    {
        get => _transferOnlineUserSearchText;
        set
        {
            if (SetProperty(ref _transferOnlineUserSearchText, value ?? string.Empty))
            {
                RefreshTransferFilteredOnlineUsers();
            }
        }
    }

    public string ConvertOnlineUserSearchText
    {
        get => _convertOnlineUserSearchText;
        set
        {
            if (SetProperty(ref _convertOnlineUserSearchText, value ?? string.Empty))
            {
                RefreshConvertFilteredOnlineUsers();
            }
        }
    }

    public string OnlineUserCountText => $"Online: {OnlineUsers.Count(user => user.IsOnline && !IsCurrentOnlineUser(user))}";

    public string SelectedTransferRecipientText => SelectedTransferRecipient is null
        ? "Seçili alıcı yok. Listeden bir personel seçin."
        : $"Seçili alıcı: {SelectedTransferRecipient.DisplayName}";

    public string TransferFileCountText => $"Transfer Dosyaları ({ConvertTransferFiles.Count})";

    public string TransferSendHintText => ConvertTransferFiles.Count == 0
        ? "Henüz transfer dosyası seçilmedi."
        : $"{ConvertTransferFiles.Count} dosya gönderime hazır.";

    public bool IsTransferInProgress
    {
        get => _isTransferInProgress;
        private set
        {
            if (SetProperty(ref _isTransferInProgress, value))
            {
                OnPropertyChanged(nameof(TransferSendButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string TransferSendButtonText => IsTransferInProgress
        ? "Aktarılıyor"
        : "Gönder";

    public string OperationQueueCountText => OperationJobs.Count == 0
        ? "İş kuyruğu boş"
        : $"İş Kuyruğu ({OperationJobs.Count})";

    public Visibility OperationQueueListVisibility => OperationJobs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OperationQueueEmptyVisibility => OperationJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string ConvertEngineStatusSummary
    {
        get
        {
            if (ConvertEngineStatuses.Count == 0)
            {
                return "Sistem durumu bekleniyor.";
            }

            var ready = ConvertEngineStatuses.Count(item => item.IsAvailable);
            return $"{ready}/{ConvertEngineStatuses.Count} motor hazır";
        }
    }

    public string PendingIncomingTransferCountText => PendingIncomingTransfers.Count == 0
        ? "Gelen transfer yok"
        : $"Gelen Transferler ({PendingIncomingTransfers.Count})";

    public Visibility PendingIncomingTransferListVisibility => PendingIncomingTransfers.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PendingIncomingTransferEmptyVisibility => PendingIncomingTransfers.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public PendingTransferItem? SelectedIncomingTransfer
    {
        get => _selectedIncomingTransfer;
        set
        {
            if (SetProperty(ref _selectedIncomingTransfer, value))
            {
                OnPropertyChanged(nameof(IncomingTransferModalVisibility));
                OnPropertyChanged(nameof(IncomingTransferTitle));
                OnPropertyChanged(nameof(IncomingTransferSubtitle));
                OnPropertyChanged(nameof(IncomingTransferDestinationText));
                OnPropertyChanged(nameof(IncomingTransferDateText));
                OnPropertyChanged(nameof(IncomingTransferSizeText));
                OnPropertyChanged(nameof(IncomingTransferFiles));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIncomingTransferModalOpen
    {
        get => _isIncomingTransferModalOpen;
        set
        {
            if (SetProperty(ref _isIncomingTransferModalOpen, value))
            {
                OnPropertyChanged(nameof(IncomingTransferModalVisibility));
            }
        }
    }

    public Visibility IncomingTransferModalVisibility => IsIncomingTransferModalOpen && SelectedIncomingTransfer is not null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string IncomingTransferTitle => SelectedIncomingTransfer is null
        ? "Gelen Transfer"
        : $"{SelectedIncomingTransfer.SenderName} size dosya gönderdi";

    public string IncomingTransferSubtitle => SelectedIncomingTransfer is null
        ? string.Empty
        : $"{SelectedIncomingTransfer.FileCountText} kabul bekliyor.";

    public string IncomingTransferDestinationText => SelectedIncomingTransfer?.DestinationText ?? string.Empty;
    public string IncomingTransferDateText => SelectedIncomingTransfer?.DateText ?? string.Empty;
    public string IncomingTransferSizeText => SelectedIncomingTransfer?.TotalSizeText ?? string.Empty;
    public ObservableCollection<TransferFileItem>? IncomingTransferFiles => SelectedIncomingTransfer?.Files;

    public string TransferStatusMessage
    {
        get => _transferStatusMessage;
        set => SetProperty(ref _transferStatusMessage, value);
    }

    public Brush TransferStatusBrush
    {
        get => _transferStatusBrush;
        set => SetProperty(ref _transferStatusBrush, value);
    }


    public string SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                OnPropertyChanged(nameof(SelectedPageTitle));
                OnPropertyChanged(nameof(SelectedPageSubtitle));
            }
        }
    }

    public string SelectedPageTitle => SelectedPage;

    public string SelectedPageSubtitle => SelectedPage switch
    {
        "Yazdırma" => "Dokümanlarınızı doğrudan bu alana bırakabilirsiniz. Word, Excel, PowerPoint, PDF ve görseller desteklenir.",
        "Convert" => "Doküman ve görsel formatlarını hızlıca dönüştürün, işlemleri tek ekrandan takip edin.",
        "Transfer" => "Dosya gönderme ve alma işlemlerinizi bu alandan yönetin.",
        _ => "ToolBridge ayarlarını ve cihaz kayıtlarını buradan yönetin."
    };

    public Brush ActiveAccentBrush
    {
        get => _activeAccentBrush;
        private set => SetProperty(ref _activeAccentBrush, value);
    }

    public Brush ActiveAccentSoftBrush
    {
        get => _activeAccentSoftBrush;
        private set => SetProperty(ref _activeAccentSoftBrush, value);
    }

    public Brush ActiveAccentBorderBrush
    {
        get => _activeAccentBorderBrush;
        private set => SetProperty(ref _activeAccentBorderBrush, value);
    }

    public Brush ActiveAccentRingBrush
    {
        get => _activeAccentRingBrush;
        private set => SetProperty(ref _activeAccentRingBrush, value);
    }

    public PrinterDeviceItem? SelectedRegisteredPrinter
    {
        get => _selectedRegisteredPrinter;
        set
        {
            if (SetProperty(ref _selectedRegisteredPrinter, value))
            {
                OnPropertyChanged(nameof(SelectedPrinterText));
                OnPropertyChanged(nameof(DefaultPrinterText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public PrinterDeviceItem? SelectedPrintPrinter
    {
        get => _selectedPrintPrinter;
        set
        {
            if (SetProperty(ref _selectedPrintPrinter, value))
            {
                OnPropertyChanged(nameof(SelectedPrintPrinterTitle));
                OnPropertyChanged(nameof(SelectedPrintPrinterSubtitle));
                OnPropertyChanged(nameof(SelectedPrintPrinterQueueText));
                OnPropertyChanged(nameof(SelectedPrintPrinterCoverBrush));
                OnPropertyChanged(nameof(PrintSettingsHeaderText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SelectedPrintPrinterTitle => SelectedPrintPrinter?.Name ?? "Yazıcı seçilmedi";
    public string SelectedPrintPrinterSubtitle => SelectedPrintPrinter?.IpAddress ?? "Yazdırma sayfasından bir cihaz seçin";
    public string SelectedPrintPrinterQueueText => SelectedPrintPrinter is null ? "Kuyruk bekleniyor" : $"Kuyruk: {SelectedPrintPrinter.QueueValue}";
    public Brush SelectedPrintPrinterCoverBrush => SelectedPrintPrinter?.CoverBrush ?? Gradient("#94A3B8", "#E5E7EB");
    public string PrintSettingsHeaderText => "Yazdırma Ayarları";

    public string SelectedPrintColor
    {
        get => _selectedPrintColor;
        set
        {
            if (SetProperty(ref _selectedPrintColor, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string SelectedPrintSide
    {
        get => _selectedPrintSide;
        set
        {
            if (SetProperty(ref _selectedPrintSide, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string SelectedPaperSize
    {
        get => _selectedPaperSize;
        set
        {
            if (SetProperty(ref _selectedPaperSize, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string SelectedMarginProfile
    {
        get => _selectedMarginProfile;
        set
        {
            if (SetProperty(ref _selectedMarginProfile, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string SelectedScaleMode
    {
        get => _selectedScaleMode;
        set
        {
            if (SetProperty(ref _selectedScaleMode, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string SelectedOrientation
    {
        get => _selectedOrientation;
        set
        {
            if (SetProperty(ref _selectedOrientation, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string PrintCopyCount
    {
        get => _printCopyCount;
        set
        {
            if (SetProperty(ref _printCopyCount, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string PrintSplitLimit
    {
        get => _printSplitLimit;
        set => SetProperty(ref _printSplitLimit, value);
    }

    public string PrintPageRange
    {
        get => _printPageRange;
        set
        {
            if (SetProperty(ref _printPageRange, value))
            {
                SavePrintSettings();
            }
        }
    }

    public string ManualPrinterName
    {
        get => _manualPrinterName;
        set
        {
            if (SetProperty(ref _manualPrinterName, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ManualPrinterIp
    {
        get => _manualPrinterIp;
        set
        {
            if (SetProperty(ref _manualPrinterIp, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ManualPrinterQueue
    {
        get => _manualPrinterQueue;
        set => SetProperty(ref _manualPrinterQueue, value);
    }

    public string SettingsStatusMessage
    {
        get => _settingsStatusMessage;
        set => SetProperty(ref _settingsStatusMessage, value);
    }

    public Brush SettingsStatusBrush
    {
        get => _settingsStatusBrush;
        set => SetProperty(ref _settingsStatusBrush, value);
    }

    public bool IsTransferReceiveEnabled
    {
        get => _isTransferReceiveEnabled;
        set
        {
            if (SetProperty(ref _isTransferReceiveEnabled, value))
            {
                SavePrintSettings();
                RefreshCurrentUserTransferSettings();
                OnPropertyChanged(nameof(TransferReceiveToggleText));
                OnPropertyChanged(nameof(TransferReceiveInfoText));
            }
        }
    }

    public string TransferDownloadFolder
    {
        get => _transferDownloadFolder;
        set
        {
            var normalizedValue = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _transferDownloadFolder, normalizedValue))
            {
                SavePrintSettings();
                RefreshOnlineUserDownloadFolders();
                OnPropertyChanged(nameof(TransferDownloadFolderInfoText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsDarkModeEnabled
    {
        get => _isDarkModeEnabled;
        set
        {
            if (SetProperty(ref _isDarkModeEnabled, value))
            {
                ApplyVisualTheme();
                RefreshThemeBoundItems();
                SavePrintSettings();
                OnPropertyChanged(nameof(DarkModeToggleText));
            }
        }
    }

    public bool IsPrinting
    {
        get => _isPrinting;
        private set
        {
            if (SetProperty(ref _isPrinting, value))
            {
                OnPropertyChanged(nameof(PrintButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string PrintButtonText => IsPrinting ? "Yazdırılıyor" : "Yazdır";

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            if (SetProperty(ref _isConverting, value))
            {
                OnPropertyChanged(nameof(ConvertButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ConvertButtonText => IsConverting ? "Dönüştürülüyor" : "Dönüştür";

    public bool IsMergingPdf
    {
        get => _isMergingPdf;
        private set
        {
            if (SetProperty(ref _isMergingPdf, value))
            {
                OnPropertyChanged(nameof(PdfMergeButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string PdfMergeButtonText => IsMergingPdf ? "Birleştiriliyor" : "Birleştir";

    public string UploadLimitText => "Maksimum dosya boyutu: 50 MB";
    public string UploadFileCountText => $"Yüklenen Dosyalar ({UploadFiles.Count})";
    public string DefaultPrinterText => $"Varsayılan yazıcı: {RegisteredPrinters.FirstOrDefault(printer => printer.IsDefault)?.Name ?? "Seçilmedi"}";
    public string SelectedPrinterText => SelectedRegisteredPrinter is null
        ? "Seçili yazıcı yok."
        : SelectedRegisteredPrinter.IsDefault ? "Seçili yazıcı varsayılan." : "Seçili yazıcı varsayılan değil.";
    public string TransferReceiveToggleText => IsTransferReceiveEnabled ? "Transfer alımı açık" : "Transfer alımı kapalı";
    public string TransferReceiveInfoText => IsTransferReceiveEnabled
        ? "Transfer alımı açık. Diğer kullanıcılar size dosya gönderebilir."
        : "Transfer alımı kapalı. Diğer kullanıcılar size dosya gönderemez.";
    public string TransferDownloadFolderInfoText => string.IsNullOrWhiteSpace(TransferDownloadFolder)
        ? "Transfer indirme klasörü seçilmedi."
        : $"Transferler şu klasöre indirilir: {TransferDownloadFolder}";
    public string DarkModeToggleText => IsDarkModeEnabled ? "Gece modu açık" : "Gece modu kapalı";

    public string ApplicationVersionText => GetApplicationVersionText();

    public string ProducerNameText => "Tolga Demirel";

    public string ProducerCopyrightText => "© 2026 Tolga Demirel. Tüm hakları saklıdır.";

    private static string GetApplicationVersionText()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }

    public string UploadStatusMessage
    {
        get => _uploadStatusMessage;
        set => SetProperty(ref _uploadStatusMessage, value);
    }

    public Brush UploadStatusBrush
    {
        get => _uploadStatusBrush;
        set => SetProperty(ref _uploadStatusBrush, value);
    }

    public string ConvertLimitText => "Maksimum dosya boyutu: 50 MB • Belge ve görsel format dönüşümleri";
    public string ConvertFileCountText => $"Seçilen Dosyalar ({ConvertFiles.Count})";
    public string ConvertTransferFileCountText => $"Gönderilecek Çıktılar ({ConvertTransferFiles.Count})";
    public string ConvertTransferDropHintText => ConvertTransferFiles.Count == 0
        ? "Dönüştürülen çıktıları bu kutuya sürükleyin."
        : "Hazır. Online kullanıcıya tek tıkla gönderebilirsiniz.";

    public string ConvertStatusMessage
    {
        get => _convertStatusMessage;
        set => SetProperty(ref _convertStatusMessage, value);
    }

    public Brush ConvertStatusBrush
    {
        get => _convertStatusBrush;
        set => SetProperty(ref _convertStatusBrush, value);
    }

    public string PdfMergeLimitText => "Yalnızca PDF dosyaları • Sıralama listeye eklenme sırasına göre yapılır";
    public string PdfMergeFileCountText => $"Seçilen PDF Dosyaları ({PdfMergeFiles.Count})";

    public string PdfMergeStatusMessage
    {
        get => _pdfMergeStatusMessage;
        set => SetProperty(ref _pdfMergeStatusMessage, value);
    }

    public Brush PdfMergeStatusBrush
    {
        get => _pdfMergeStatusBrush;
        set => SetProperty(ref _pdfMergeStatusBrush, value);
    }

    public string PdfMergeOutputFileName
    {
        get => _pdfMergeOutputFileName;
        set
        {
            if (SetProperty(ref _pdfMergeOutputFileName, value ?? string.Empty))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ConvertTransferStatusMessage
    {
        get => _convertTransferStatusMessage;
        set => SetProperty(ref _convertTransferStatusMessage, value);
    }

    public Brush ConvertTransferStatusBrush
    {
        get => _convertTransferStatusBrush;
        set => SetProperty(ref _convertTransferStatusBrush, value);
    }

    public string SelectedConvertTargetFormat
    {
        get => _selectedConvertTargetFormat;
        set
        {
            if (SetProperty(ref _selectedConvertTargetFormat, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsConvertFormatPopupOpen
    {
        get => _isConvertFormatPopupOpen;
        set => SetProperty(ref _isConvertFormatPopupOpen, value);
    }

    public bool IsConvertSideFormatPopupOpen
    {
        get => _isConvertSideFormatPopupOpen;
        set => SetProperty(ref _isConvertSideFormatPopupOpen, value);
    }


    public string ConvertFormatSearchText
    {
        get => _convertFormatSearchText;
        set
        {
            if (SetProperty(ref _convertFormatSearchText, value))
            {
                RefreshFilteredConvertFormatGroups();
            }
        }
    }

    public string ConvertOutputFolder
    {
        get => _convertOutputFolder;
        set
        {
            var normalizedValue = (value ?? string.Empty).Trim();
            if (SetProperty(ref _convertOutputFolder, normalizedValue))
            {
                SaveConvertSettings();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public void AddUploadFiles(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths.Where(File.Exists))
        {
            TryAddUploadFile(filePath);
        }

        CommandManager.InvalidateRequerySuggested();
    }


    private void RefreshFilteredConvertFormatGroups()
    {
        if (FilteredConvertFormatGroups is null || ConvertFormatGroups is null)
        {
            return;
        }

        var query = (ConvertFormatSearchText ?? string.Empty).Trim();
        FilteredConvertFormatGroups.Clear();

        foreach (var group in ConvertFormatGroups)
        {
            var formats = string.IsNullOrWhiteSpace(query)
                ? group.Formats.ToArray()
                : group.Formats
                    .Where(format => format.Contains(query, StringComparison.OrdinalIgnoreCase) || group.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            if (formats.Length > 0)
            {
                FilteredConvertFormatGroups.Add(new ConvertFormatGroup(group.Name, formats));
            }
        }
    }

    private void RefreshFilteredOnlineUsers()
    {
        RefreshLeftFilteredOnlineUsers();
        RefreshTransferFilteredOnlineUsers();
        RefreshConvertFilteredOnlineUsers();
    }

    private void RefreshLeftFilteredOnlineUsers()
    {
        RefreshOnlineUserView(LeftFilteredOnlineUsers, LeftOnlineUserSearchText);
        SyncLegacyFilteredOnlineUsers(LeftFilteredOnlineUsers);
        OnPropertyChanged(nameof(OnlineUserCountText));
    }

    private void RefreshTransferFilteredOnlineUsers()
    {
        RefreshOnlineUserView(TransferFilteredOnlineUsers, TransferOnlineUserSearchText);
        OnPropertyChanged(nameof(OnlineUserCountText));
    }

    private void RefreshConvertFilteredOnlineUsers()
    {
        RefreshOnlineUserView(ConvertFilteredOnlineUsers, ConvertOnlineUserSearchText);
        OnPropertyChanged(nameof(OnlineUserCountText));
    }

    private bool IsCurrentOnlineUser(OnlineUserItem? user)
    {
        return user is not null &&
               !string.IsNullOrWhiteSpace(user.PresenceId) &&
               string.Equals(user.PresenceId, _currentPresenceId, StringComparison.OrdinalIgnoreCase);
    }
    private void RefreshOnlineUserView(ObservableCollection<OnlineUserItem> target, string searchText)
    {
        if (target is null || OnlineUsers is null)
        {
            return;
        }

        var query = (searchText ?? string.Empty).Trim();
        target.Clear();

        foreach (var user in OnlineUsers.Where(user => user.IsOnline && !IsCurrentOnlineUser(user)))
        {
            if (string.IsNullOrWhiteSpace(query) ||
                user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                user.MachineName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                user.IpAddress.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                user.TransferStatusText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                target.Add(user);
            }
        }
    }

    private void SyncLegacyFilteredOnlineUsers(IEnumerable<OnlineUserItem> users)
    {
        if (FilteredOnlineUsers is null)
        {
            return;
        }

        FilteredOnlineUsers.Clear();
        foreach (var user in users)
        {
            FilteredOnlineUsers.Add(user);
        }
    }


    private void SelectConvertTargetFormat(object? parameter)
    {
        if (parameter is not string targetFormat || string.IsNullOrWhiteSpace(targetFormat))
        {
            return;
        }

        SelectedConvertTargetFormat = targetFormat.Trim().TrimStart('.').ToUpperInvariant();
        IsConvertFormatPopupOpen = false;
        IsConvertSideFormatPopupOpen = false;
        SetConvertStatus($"Hedef format {SelectedConvertTargetFormat} olarak seçildi.", false);
        ConvertFormatSearchText = string.Empty;
    }

    private void BrowseUploadFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Yüklenecek dosyaları seçin",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "Tüm dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddUploadFiles(dialog.FileNames);
        }
    }

    private void TryAddUploadFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            SetUploadStatus("Dosya bulunamadı.", true);
            return;
        }

        if (fileInfo.Length > MaxUploadSizeBytes)
        {
            SetUploadStatus($"{fileInfo.Name} yüklenemedi. Dosya boyutu {UploadFileItem.FormatSize(fileInfo.Length)}; izin verilen üst limit 50 MB.", true);
            return;
        }

        var safeUploadName = SanitizeFileName(fileInfo.Name);
        if (!string.Equals(safeUploadName, fileInfo.Name, StringComparison.Ordinal))
        {
            SetUploadStatus($"{fileInfo.Name} dosya adında geçersiz karakter var. Güvenli ad: {safeUploadName}", true);
            return;
        }

        var existing = UploadFiles.FirstOrDefault(file => string.Equals(file.FullPath, fileInfo.FullName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SetUploadStatus($"{fileInfo.Name} zaten listeye eklenmiş.", false);
            return;
        }

        UploadFiles.Add(UploadFileItem.FromFileInfo(fileInfo));
        SetUploadStatus($"{fileInfo.Name} yüklendi. 50 MB sınırı içinde.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveUploadFile(object? parameter)
    {
        if (parameter is UploadFileItem file)
        {
            UploadFiles.Remove(file);
            SetUploadStatus($"{file.FileName} listeden kaldırıldı.", false);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ClearUploadFiles()
    {
        UploadFiles.Clear();
        SetUploadStatus("Dosya listesi temizlendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }


    public void AddConvertFiles(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths.Where(File.Exists))
        {
            TryAddConvertFile(filePath);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    public void AddPdfMergeFiles(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths.Where(File.Exists))
        {
            TryAddPdfMergeFile(filePath);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    public void AddTransferFiles(IEnumerable<string> filePaths)
    {
        AddConvertTransferFiles(filePaths);
    }

    private void BrowseTransferFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Transfer edilecek dosyaları seçin",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "Tüm dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddTransferFiles(dialog.FileNames);
        }
    }

    private void BrowseConvertFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Dönüştürülecek dosyaları seçin",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "Desteklenen doküman ve görsel dosyaları|*.pdf;*.doc;*.docx;*.odt;*.rtf;*.txt;*.html;*.htm;*.csv;*.xls;*.xlsx;*.ods;*.ppt;*.pptx;*.odp;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.webp;*.svg;*.avif;*.heic;*.tga|Dokümanlar|*.pdf;*.doc;*.docx;*.odt;*.rtf;*.txt;*.html;*.htm;*.csv;*.xls;*.xlsx;*.ods;*.ppt;*.pptx;*.odp|Görseller|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.webp;*.svg;*.avif;*.heic;*.tga|Tüm dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddConvertFiles(dialog.FileNames);
        }
    }

    private void BrowsePdfMergeFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Birleştirilecek PDF dosyalarını seçin",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "PDF dosyaları|*.pdf|Tüm dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddPdfMergeFiles(dialog.FileNames);
        }
    }

    private void TryAddConvertFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            SetConvertStatus("Dosya bulunamadı.", true);
            return;
        }

        if (fileInfo.Length > MaxUploadSizeBytes)
        {
            SetConvertStatus($"{fileInfo.Name} eklenemedi. Dosya boyutu {UploadFileItem.FormatSize(fileInfo.Length)}; izin verilen üst limit 50 MB.", true);
            return;
        }

        var safeConvertName = SanitizeFileName(fileInfo.Name);
        if (!string.Equals(safeConvertName, fileInfo.Name, StringComparison.Ordinal))
        {
            SetConvertStatus($"{fileInfo.Name} dosya adında geçersiz karakter var. Güvenli ad: {safeConvertName}", true);
            return;
        }

        var existing = ConvertFiles.FirstOrDefault(file => string.Equals(file.FullPath, fileInfo.FullName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SetConvertStatus($"{fileInfo.Name} zaten listeye eklenmiş.", false);
            return;
        }

        ConvertFiles.Add(ConvertFileItem.FromFileInfo(fileInfo));
        SetConvertStatus($"{fileInfo.Name} dönüştürme listesine eklendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void TryAddPdfMergeFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            SetPdfMergeStatus("PDF dosyası bulunamadı.", true);
            return;
        }

        if (!string.Equals(fileInfo.Extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            SetPdfMergeStatus($"{fileInfo.Name} eklenemedi. Sadece PDF dosyaları birleştirilebilir.", true);
            return;
        }

        if (fileInfo.Length > MaxUploadSizeBytes)
        {
            SetPdfMergeStatus($"{fileInfo.Name} eklenemedi. Dosya boyutu {UploadFileItem.FormatSize(fileInfo.Length)}; izin verilen üst limit 50 MB.", true);
            return;
        }

        var safeName = SanitizeFileName(fileInfo.Name);
        if (!string.Equals(safeName, fileInfo.Name, StringComparison.Ordinal))
        {
            SetPdfMergeStatus($"{fileInfo.Name} dosya adında geçersiz karakter var. Güvenli ad: {safeName}", true);
            return;
        }

        var existing = PdfMergeFiles.FirstOrDefault(file => string.Equals(file.FullPath, fileInfo.FullName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SetPdfMergeStatus($"{fileInfo.Name} zaten PDF birleştirme listesinde.", false);
            return;
        }

        PdfMergeFiles.Add(ConvertFileItem.FromFileInfo(fileInfo));
        SetPdfMergeStatus($"{fileInfo.Name} PDF birleştirme listesine eklendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveConvertFile(object? parameter)
    {
        if (parameter is ConvertFileItem file)
        {
            ConvertFiles.Remove(file);
            SetConvertStatus($"{file.FileName} listeden kaldırıldı.", false);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ClearConvertFiles()
    {
        foreach (var file in ConvertFiles)
        {
            file.MarkReady();
        }

        ConvertFiles.Clear();
        SetConvertStatus("Dönüştürme listesi temizlendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemovePdfMergeFile(object? parameter)
    {
        if (parameter is ConvertFileItem file)
        {
            PdfMergeFiles.Remove(file);
            SetPdfMergeStatus($"{file.FileName} PDF birleştirme listesinden kaldırıldı.", false);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ClearPdfMergeFiles()
    {
        foreach (var file in PdfMergeFiles)
        {
            file.MarkReady();
        }

        PdfMergeFiles.Clear();
        SetPdfMergeStatus("PDF birleştirme listesi temizlendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void BrowseConvertOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Personel varsayılan kayıt klasörünü seçin",
            Multiselect = false,
            InitialDirectory = Directory.Exists(ConvertOutputFolder)
                ? ConvertOutputFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            ConvertOutputFolder = dialog.FolderName;
            SetConvertStatus($"Varsayılan kayıt klasörü seçildi: {ConvertOutputFolder}", false);
        }
    }

    private void BrowseTransferDownloadFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Transferlerin indirileceği klasörü seçin",
            Multiselect = false,
            InitialDirectory = Directory.Exists(TransferDownloadFolder)
                ? TransferDownloadFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            TransferDownloadFolder = dialog.FolderName;
            SetConvertTransferStatus($"Transfer indirme klasörü seçildi: {TransferDownloadFolder}", false);
        }
    }

    private void OpenConvertResult(object? parameter)
    {
        if (parameter is not ConvertFileItem file)
        {
            return;
        }

        var preferredPath = !string.IsNullOrWhiteSpace(file.OutputPath) && File.Exists(file.OutputPath)
            ? file.OutputPath
            : file.FullPath;

        if (string.IsNullOrWhiteSpace(preferredPath) || !File.Exists(preferredPath))
        {
            SetConvertStatus("Açılacak kaynak veya çıktı dosyası bulunamadı.", true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = preferredPath,
                UseShellExecute = true,
                Verb = "open"
            });

            SetConvertStatus($"Dosya açıldı: {Path.GetFileName(preferredPath)}", false);
        }
        catch (Exception ex)
        {
            SetConvertStatus($"Dosya açılamadı: {ex.Message}", true);
        }
    }

    private bool CanUseConvertResult(object? parameter)
    {
        return ResolveConvertResultTargets(parameter).Count > 0;
    }

    private List<ConvertFileItem> ResolveConvertResultTargets(object? parameter)
    {
        if (parameter is not ConvertFileItem clickedItem)
        {
            return new List<ConvertFileItem>();
        }

        static bool HasPrintableOutput(ConvertFileItem item)
        {
            return !string.IsNullOrWhiteSpace(item.OutputPath) && File.Exists(item.OutputPath);
        }

        var selectedItems = ConvertFiles
            .Where(item => item.IsSelectedForPrintPool && HasPrintableOutput(item))
            .ToList();

        if (clickedItem.IsSelectedForPrintPool && selectedItems.Count > 0)
        {
            return selectedItems;
        }

        return HasPrintableOutput(clickedItem)
            ? new List<ConvertFileItem> { clickedItem }
            : new List<ConvertFileItem>();
    }

    private void SendConvertResultToPrintPool(object? parameter)
    {
        var targets = ResolveConvertResultTargets(parameter);
        if (targets.Count == 0)
        {
            SetConvertStatus("Yazdırma havuzuna aktarılacak dönüştürülmüş çıktı bulunamadı.", true);
            return;
        }

        var addedCount = 0;
        var skippedFiles = new List<string>();

        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.OutputPath) || !File.Exists(target.OutputPath))
            {
                skippedFiles.Add(target.OutputName);
                continue;
            }

            var fileInfo = new FileInfo(target.OutputPath);
            if (fileInfo.Length > MaxUploadSizeBytes)
            {
                skippedFiles.Add($"{fileInfo.Name} ({UploadFileItem.FormatSize(fileInfo.Length)})");
                continue;
            }

            var safeUploadName = SanitizeFileName(fileInfo.Name);
        if (!string.Equals(safeUploadName, fileInfo.Name, StringComparison.Ordinal))
        {
            SetUploadStatus($"{fileInfo.Name} dosya adında geçersiz karakter var. Güvenli ad: {safeUploadName}", true);
            return;
        }

        var existing = UploadFiles.FirstOrDefault(file => string.Equals(file.FullPath, fileInfo.FullName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                skippedFiles.Add($"{fileInfo.Name} zaten havuzda");
                continue;
            }

            UploadFiles.Add(UploadFileItem.FromFileInfo(fileInfo));
            addedCount++;
        }

        NavigateToPage(PagePrint);

        if (addedCount > 0)
        {
            var message = skippedFiles.Count == 0
                ? $"{addedCount} dönüştürülmüş çıktı yazdırma havuzuna eklendi."
                : $"{addedCount} çıktı yazdırma havuzuna eklendi. Atlanan: {string.Join(", ", skippedFiles)}.";
            SetUploadStatus(message, skippedFiles.Count > 0);
            SetConvertStatus(message, skippedFiles.Count > 0);
        }
        else
        {
            var message = skippedFiles.Count == 0
                ? "Yazdırma havuzuna eklenecek çıktı bulunamadı."
                : $"Çıktılar yazdırma havuzuna eklenemedi. Atlanan: {string.Join(", ", skippedFiles)}.";
            SetUploadStatus(message, true);
            SetConvertStatus(message, true);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task PrintConvertResultAsync(object? parameter)
    {
        if (IsPrinting)
        {
            return;
        }

        var targets = ResolveConvertResultTargets(parameter);
        if (targets.Count == 0)
        {
            SetConvertStatus("Yazdırılacak dönüştürülmüş çıktı bulunamadı.", true);
            return;
        }

        var printer = ResolveDefaultPrintPrinter();
        if (printer is null)
        {
            SetConvertStatus("Varsayılan yazıcı bulunamadı. Ayarlar bölümünden yazıcı ekleyin veya Windows varsayılan yazıcısını kontrol edin.", true);
            return;
        }

        SavePrintSettings();

        var printerQueueName = printer.QueueValue;
        var filesToPrint = targets
            .Where(item => !string.IsNullOrWhiteSpace(item.OutputPath) && File.Exists(item.OutputPath))
            .Select(item => UploadFileItem.FromFileInfo(new FileInfo(item.OutputPath)))
            .ToList();

        if (filesToPrint.Count == 0)
        {
            SetConvertStatus("Yazdırılacak çıktı dosyası bulunamadı.", true);
            return;
        }

        var printedCount = 0;
        var failedFiles = new List<string>();
        var settingsSummary = $"{SelectedPrintColor}, {SelectedPrintSide}, {GetCopyCount()} kopya, {SelectedPaperSize}, {SelectedMarginProfile}, {SelectedScaleMode}, {SelectedOrientation}";
        var job = CreateOperationJob("Yazdırma", $"Convert çıktısı yazdırma", $"{filesToPrint.Count} çıktı • {printerQueueName}");
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsPrinting = true;
        SetConvertStatus($"{filesToPrint.Count} dönüştürülmüş çıktı varsayılan yazıcıya gönderiliyor: {printerQueueName}.", false);

        try
        {
            for (var index = 0; index < filesToPrint.Count; index++)
            {
                var file = filesToPrint[index];
                job.CancellationToken.ThrowIfCancellationRequested();
                job.UpdateProgress((int)Math.Round(index * 100d / Math.Max(1, filesToPrint.Count)), $"Yazdırılıyor: {file.FileName}");

                if (!File.Exists(file.FullPath))
                {
                    failedFiles.Add(file.FileName);
                    AddPrintHistory(file, printerQueueName, "Hata");
                    continue;
                }

                SetConvertStatus($"Yazdırılıyor: {file.FileName}", false);

                (bool isPrinted, string printError) printResult;
                try
                {
                    printResult = await RunOnStaThreadAsync(() =>
                    {
                        var result = TryPrintFileSilently(file.FullPath, printer, out var error);
                        return (result, error);
                    }, job.CancellationToken);
                }
                catch (Exception ex)
                {
                    printResult = (false, ex.Message);
                }

                if (printResult.isPrinted)
                {
                    printedCount++;
                    AddPrintHistory(file, printerQueueName, "Yazdırıldı");
                }
                else
                {
                    failedFiles.Add($"{file.FileName}{(string.IsNullOrWhiteSpace(printResult.printError) ? string.Empty : $" ({printResult.printError})")}");
                    AddPrintHistory(file, printerQueueName, "Hata");
                }

                job.UpdateProgress((int)Math.Round((index + 1) * 100d / Math.Max(1, filesToPrint.Count)), $"Tamamlandı: {file.FileName}");
            }
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled("Convert çıktısı yazdırma iptal edildi.");
            SetConvertStatus("Yazdırma iptal edildi.", true);
            return;
        }
        finally
        {
            IsPrinting = false;
            ReleaseJobSlot();
            CommandManager.InvalidateRequerySuggested();
        }

        if (printedCount == 0)
        {
            var errorMessage = failedFiles.Count > 0
                ? $"Çıktılar yazdırılamadı. Detay: {string.Join(", ", failedFiles)}"
                : "Çıktılar yazdırılamadı. PDF için SumatraPDF veya PDF Direct destekli yazıcı gerekir.";
            job.MarkError(errorMessage);
            SetConvertStatus(errorMessage, true);
            ShowPrintNotification("Yazdırma tamamlanamadı", errorMessage, MessageBoxImage.Warning);
            return;
        }

        if (failedFiles.Count > 0)
        {
            var warningMessage = $"{printedCount} çıktı {printerQueueName} yazıcısına gönderildi. Yazdırılamayan: {string.Join(", ", failedFiles)}. Ayarlar: {settingsSummary}.";
            job.MarkCompleted(warningMessage);
            SetConvertStatus(warningMessage, true);
            ShowPrintNotification("Yazdırma kısmen tamamlandı", warningMessage, MessageBoxImage.Warning);
            return;
        }

        var successMessage = $"{printedCount} çıktı {printerQueueName} yazıcısına gönderildi. Ayarlar: {settingsSummary}.";
        job.MarkCompleted(successMessage);
        SetConvertStatus(successMessage, false);
        ShowPrintNotification("Yazdırma tamamlandı", successMessage, MessageBoxImage.Information);
    }

    private PrinterDeviceItem? ResolveDefaultPrintPrinter()
    {
        if (RegisteredPrinters.FirstOrDefault(printer => printer.IsDefault) is { } defaultPrinter)
        {
            return defaultPrinter;
        }

        if (SelectedPrintPrinter is not null)
        {
            return SelectedPrintPrinter;
        }

        if (RegisteredPrinters.FirstOrDefault() is { } firstPrinter)
        {
            return firstPrinter;
        }

        try
        {
            using var printServer = new LocalPrintServer();
            var defaultQueue = printServer.DefaultPrintQueue;
            var queueName = string.IsNullOrWhiteSpace(defaultQueue.FullName)
                ? defaultQueue.Name
                : defaultQueue.FullName;

            if (string.IsNullOrWhiteSpace(queueName))
            {
                return null;
            }

            return new PrinterDeviceItem
            {
                Name = queueName,
                QueueName = queueName,
                IpAddress = string.Empty,
                IsDefault = true
            };
        }
        catch
        {
            return null;
        }
    }

    private void SelectTransferRecipient(object? parameter)
    {
        if (parameter is not OnlineUserItem user)
        {
            SetTransferStatus("Seçilecek personel bulunamadı.", true);
            return;
        }

        if (string.Equals(user.PresenceId, _currentPresenceId, StringComparison.OrdinalIgnoreCase))
        {
            SetTransferStatus("Kendi kullanıcınız alıcı olarak seçilemez.", true);
            return;
        }

        if (!user.IsOnline)
        {
            SetTransferStatus($"{user.DisplayName} şu anda online değil.", true);
            return;
        }

        if (!user.IsTransferReceiveEnabled)
        {
            SetTransferStatus($"{user.DisplayName} transfer alımına izin vermediği için gönderim yapılamaz.", true);
            return;
        }

        SelectedTransferRecipient = user;
        SetTransferStatus($"{user.DisplayName} alıcı olarak seçildi.", false);
    }

    public void AddConvertTransferFiles(IEnumerable<string> filePaths)
    {
        var addedCount = 0;

        foreach (var filePath in filePaths.Where(File.Exists))
        {
            if (TryAddConvertTransferFile(filePath))
            {
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            SetConvertTransferStatus($"{addedCount} dosya gönderim kutusuna eklendi.", false);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private bool TryAddConvertTransferFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            SetConvertTransferStatus("Gönderilecek dosya bulunamadı.", true);
            return false;
        }

        var safeTransferName = SanitizeFileName(fileInfo.Name);
        if (!string.Equals(safeTransferName, fileInfo.Name, StringComparison.Ordinal))
        {
            SetConvertTransferStatus($"{fileInfo.Name} dosya adında geçersiz karakter var. Güvenli ad: {safeTransferName}", true);
            return false;
        }

        var isLargeTransfer = fileInfo.Length > MaxUploadSizeBytes;

        var existing = ConvertTransferFiles.FirstOrDefault(file => string.Equals(file.FullPath, fileInfo.FullName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SetConvertTransferStatus($"{fileInfo.Name} zaten gönderim kutusunda.", false);
            return false;
        }

        var transferFile = TransferFileItem.FromFileInfo(fileInfo);
        if (isLargeTransfer)
        {
            transferFile.StatusText = "Büyük dosya • Parçalı aktarım";
        }

        ConvertTransferFiles.Add(transferFile);
        return true;
    }

    private void RemoveConvertTransferFile(object? parameter)
    {
        if (parameter is TransferFileItem file)
        {
            ConvertTransferFiles.Remove(file);
            SetConvertTransferStatus($"{file.FileName} gönderim kutusundan kaldırıldı.", false);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ClearConvertTransferFiles()
    {
        ConvertTransferFiles.Clear();
        SetConvertTransferStatus("Gönderim kutusu temizlendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanSendConvertTransferFilesToUser(object? parameter)
    {
        if (IsTransferInProgress || ConvertTransferFiles.Count == 0)
        {
            return false;
        }

        return parameter switch
        {
            OnlineUserItem recipient => recipient.CanReceiveTransfers &&
                                        !string.Equals(recipient.PresenceId, _currentPresenceId, StringComparison.OrdinalIgnoreCase),
            string recipientName => !string.IsNullOrWhiteSpace(recipientName),
            _ => false
        };
    }

    private async Task SendConvertTransferFilesToUserAsync(object? parameter)
    {
        if (!TryResolveTransferRecipient(parameter, out var recipient, out var errorMessage))
        {
            SetConvertTransferStatus(errorMessage, true);
            return;
        }

        if (parameter is not OnlineUserItem recipientUser)
        {
            SetConvertTransferStatus("Online transfer için ağdaki kullanıcı listesinden geçerli bir alıcı seçin.", true);
            return;
        }

        if (string.Equals(recipientUser.PresenceId, _currentPresenceId, StringComparison.OrdinalIgnoreCase))
        {
            SetConvertTransferStatus("Kendi kullanıcınıza online transfer gönderilemez. Kendinize dosya kopyalamak için dosyayı doğrudan hedef klasöre taşıyın.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(recipientUser.IpAddress))
        {
            SetConvertTransferStatus($"{recipient} kullanıcısının ağ adresi bulunamadı. Kullanıcı listesinin yenilenmesini bekleyin.", true);
            return;
        }

        if (_fileTransferService is null)
        {
            StartLanFileTransfer();
        }

        if (_fileTransferService is null)
        {
            SetConvertTransferStatus("Dosya transfer servisi başlatılamadı.", true);
            return;
        }

        if (ConvertTransferFiles.Count == 0)
        {
            SetConvertTransferStatus("Önce gönderilecek dosyaları listeye ekleyin.", true);
            return;
        }

        var filesToSend = ConvertTransferFiles.ToList();

        var job = CreateOperationJob("Transfer", $"{recipient} kullanıcısına gönderim", $"{filesToSend.Count} dosya • {UploadFileItem.FormatSize(filesToSend.Sum(file => file.SizeBytes))}");
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsTransferInProgress = true;

        try
        {
            foreach (var file in filesToSend)
            {
                job.CancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(file.FullPath))
                {
                    file.MarkError("Kaynak yok");
                    throw new FileNotFoundException($"Dosya bulunamadı: {file.FileName}", file.FullPath);
                }
            }

            SetConvertTransferStatus($"{filesToSend.Count} dosya {recipient} için checksum hazırlanıyor.", false);

            for (var index = 0; index < filesToSend.Count; index++)
            {
                var file = filesToSend[index];
                job.CancellationToken.ThrowIfCancellationRequested();
                file.MarkSending(5);
                job.UpdateProgress((int)Math.Round(index * 100d / Math.Max(1, filesToSend.Count)), $"Checksum hazırlanıyor: {file.FileName}");
                file.SourceChecksum = await CalculateSha256Async(file.FullPath, job.CancellationToken);
                file.MarkSending(10);
            }

            SetConvertTransferStatus($"{filesToSend.Count} dosya {recipient} bilgisayarına ağ üzerinden gönderiliyor.", false);

            var request = new LanFileTransferRequest(
                _currentPresenceId,
                GetCurrentUserDisplayName(),
                recipientUser.PresenceId,
                recipient,
                filesToSend);

            var activeFileIndex = 0;
            await _fileTransferService.SendTransferAsync(
                recipientUser.IpAddress,
                request,
                (file, fileProgress) =>
                {
                    var fileIndex = Math.Max(0, filesToSend.IndexOf(file));
                    if (fileIndex >= 0)
                    {
                        activeFileIndex = fileIndex;
                    }

                    file.MarkSending(fileProgress);
                    var totalProgress = filesToSend.Count <= 0
                        ? fileProgress
                        : Math.Clamp((int)Math.Round(((activeFileIndex * 100d) + fileProgress) / filesToSend.Count), 0, 99);
                    job.UpdateProgress(totalProgress, $"Gönderiliyor: {file.FileName}");
                },
                job.CancellationToken);

            foreach (var file in filesToSend)
            {
                file.MarkCompleted(file.FullPath);
                TransferHistory.Insert(0, TransferHistoryItem.Outgoing(file.FileName, recipient, file.FullPath));
            }

            ConvertTransferFiles.Clear();
            job.MarkCompleted($"{filesToSend.Count} dosya alıcı bilgisayara gönderildi.");
            SetConvertTransferStatus($"{filesToSend.Count} dosya {recipient} kullanıcısının bilgisayarına gönderildi. Alıcı kendi indirme klasöründen kabul edecek.", false);
        }
        catch (OperationCanceledException)
        {
            foreach (var file in filesToSend)
            {
                file.MarkError("İptal edildi");
            }
            job.MarkCancelled("Gönderim kullanıcı tarafından iptal edildi.");
            SetConvertTransferStatus("Transfer gönderimi iptal edildi.", true);
        }
        catch (Exception ex)
        {
            job.MarkError(ex.Message);
            AppLogger.LogException("Transfer gönderimi başarısız", ex);
            SetConvertTransferStatus($"Gönderim başarısız: {ex.Message}", true);
        }
        finally
        {
            IsTransferInProgress = false;
            ReleaseJobSlot();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void MarkIncomingTransferNotificationsSeen()
    {
        foreach (var transfer in PendingIncomingTransfers)
        {
            if (IsTransferIncomingForCurrentUser(transfer))
            {
                ClearTransferNotificationForTransfer(transfer);
            }
        }
    }

    private void OpenIncomingTransfer(object? parameter)
    {
        if (parameter is not PendingTransferItem transfer)
        {
            return;
        }

        ClearTransferNotificationForTransfer(transfer);
        SelectedIncomingTransfer = transfer;
        IsIncomingTransferModalOpen = true;
    }

    private void CloseIncomingTransferModal()
    {
        IsIncomingTransferModalOpen = false;
    }

    private async Task AcceptSelectedIncomingTransferAsync()
    {
        var transfer = SelectedIncomingTransfer;
        if (transfer is null)
        {
            return;
        }

        var destinationFolder = string.IsNullOrWhiteSpace(transfer.DestinationFolder)
            ? TransferDownloadFolder.Trim()
            : transfer.DestinationFolder.Trim();

        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            SetTransferStatus("Transfer kabul edilemedi. İndirme klasörü seçili değil.", true);
            return;
        }

        var job = CreateOperationJob("Transfer", $"{transfer.SenderName} gelen transfer", $"{transfer.FileCountText} • {transfer.TotalSizeText}");
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsTransferInProgress = true;

        try
        {
            Directory.CreateDirectory(destinationFolder);
            CleanupStaleTransferTempFiles(destinationFolder);
            SetTransferStatus($"{transfer.FileCountText} indiriliyor. Lütfen aktarım tamamlanana kadar bekleyin.", false);

            var fileIndex = 0;
            foreach (var file in transfer.Files)
            {
                job.CancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(file.FullPath))
                {
                    file.MarkError("Kaynak yok");
                    throw new FileNotFoundException($"Kaynak dosya bulunamadı: {file.FileName}", file.FullPath);
                }

                var destinationPath = GetUniqueDestinationPath(destinationFolder, file.FileName);
                file.MarkReceiving(0);
                job.UpdateProgress((int)Math.Round(fileIndex * 100d / Math.Max(1, transfer.Files.Count)), $"İndiriliyor: {file.FileName}");
                await CopyFileWithProgressAsync(file.FullPath, destinationPath, file, job.CancellationToken, verifyChecksum: true);
                file.MarkCompleted(destinationPath);
                TransferHistory.Insert(0, TransferHistoryItem.Incoming(file.FileName, transfer.SenderName, destinationPath));
                fileIndex++;
                job.UpdateProgress((int)Math.Round(fileIndex * 100d / Math.Max(1, transfer.Files.Count)), $"Tamamlandı: {file.FileName}");
            }

            PendingIncomingTransfers.Remove(transfer);
            CleanupIncomingTransferStaging(transfer);
            ClearTransferNotificationForTransfer(transfer);
            job.MarkCompleted($"{transfer.FileCountText} kaydedildi: {destinationFolder}");
            SetTransferStatus($"{transfer.FileCountText} kabul edildi ve kaydedildi: {destinationFolder}", false);
            IsIncomingTransferModalOpen = false;
            SelectedIncomingTransfer = null;
        }
        catch (OperationCanceledException)
        {
            foreach (var file in transfer.Files)
            {
                file.MarkError("İptal edildi");
            }
            job.MarkCancelled("Gelen transfer kullanıcı tarafından iptal edildi.");
            SetTransferStatus("Gelen transfer iptal edildi.", true);
        }
        catch (Exception ex)
        {
            job.MarkError(ex.Message);
            AppLogger.LogException("Gelen transfer kabul edilemedi", ex);
            SetTransferStatus($"Transfer kabul edilemedi: {ex.Message}", true);
        }
        finally
        {
            IsTransferInProgress = false;
            ReleaseJobSlot();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RejectSelectedIncomingTransfer()
    {
        var transfer = SelectedIncomingTransfer;
        if (transfer is null)
        {
            return;
        }

        PendingIncomingTransfers.Remove(transfer);
        CleanupIncomingTransferStaging(transfer);
        ClearTransferNotificationForTransfer(transfer);
        SetTransferStatus($"{transfer.SenderName} tarafından gönderilen transfer reddedildi.", false);
        IsIncomingTransferModalOpen = false;
        SelectedIncomingTransfer = null;
        CommandManager.InvalidateRequerySuggested();
    }

    private void ClearPendingIncomingTransfers()
    {
        if (PendingIncomingTransfers.Count == 0)
        {
            return;
        }

        foreach (var transfer in PendingIncomingTransfers.ToList())
        {
            CleanupIncomingTransferStaging(transfer);
        }

        PendingIncomingTransfers.Clear();

        foreach (var user in OnlineUsers)
        {
            user.ClearTransferNotification();
        }

        IsIncomingTransferModalOpen = false;
        SelectedIncomingTransfer = null;
        SetTransferStatus("Gelen transfer bildirimleri temizlendi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanUseTransferHistoryFile(object? parameter)
    {
        return parameter is TransferHistoryItem item &&
               !string.IsNullOrWhiteSpace(item.FullPath) &&
               File.Exists(item.FullPath);
    }

    private void OpenTransferHistoryFile(object? parameter)
    {
        if (parameter is not TransferHistoryItem item || string.IsNullOrWhiteSpace(item.FullPath) || !File.Exists(item.FullPath))
        {
            SetTransferStatus("Açılacak transfer dosyası bulunamadı.", true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FullPath,
                UseShellExecute = true,
                Verb = "open"
            });
            SetTransferStatus($"Dosya açıldı: {item.FileName}", false);
        }
        catch (Exception ex)
        {
            SetTransferStatus($"Dosya açılamadı: {ex.Message}", true);
        }
    }

    private void SendTransferHistoryToPrintPool(object? parameter)
    {
        if (parameter is not TransferHistoryItem item || string.IsNullOrWhiteSpace(item.FullPath) || !File.Exists(item.FullPath))
        {
            SetTransferStatus("Yazdırma havuzuna alınacak transfer dosyası bulunamadı.", true);
            return;
        }

        var fileInfo = new FileInfo(item.FullPath);
        if (fileInfo.Length > MaxUploadSizeBytes)
        {
            SetTransferStatus($"{fileInfo.Name} yazdırma havuzuna eklenemedi. Dosya boyutu {UploadFileItem.FormatSize(fileInfo.Length)}.", true);
            return;
        }

        if (UploadFiles.Any(file => string.Equals(file.FullPath, fileInfo.FullName, StringComparison.OrdinalIgnoreCase)))
        {
            SetTransferStatus($"{fileInfo.Name} zaten yazdırma havuzunda.", false);
        }
        else
        {
            UploadFiles.Add(UploadFileItem.FromFileInfo(fileInfo));
            SetTransferStatus($"{fileInfo.Name} yazdırma havuzuna eklendi.", false);
        }

        NavigateToPage(PagePrint);
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task PrintTransferHistoryFileAsync(object? parameter)
    {
        if (parameter is not TransferHistoryItem item || string.IsNullOrWhiteSpace(item.FullPath) || !File.Exists(item.FullPath))
        {
            SetTransferStatus("Yazdırılacak transfer dosyası bulunamadı.", true);
            return;
        }

        var printer = ResolveDefaultPrintPrinter();
        if (printer is null)
        {
            SetTransferStatus("Varsayılan yazıcı bulunamadı. Ayarlar bölümünden yazıcı ekleyin veya Windows varsayılan yazıcısını kontrol edin.", true);
            return;
        }

        var job = CreateOperationJob("Yazdırma", $"Transfer geçmişi yazdırma", item.FileName);
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsPrinting = true;
        try
        {
            job.CancellationToken.ThrowIfCancellationRequested();
            job.UpdateProgress(20, $"Yazdırılıyor: {item.FileName}");
            var printResult = await RunOnStaThreadAsync(() =>
            {
                var result = TryPrintFileSilently(item.FullPath, printer, out var error);
                return (result, error);
            }, job.CancellationToken);

            job.CancellationToken.ThrowIfCancellationRequested();
            var uploadItem = UploadFileItem.FromFileInfo(new FileInfo(item.FullPath));
            if (printResult.Item1)
            {
                job.MarkCompleted($"{item.FileName} yazıcıya gönderildi.");
                AddPrintHistory(uploadItem, printer.QueueValue, "Yazdırıldı");
                SetTransferStatus($"{item.FileName} varsayılan yazıcıya gönderildi: {printer.QueueValue}", false);
            }
            else
            {
                job.MarkError(printResult.Item2);
                AddPrintHistory(uploadItem, printer.QueueValue, "Hata");
                SetTransferStatus($"{item.FileName} yazdırılamadı: {printResult.Item2}", true);
            }
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled("Yazdırma kullanıcı tarafından iptal edildi.");
            SetTransferStatus("Yazdırma iptal edildi.", true);
        }
        catch (Exception ex)
        {
            job.MarkError(ex.Message);
            AppLogger.LogException("Transfer geçmişi doğrudan yazdırılamadı", ex);
            SetTransferStatus($"{item.FileName} yazdırılamadı: {ex.Message}", true);
        }
        finally
        {
            IsPrinting = false;
            ReleaseJobSlot();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void LoadTransferHistory()
    {
        try
        {
            if (!File.Exists(TransferHistoryStorePath))
            {
                return;
            }

            var json = File.ReadAllText(TransferHistoryStorePath);
            var records = JsonSerializer.Deserialize<List<TransferHistoryRecord>>(json) ?? new List<TransferHistoryRecord>();

            foreach (var record in records
                         .Where(record => !string.IsNullOrWhiteSpace(record.FileName))
                         .Take(500))
            {
                TransferHistory.Add(new TransferHistoryItem
                {
                    DateText = string.IsNullOrWhiteSpace(record.DateText) ? DateTime.Now.ToString("dd.MM.yyyy HH:mm") : record.DateText,
                    DirectionText = string.IsNullOrWhiteSpace(record.DirectionText) ? "Transfer" : record.DirectionText,
                    FileName = record.FileName,
                    Personel = record.Personel ?? string.Empty,
                    StatusText = string.IsNullOrWhiteSpace(record.StatusText) ? "Tamamlandı" : record.StatusText,
                    FullPath = record.FullPath ?? string.Empty,
                    StatusBrush = string.Equals(record.StatusText, "Hata", StringComparison.OrdinalIgnoreCase)
                        ? Solid("#DC2626")
                        : Solid("#16A34A")
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Transfer geçmişi yüklenemedi", ex);
        }
    }

    private void SaveTransferHistory()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TransferHistoryStorePath)!);
            var records = TransferHistory
                .Take(500)
                .Select(item => new TransferHistoryRecord
                {
                    DateText = item.DateText,
                    DirectionText = item.DirectionText,
                    FileName = item.FileName,
                    Personel = item.Personel,
                    StatusText = item.StatusText,
                    FullPath = item.FullPath
                })
                .ToList();

            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TransferHistoryStorePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Transfer geçmişi kaydedilemedi", ex);
        }
    }

    private void ClearTransferHistory()
    {
        TransferHistory.Clear();
        SetTransferStatus("Transfer geçmişi temizlendi.", false);
    }

    private void AddLocalOnlineUser()
    {
        var currentUser = new OnlineUserItem(_currentUserDisplayName, isOnline: true, isTransferReceiveEnabled: IsTransferReceiveEnabled)
        {
            PresenceId = _currentPresenceId,
            MachineName = Environment.MachineName?.Trim() ?? string.Empty,
            IpAddress = GetPrimaryIPv4Address(),
            LastSeen = DateTime.Now
        };

        _onlineUsersByPresenceId[_currentPresenceId] = currentUser;
        OnlineUsers.Add(currentUser);
    }

    private void StartLanPresence()
    {
        try
        {
            _presenceService = new LanPresenceService(_currentPresenceId, _currentUserDisplayName, IsTransferReceiveEnabled);
            _presenceService.UserOnline += PresenceService_UserOnline;
            _presenceService.UserOffline += PresenceService_UserOffline;
            _presenceService.Start();
            SetTransferStatus("Ağdaki online kullanıcılar dinleniyor.", false);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("LAN online kullanıcı servisi başlatılamadı", ex);
            SetTransferStatus($"Online kullanıcı servisi başlatılamadı: {ex.Message}", true);
        }
    }

    private void StartLanFileTransfer()
    {
        try
        {
            if (_fileTransferService is not null)
            {
                return;
            }

            _fileTransferService = new LanFileTransferService(_currentPresenceId, _currentUserDisplayName);
            _fileTransferService.TransferReceived += FileTransferService_TransferReceived;
            _fileTransferService.Start();
            AppLogger.Log($"LAN dosya transfer servisi başlatıldı. Port: {LanFileTransferService.TransferPort}");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("LAN dosya transfer servisi başlatılamadı", ex);
            SetTransferStatus($"Dosya transfer servisi başlatılamadı: {ex.Message}", true);
        }
    }

    private void FileTransferService_TransferReceived(object? sender, LanFileTransferReceivedEventArgs transfer)
    {
        RunOnUiThread(() => RegisterIncomingLanTransfer(transfer));
    }

    private void RegisterIncomingLanTransfer(LanFileTransferReceivedEventArgs transfer)
    {
        if (!IsTransferReceiveEnabled)
        {
            SetTransferStatus($"{transfer.SenderName} tarafından gönderilen transfer reddedildi. Transfer alımı kapalı.", true);
            return;
        }

        var destinationFolder = TransferDownloadFolder.Trim();
        var files = transfer.Files
            .Where(file => File.Exists(file.FullPath))
            .Select(file =>
            {
                var item = new TransferFileItem
                {
                    FileName = file.FileName,
                    FullPath = file.FullPath,
                    SizeBytes = file.SizeBytes,
                    SizeText = UploadFileItem.FormatSize(file.SizeBytes),
                    Extension = Path.GetExtension(file.FileName).TrimStart('.').ToUpperInvariant(),
                    SourceChecksum = file.SourceChecksum,
                    VerifiedChecksum = file.SourceChecksum
                };
                item.MarkQueued();
                return item;
            })
            .ToList();

        if (files.Count == 0)
        {
            SetTransferStatus("Gelen transfer paketi boş olduğu için listeye eklenmedi.", true);
            return;
        }

        var pendingTransfer = new PendingTransferItem(
            transfer.SenderName,
            GetCurrentUserDisplayName(),
            files,
            destinationFolder);

        PendingIncomingTransfers.Insert(0, pendingTransfer);

        var senderUser = OnlineUsers.FirstOrDefault(item =>
            string.Equals(item.PresenceId, transfer.SenderPresenceId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.DisplayName, transfer.SenderName, StringComparison.OrdinalIgnoreCase));
        senderUser?.RegisterIncomingTransfer(files.Count, destinationFolder);

        var folderText = string.IsNullOrWhiteSpace(destinationFolder)
            ? "Kabul etmeden önce transfer indirme klasörü seçin."
            : $"Kabul edilince kaydedilecek klasör: {destinationFolder}";

        SetTransferStatus($"{transfer.SenderName} kullanıcısından {files.Count} dosyalık transfer geldi. {folderText}", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void PresenceService_UserOnline(object? sender, LanPresenceUser user)
    {
        RunOnUiThread(() => UpsertOnlineUser(user));
    }

    private void PresenceService_UserOffline(object? sender, string presenceId)
    {
        RunOnUiThread(() => RemoveOnlineUser(presenceId));
    }

    private void UpsertOnlineUser(LanPresenceUser presenceUser)
    {
        if (string.IsNullOrWhiteSpace(presenceUser.InstanceId))
        {
            return;
        }

        if (string.Equals(presenceUser.InstanceId, _currentPresenceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(presenceUser.DisplayName)
            ? presenceUser.MachineName
            : presenceUser.DisplayName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Ağ kullanıcısı";
        }

        if (!_onlineUsersByPresenceId.TryGetValue(presenceUser.InstanceId, out var user))
        {
            user = new OnlineUserItem(displayName, isOnline: true, isTransferReceiveEnabled: presenceUser.IsTransferReceiveEnabled)
            {
                PresenceId = presenceUser.InstanceId
            };
            _onlineUsersByPresenceId[presenceUser.InstanceId] = user;
            OnlineUsers.Add(user);
        }

        user.DisplayName = displayName;
        user.MachineName = presenceUser.MachineName;
        user.IpAddress = presenceUser.IpAddress;
        user.LastSeen = presenceUser.LastSeen;
        user.IsOnline = true;
        user.IsTransferReceiveEnabled = presenceUser.IsTransferReceiveEnabled;

        SortOnlineUsers();
        RefreshFilteredOnlineUsers();
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveOnlineUser(string presenceId)
    {
        if (string.IsNullOrWhiteSpace(presenceId) || !_onlineUsersByPresenceId.TryGetValue(presenceId, out var user))
        {
            return;
        }

        if (ReferenceEquals(SelectedTransferRecipient, user))
        {
            SelectedTransferRecipient = null;
        }

        _onlineUsersByPresenceId.Remove(presenceId);
        OnlineUsers.Remove(user);
        RefreshFilteredOnlineUsers();
        CommandManager.InvalidateRequerySuggested();
    }

    private void PruneOfflineUsers()
    {
        RunOnUiThread(() =>
        {
            var expireBefore = DateTime.Now - OnlineUserTimeout;
            var staleIds = _onlineUsersByPresenceId
                .Where(pair => !string.Equals(pair.Key, _currentPresenceId, StringComparison.OrdinalIgnoreCase) && pair.Value.LastSeen < expireBefore)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var staleId in staleIds)
            {
                RemoveOnlineUser(staleId);
            }
        });
    }

    private void SortOnlineUsers()
    {
        var orderedUsers = OnlineUsers
            .OrderBy(user => user.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(user => user.MachineName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var targetIndex = 0; targetIndex < orderedUsers.Count; targetIndex++)
        {
            var user = orderedUsers[targetIndex];
            var currentIndex = OnlineUsers.IndexOf(user);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                OnlineUsers.Move(currentIndex, targetIndex);
            }
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private string GetCurrentUserDisplayName() => _currentUserDisplayName;

    private bool IsTransferIncomingForCurrentUser(PendingTransferItem transfer)
    {
        var currentUser = GetCurrentUserDisplayName();
        return string.Equals(transfer.RecipientName, currentUser, StringComparison.OrdinalIgnoreCase);
    }

    private void ClearTransferNotificationForTransfer(PendingTransferItem transfer)
    {
        ClearTransferNotificationForPerson(transfer.SenderName);
        ClearTransferNotificationForPerson(transfer.RecipientName);
    }

    private void ClearTransferNotificationForRecipient(string recipientName)
    {
        ClearTransferNotificationForPerson(recipientName);
    }

    private void ClearTransferNotificationForPerson(string personName)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            return;
        }

        var user = OnlineUsers.FirstOrDefault(item =>
            string.Equals(item.DisplayName, personName, StringComparison.OrdinalIgnoreCase));
        user?.ClearTransferNotification();
    }

    private string ResolveTransferDownloadFolder(OnlineUserItem? recipientUser)
    {
        // Transfer indirme klasörü yalnızca yerel kullanıcıya aittir.
        // Uzak alıcının klasörü gönderici tarafında bilinmez ve yerel klasöre fallback yapılmaz.
        return string.Equals(recipientUser?.PresenceId, _currentPresenceId, StringComparison.OrdinalIgnoreCase)
            ? TransferDownloadFolder.Trim()
            : string.Empty;
    }

    private void RefreshCurrentUserTransferSettings()
    {
        if (_onlineUsersByPresenceId.TryGetValue(_currentPresenceId, out var currentUser))
        {
            currentUser.IsTransferReceiveEnabled = IsTransferReceiveEnabled;
            currentUser.LastSeen = DateTime.Now;
            RefreshFilteredOnlineUsers();
        }

        _presenceService?.UpdateStatus(IsTransferReceiveEnabled);
    }

    private void RefreshOnlineUserDownloadFolders()
    {
        // İndirme klasörü yerel kullanıcı ayarıdır. LAN üzerindeki diğer kullanıcılara klasör yolu yayınlanmaz.
    }

    private static bool TryResolveTransferRecipient(object? parameter, out string recipient, out string errorMessage)
    {
        recipient = string.Empty;
        errorMessage = "Gönderilecek online kullanıcı seçilemedi.";

        if (parameter is OnlineUserItem user)
        {
            recipient = user.DisplayName.Trim();

            if (string.IsNullOrWhiteSpace(recipient))
            {
                errorMessage = "Gönderilecek online kullanıcı seçilemedi.";
                return false;
            }

            if (!user.IsOnline)
            {
                errorMessage = $"{recipient} şu anda online değil. Transfer yapılamaz.";
                return false;
            }

            if (!user.IsTransferReceiveEnabled)
            {
                errorMessage = $"{recipient} transfer alımına izin vermediği için gönderim yapılamaz.";
                return false;
            }

            return true;
        }

        if (parameter is string recipientName && !string.IsNullOrWhiteSpace(recipientName))
        {
            recipient = recipientName.Trim();
            return true;
        }

        return false;
    }

    private static string GetSafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "User" : result;
    }

    private static string GetUniqueDestinationPath(string folder, string fileName)
    {
        var safeFileName = SanitizeFileName(fileName);
        var destinationPath = Path.Combine(folder, safeFileName);
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var name = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        var index = 2;

        do
        {
            destinationPath = Path.Combine(folder, $"{name}_{index}{extension}");
            index++;
        }
        while (File.Exists(destinationPath));

        return destinationPath;
    }

    private static async Task CopyFileWithProgressAsync(string sourcePath, string destinationPath, TransferFileItem transferFile, CancellationToken cancellationToken, bool verifyChecksum)
    {
        var buffer = new byte[TransferChunkSizeBytes];
        var sourceInfo = new FileInfo(sourcePath);
        var totalBytes = sourceInfo.Length;
        long copiedBytes = 0;

        var destinationFolder = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var tempPath = destinationPath + $".toolbridge-{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var sourceStream = await OpenReadSharedWithRetryAsync(sourcePath, cancellationToken))
            await using (var destinationStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, TransferChunkSizeBytes, useAsync: true))
            {
                int read;
                while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    copiedBytes += read;

                    var progress = totalBytes <= 0
                        ? 100
                        : Math.Clamp((int)Math.Round(copiedBytes * 100d / totalBytes), 0, 98);

                    transferFile.MarkReceiving(progress);
                }

                await destinationStream.FlushAsync(cancellationToken);
            }

            if (verifyChecksum)
            {
                transferFile.StatusText = "Checksum doğrulanıyor";
                transferFile.Progress = 99;
                var expectedChecksum = string.IsNullOrWhiteSpace(transferFile.SourceChecksum)
                    ? await CalculateSha256Async(sourcePath, cancellationToken)
                    : transferFile.SourceChecksum;
                var destinationChecksum = await CalculateSha256Async(tempPath, cancellationToken);

                if (!string.Equals(expectedChecksum, destinationChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Checksum doğrulaması başarısız: {transferFile.FileName}");
                }

                transferFile.SourceChecksum = expectedChecksum;
                transferFile.VerifiedChecksum = destinationChecksum;
            }

            if (File.Exists(destinationPath))
            {
                destinationPath = GetUniqueDestinationPath(Path.GetDirectoryName(destinationPath) ?? string.Empty, Path.GetFileName(destinationPath));
            }

            await MoveFileWithRetryAsync(tempPath, destinationPath, cancellationToken);
            transferFile.MarkReceiving(100);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static async Task<FileStream> OpenReadSharedWithRetryAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 8;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, TransferChunkSizeBytes, useAsync: true);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
        }

        throw new IOException($"Dosya başka bir işlem tarafından kullanılıyor: {Path.GetFileName(filePath)}. Dosyayı kapatıp tekrar deneyin.");
    }

    private static async Task<string> CalculateSha256Async(string filePath, CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            await using var stream = await OpenReadSharedWithRetryAsync(filePath, cancellationToken);
            using var sha256 = SHA256.Create();
            var buffer = new byte[TransferChunkSizeBytes];

            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sha256.TransformBlock(buffer, 0, read, null, 0);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
        }, cancellationToken);
    }

    private static async Task MoveFileWithRetryAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 6;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(sourcePath, destinationPath);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void CleanupStaleTransferTempFiles(string folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var threshold = DateTime.Now.AddDays(-1);
            foreach (var file in Directory.GetFiles(folder, "*.toolbridge-*.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < threshold)
                    {
                        info.Delete();
                    }
                }
                catch
                {
                    // Geçici dosya kilitliyse bir sonraki temizlikte yeniden denenir.
                }
            }
        }
        catch
        {
            // Temizlik hatası transfer akışını durdurmamalı.
        }
    }

    private static void CleanupIncomingTransferStaging(PendingTransferItem transfer)
    {
        foreach (var file in transfer.Files)
        {
            if (!IsPathInsideDirectory(file.FullPath, IncomingTransferStagingRoot))
            {
                continue;
            }

            var parentFolder = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
            TryDeleteFile(file.FullPath);

            try
            {
                if (!string.IsNullOrWhiteSpace(parentFolder) &&
                    Directory.Exists(parentFolder) &&
                    !Directory.EnumerateFileSystemEntries(parentFolder).Any())
                {
                    Directory.Delete(parentFolder);
                }
            }
            catch
            {
                // Staging klasörü kilitliyse sonraki temizliğe bırakılır.
            }
        }
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

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

    private static void TryDeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Geçici dosya kilitliyse sonraki sistem temizliğine bırakılır.
        }
    }

    private void SetTransferStatus(string message, bool isError)
    {
        TransferStatusMessage = message;
        TransferStatusBrush = isError ? Solid("#DC2626") : Solid("#777783");
    }

    private void SetConvertTransferStatus(string message, bool isError)
    {
        ConvertTransferStatusMessage = message;
        ConvertTransferStatusBrush = isError ? Solid("#DC2626") : Solid("#777783");
        SetTransferStatus(message, isError);
    }

    private bool CanMergePdfFiles()
    {
        return !IsMergingPdf &&
               PdfMergeFiles.Count >= 2 &&
               !string.IsNullOrWhiteSpace(ConvertOutputFolder) &&
               !string.IsNullOrWhiteSpace(PdfMergeOutputFileName);
    }

    private async Task MergePdfFilesAsync()
    {
        if (IsMergingPdf)
        {
            return;
        }

        if (PdfMergeFiles.Count < 2)
        {
            SetPdfMergeStatus("PDF birleştirmek için en az 2 PDF dosyası seçin.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(ConvertOutputFolder))
        {
            SetPdfMergeStatus("PDF birleştirmek için önce çıktı klasörü seçin.", true);
            return;
        }

        var filesToMerge = PdfMergeFiles.ToList();
        var outputFolder = ConvertOutputFolder.Trim();
        var outputFileName = SanitizeFileName((PdfMergeOutputFileName ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(outputFileName))
        {
            outputFileName = "Birlesik_PDF.pdf";
        }

        if (!outputFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            outputFileName += ".pdf";
        }

        var outputBaseName = Path.GetFileNameWithoutExtension(outputFileName);
        var outputPath = GetUniqueOutputPath(outputFolder, outputBaseName, ".pdf");
        var job = CreateOperationJob("PDF", "PDF birleştirme", $"{filesToMerge.Count} dosya");
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsMergingPdf = true;
        SetPdfMergeStatus($"{filesToMerge.Count} PDF birleştiriliyor. İş kuyruğundan iptal edilebilir.", false);

        try
        {
            Directory.CreateDirectory(outputFolder);

            foreach (var file in filesToMerge)
            {
                if (!File.Exists(file.FullPath))
                {
                    file.MarkError("Dosya bulunamadı");
                    job.MarkError($"{file.FileName} bulunamadı.");
                    SetPdfMergeStatus($"{file.FileName} bulunamadı.", true);
                    return;
                }

                file.MarkConverting();
            }

            (bool isMerged, int pageCount, string error) result;
            try
            {
                result = await RunOnStaThreadAsync(() =>
                {
                    var merged = TryMergePdfFilesAsRenderedPdf(filesToMerge.Select(file => file.FullPath).ToArray(), outputPath, job.CancellationToken,
                        (progress, message) => job.UpdateProgress(progress, message), out var pageCount, out var error);
                    return (merged, pageCount, error);
                }, job.CancellationToken);
            }
            catch (Exception ex)
            {
                result = (false, 0, ex.Message);
            }

            job.CancellationToken.ThrowIfCancellationRequested();

            if (!result.isMerged)
            {
                var errorText = string.IsNullOrWhiteSpace(result.error) ? "PDF birleştirilemedi" : result.error;
                foreach (var file in filesToMerge)
                {
                    file.MarkError(errorText);
                }

                job.MarkError(errorText);
                SetPdfMergeStatus(errorText, true);
                return;
            }

            foreach (var file in filesToMerge)
            {
                file.MarkCompleted(outputPath);
            }

            AddConvertTransferFiles(new[] { outputPath });
            job.MarkCompleted($"{filesToMerge.Count} PDF, {result.pageCount} sayfa olarak birleştirildi.");
            SetPdfMergeStatus($"PDF başarıyla birleştirildi: {outputPath}", false);
        }
        catch (OperationCanceledException)
        {
            foreach (var file in filesToMerge.Where(file => file.Progress < 100))
            {
                file.MarkError("İptal edildi");
            }

            job.MarkCancelled("PDF birleştirme kullanıcı tarafından iptal edildi.");
            SetPdfMergeStatus("PDF birleştirme iptal edildi.", true);
        }
        catch (Exception ex)
        {
            job.MarkError(ex.Message);
            AppLogger.LogException("PDF birleştirme işlemi başarısız", ex);
            SetPdfMergeStatus($"PDF birleştirme hatası: {ex.Message}", true);
        }
        finally
        {
            IsMergingPdf = false;
            ReleaseJobSlot();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool CanConvertDocuments()
    {
        return !IsConverting && ConvertFiles.Count > 0 && !string.IsNullOrWhiteSpace(SelectedConvertTargetFormat) && !string.IsNullOrWhiteSpace(ConvertOutputFolder);
    }

    private async Task ConvertDocumentsAsync()
    {
        if (IsConverting)
        {
            return;
        }

        if (ConvertFiles.Count == 0)
        {
            SetConvertStatus("Dönüştürmek için önce dosya seçin.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(ConvertOutputFolder))
        {
            SetConvertStatus("Dönüştürmek için önce çıktı klasörü seçin.", true);
            return;
        }

        var filesToConvert = ConvertFiles.ToList();
        var targetFormat = SelectedConvertTargetFormat.Trim().ToUpperInvariant();
        var outputFolder = ConvertOutputFolder.Trim();
        var convertedCount = 0;
        var failedFiles = new List<string>();
        var job = CreateOperationJob("Convert", $"{targetFormat} dönüştürme", $"{filesToConvert.Count} dosya");
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsConverting = true;
        SetConvertStatus($"{filesToConvert.Count} dosya {targetFormat} formatına dönüştürülüyor. İş kuyruğundan iptal edilebilir.", false);

        try
        {
            Directory.CreateDirectory(outputFolder);

            for (var index = 0; index < filesToConvert.Count; index++)
            {
                var file = filesToConvert[index];
                job.CancellationToken.ThrowIfCancellationRequested();
                job.UpdateProgress((int)Math.Round(index * 100d / Math.Max(1, filesToConvert.Count)), $"Dönüştürülüyor: {file.FileName}");

                if (!File.Exists(file.FullPath))
                {
                    file.MarkError("Dosya bulunamadı");
                    failedFiles.Add(file.FileName);
                    continue;
                }

                file.MarkConverting();
                SetConvertStatus($"Dönüştürülüyor: {file.FileName}", false);

                (bool isConverted, string outputPath, string error) result;
                try
                {
                    result = await RunOnStaThreadAsync(() =>
                    {
                        var converted = TryConvertFile(file.FullPath, targetFormat, outputFolder, out var outputPath, out var error);
                        return (converted, outputPath, error);
                    }, job.CancellationToken);
                }
                catch (Exception ex)
                {
                    result = (false, string.Empty, ex.Message);
                }

                job.CancellationToken.ThrowIfCancellationRequested();

                if (result.isConverted)
                {
                    convertedCount++;
                    file.MarkCompleted(result.outputPath);
                }
                else
                {
                    var errorText = string.IsNullOrWhiteSpace(result.error) ? "Dönüştürülemedi" : result.error;
                    file.MarkError(errorText);
                    failedFiles.Add($"{file.FileName} ({errorText})");
                }

                job.UpdateProgress((int)Math.Round((index + 1) * 100d / Math.Max(1, filesToConvert.Count)), $"Tamamlandı: {file.FileName}");
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var file in filesToConvert.Where(file => file.Progress < 100))
            {
                file.MarkError("İptal edildi");
            }

            job.MarkCancelled("Dönüştürme kullanıcı tarafından iptal edildi.");
            SetConvertStatus("Dönüştürme iptal edildi.", true);
            return;
        }
        catch (Exception ex)
        {
            job.MarkError(ex.Message);
            AppLogger.LogException("Dönüştürme işlemi başarısız", ex);
            SetConvertStatus($"Dönüştürme hatası: {ex.Message}", true);
            return;
        }
        finally
        {
            IsConverting = false;
            ReleaseJobSlot();
            CommandManager.InvalidateRequerySuggested();
        }

        if (convertedCount == 0)
        {
            var errorMessage = failedFiles.Count > 0
                ? $"Dosyalar dönüştürülemedi. Detay: {string.Join(", ", failedFiles)}"
                : "Dosyalar dönüştürülemedi.";
            job.MarkError(errorMessage);
            SetConvertStatus(errorMessage, true);
            return;
        }

        if (failedFiles.Count > 0)
        {
            var warningMessage = $"{convertedCount} dosya dönüştürüldü. Dönüştürülemeyen: {string.Join(", ", failedFiles)}";
            job.MarkCompleted(warningMessage);
            SetConvertStatus(warningMessage, true);
            return;
        }

        job.MarkCompleted($"{convertedCount} dosya başarıyla dönüştürüldü.");
        SetConvertStatus($"{convertedCount} dosya başarıyla {targetFormat} formatına dönüştürüldü. Çıktı klasörü: {outputFolder}", false);
    }

    private bool CanPrintUploadedDocuments()
    {
        return !IsPrinting && UploadFiles.Count > 0 && SelectedPrintPrinter is not null;
    }

    private async Task PrintUploadedDocumentsAsync()
    {
        if (IsPrinting)
        {
            return;
        }

        if (SelectedPrintPrinter is null)
        {
            SetUploadStatus("Yazdırmak için önce Cihazlar alanından bir yazıcı seçin.", true);
            return;
        }

        if (UploadFiles.Count == 0)
        {
            SetUploadStatus("Yazdırmak için önce dosya yükleyin.", true);
            return;
        }

        SavePrintSettings();

        var printer = SelectedPrintPrinter;
        var printerQueueName = printer.QueueValue;
        var filesToPrint = UploadFiles.ToList();
        var printedCount = 0;
        var failedFiles = new List<string>();
        var settingsSummary = $"{SelectedPrintColor}, {SelectedPrintSide}, {GetCopyCount()} kopya, {SelectedPaperSize}, {SelectedMarginProfile}, {SelectedScaleMode}, {SelectedOrientation}";
        var job = CreateOperationJob("Yazdırma", $"{printerQueueName} yazdırma", $"{filesToPrint.Count} dosya");
        if (!await WaitForJobSlotAsync(job))
        {
            return;
        }

        IsPrinting = true;
        SetUploadStatus($"{filesToPrint.Count} dosya yazdırma kuyruğuna gönderiliyor. İş kuyruğundan iptal edilebilir.", false);

        try
        {
            for (var index = 0; index < filesToPrint.Count; index++)
            {
                var file = filesToPrint[index];
                job.CancellationToken.ThrowIfCancellationRequested();
                job.UpdateProgress((int)Math.Round(index * 100d / Math.Max(1, filesToPrint.Count)), $"Yazdırılıyor: {file.FileName}");

                if (!File.Exists(file.FullPath))
                {
                    failedFiles.Add(file.FileName);
                    AddPrintHistory(file, printerQueueName, "Hata");
                    continue;
                }

                SetUploadStatus($"Yazdırılıyor: {file.FileName}", false);

                (bool isPrinted, string printError) printResult;
                try
                {
                    printResult = await RunOnStaThreadAsync(() =>
                    {
                        var result = TryPrintFileSilently(file.FullPath, printer, out var error);
                        return (result, error);
                    }, job.CancellationToken);
                }
                catch (Exception ex)
                {
                    printResult = (false, ex.Message);
                }

                if (printResult.isPrinted)
                {
                    printedCount++;
                    AddPrintHistory(file, printerQueueName, "Yazdırıldı");
                }
                else
                {
                    failedFiles.Add($"{file.FileName}{(string.IsNullOrWhiteSpace(printResult.printError) ? string.Empty : $" ({printResult.printError})")}");
                    AddPrintHistory(file, printerQueueName, "Hata");
                }

                job.UpdateProgress((int)Math.Round((index + 1) * 100d / Math.Max(1, filesToPrint.Count)), $"Tamamlandı: {file.FileName}");
            }
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled("Yazdırma kullanıcı tarafından iptal edildi.");
            SetUploadStatus("Yazdırma iptal edildi.", true);
            return;
        }
        finally
        {
            IsPrinting = false;
            ReleaseJobSlot();
        }

        if (printedCount == 0)
        {
            var errorMessage = failedFiles.Count > 0
                ? $"Dosyalar yazdırılamadı. Detay: {string.Join(", ", failedFiles)}"
                : "Dosyalar yazdırılamadı. PDF için SumatraPDF veya PDF Direct destekli yazıcı gerekir.";
            job.MarkError(errorMessage);
            SetUploadStatus(errorMessage, true);
            ShowPrintNotification("Yazdırma tamamlanamadı", errorMessage, MessageBoxImage.Warning);
            return;
        }

        if (failedFiles.Count > 0)
        {
            var warningMessage = $"{printedCount} dosya {printerQueueName} yazıcısına gönderildi. Yazdırılamayan: {string.Join(", ", failedFiles)}. Ayarlar: {settingsSummary}.";
            job.MarkCompleted(warningMessage);
            SetUploadStatus(warningMessage, true);
            ShowPrintNotification("Yazdırma kısmen tamamlandı", warningMessage, MessageBoxImage.Warning);
            return;
        }

        var successMessage = $"{printedCount} dosya {printerQueueName} yazıcısına gönderildi. Ayarlar: {settingsSummary}.";
        job.MarkCompleted(successMessage);
        SetUploadStatus(successMessage, false);
        ShowPrintNotification("Yazdırma tamamlandı", successMessage, MessageBoxImage.Information);
    }



    private static bool TryMergePdfFilesAsRenderedPdf(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken cancellationToken, Action<int, string>? progressCallback, out int mergedPageCount, out string errorMessage)
    {
        mergedPageCount = 0;
        errorMessage = string.Empty;

        if (inputPaths.Count < 2)
        {
            errorMessage = "PDF birleştirmek için en az 2 dosya gerekir.";
            return false;
        }

        var pdfiumPath = FindPdfiumNativeLibrary();
        if (string.IsNullOrWhiteSpace(pdfiumPath) || !File.Exists(pdfiumPath))
        {
            errorMessage = "PDF birleştirme için pdfium.dll bulunamadı.";
            return false;
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridge", "pdf-merge-" + Guid.NewGuid().ToString("N"));
        var renderedPages = new List<RenderedPdfPageFile>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            Directory.CreateDirectory(tempFolder);

            for (var fileIndex = 0; fileIndex < inputPaths.Count; fileIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inputPath = inputPaths[fileIndex];
                progressCallback?.Invoke((int)Math.Round(fileIndex * 80d / Math.Max(1, inputPaths.Count)), $"PDF okunuyor: {Path.GetFileName(inputPath)}");

                if (!File.Exists(inputPath))
                {
                    errorMessage = $"PDF bulunamadı: {Path.GetFileName(inputPath)}";
                    return false;
                }

                if (!string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Sadece PDF dosyaları birleştirilebilir: {Path.GetFileName(inputPath)}";
                    return false;
                }

                if (!TryRenderPdfPagesToJpegFilesWithPdfiumNative(pdfiumPath, inputPath, tempFolder, fileIndex + 1, cancellationToken, renderedPages, out var renderError))
                {
                    errorMessage = renderError;
                    return false;
                }
            }

            if (renderedPages.Count == 0)
            {
                errorMessage = "Birleştirilecek PDF sayfası bulunamadı.";
                return false;
            }

            progressCallback?.Invoke(85, "Birleşik PDF yazılıyor");
            WriteMultiImagePdf(outputPath, renderedPages);
            mergedPageCount = renderedPages.Count;
            progressCallback?.Invoke(100, "PDF birleştirme tamamlandı");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempFolder);
        }
    }

    private static bool TryRenderPdfPagesToJpegFilesWithPdfiumNative(string pdfiumPath, string filePath, string tempFolder, int documentIndex, CancellationToken cancellationToken, IList<RenderedPdfPageFile> renderedPages, out string errorMessage)
    {
        errorMessage = string.Empty;
        IntPtr libraryHandle = IntPtr.Zero;
        IntPtr documentHandle = IntPtr.Zero;

        try
        {
            libraryHandle = NativeLibrary.Load(pdfiumPath);
            if (libraryHandle == IntPtr.Zero ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_InitLibrary", out var initLibraryPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadDocument", out var loadDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageCount", out var getPageCountPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadPage", out var loadPagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_ClosePage", out var closePagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_Create", out var createBitmapPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_FillRect", out var fillRectPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_RenderPageBitmap", out var renderPageBitmapPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_GetBuffer", out var getBufferPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_GetStride", out var getStridePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_Destroy", out var destroyBitmapPointer))
            {
                errorMessage = "PDFium PDF birleştirme fonksiyonları bulunamadı.";
                return false;
            }

            var initLibrary = Marshal.GetDelegateForFunctionPointer<FpdfInitLibraryDelegate>(initLibraryPointer);
            var loadDocument = Marshal.GetDelegateForFunctionPointer<FpdfLoadDocumentDelegate>(loadDocumentPointer);
            var getPageCount = Marshal.GetDelegateForFunctionPointer<FpdfGetPageCountDelegate>(getPageCountPointer);
            var loadPage = Marshal.GetDelegateForFunctionPointer<FpdfLoadPageDelegate>(loadPagePointer);
            var closePage = Marshal.GetDelegateForFunctionPointer<FpdfClosePageDelegate>(closePagePointer);
            var closeDocument = Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer);
            var createBitmap = Marshal.GetDelegateForFunctionPointer<FpdfBitmapCreateDelegate>(createBitmapPointer);
            var fillRect = Marshal.GetDelegateForFunctionPointer<FpdfBitmapFillRectDelegate>(fillRectPointer);
            var renderPageBitmap = Marshal.GetDelegateForFunctionPointer<FpdfRenderPageBitmapDelegate>(renderPageBitmapPointer);
            var getBuffer = Marshal.GetDelegateForFunctionPointer<FpdfBitmapGetBufferDelegate>(getBufferPointer);
            var getStride = Marshal.GetDelegateForFunctionPointer<FpdfBitmapGetStrideDelegate>(getStridePointer);
            var destroyBitmap = Marshal.GetDelegateForFunctionPointer<FpdfBitmapDestroyDelegate>(destroyBitmapPointer);

            initLibrary();
            documentHandle = loadDocument(filePath, null);
            if (documentHandle == IntPtr.Zero)
            {
                errorMessage = $"PDF açılamadı: {Path.GetFileName(filePath)}";
                return false;
            }

            var pageCount = Math.Max(0, getPageCount(documentHandle));
            if (pageCount == 0)
            {
                errorMessage = $"PDF sayfa sayısı okunamadı: {Path.GetFileName(filePath)}";
                return false;
            }

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IntPtr pageHandle = IntPtr.Zero;
                IntPtr bitmapHandle = IntPtr.Zero;

                try
                {
                    pageHandle = loadPage(documentHandle, pageIndex);
                    if (pageHandle == IntPtr.Zero)
                    {
                        errorMessage = $"{Path.GetFileName(filePath)} dosyasının {pageIndex + 1}. sayfası açılamadı.";
                        return false;
                    }

                    var (pageWidthPoints, pageHeightPoints) = GetPdfiumPageSizePoints(libraryHandle, pageHandle);
                    var (renderWidth, renderHeight) = GetPdfiumMergeRenderSize(pageWidthPoints, pageHeightPoints);
                    bitmapHandle = createBitmap(renderWidth, renderHeight, 0);
                    if (bitmapHandle == IntPtr.Zero)
                    {
                        errorMessage = "PDFium bitmap oluşturamadı.";
                        return false;
                    }

                    fillRect(bitmapHandle, 0, 0, renderWidth, renderHeight, 0xFFFFFFFF);
                    const int renderAnnotations = 0x01;
                    const int lcdText = 0x02;
                    renderPageBitmap(bitmapHandle, pageHandle, 0, 0, renderWidth, renderHeight, 0, renderAnnotations | lcdText);

                    var buffer = getBuffer(bitmapHandle);
                    var stride = getStride(bitmapHandle);
                    if (buffer == IntPtr.Zero || stride <= 0)
                    {
                        errorMessage = "PDFium bitmap belleği okunamadı.";
                        return false;
                    }

                    var outputBitmap = CreateWhiteBitmapSourceFromPdfiumNativeBuffer(buffer, stride, renderWidth, renderHeight);
                    if (outputBitmap.CanFreeze)
                    {
                        outputBitmap.Freeze();
                    }

                    var jpegBytes = EncodeBitmapToJpegBytes(outputBitmap, 88);
                    var jpegPath = Path.Combine(tempFolder, $"doc_{documentIndex:000}_page_{pageIndex + 1:0000}.jpg");
                    File.WriteAllBytes(jpegPath, jpegBytes);
                    renderedPages.Add(new RenderedPdfPageFile(jpegPath, renderWidth, renderHeight, pageWidthPoints, pageHeightPoints));
                }
                finally
                {
                    if (bitmapHandle != IntPtr.Zero)
                    {
                        destroyBitmap(bitmapHandle);
                    }

                    if (pageHandle != IntPtr.Zero)
                    {
                        closePage(pageHandle);
                    }
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            if (documentHandle != IntPtr.Zero)
            {
                try
                {
                    if (libraryHandle != IntPtr.Zero && NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer))
                    {
                        Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer)(documentHandle);
                    }
                }
                catch
                {
                    // Kapatma hatası PDF birleştirme sonucunu etkilemez.
                }
            }
        }
    }

    private static string FindPdfiumNativeLibrary()
    {
        var docnetPath = FindDocnetCoreAssembly();
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "pdfium.dll"),
            Path.Combine(AppContext.BaseDirectory, "pdfium.dll"),
            string.IsNullOrWhiteSpace(docnetPath) ? string.Empty : Path.Combine(Path.GetDirectoryName(docnetPath) ?? string.Empty, "pdfium.dll")
        };

        return possiblePaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)) ?? string.Empty;
    }

    private static (double Width, double Height) GetPdfiumPageSizePoints(IntPtr libraryHandle, IntPtr pageHandle)
    {
        double pageWidthPoints = 595;
        double pageHeightPoints = 842;

        try
        {
            if (NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageWidthF", out var widthFPointer) &&
                NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageHeightF", out var heightFPointer))
            {
                var getWidthF = Marshal.GetDelegateForFunctionPointer<FpdfGetPageWidthFDelegate>(widthFPointer);
                var getHeightF = Marshal.GetDelegateForFunctionPointer<FpdfGetPageHeightFDelegate>(heightFPointer);
                pageWidthPoints = Math.Max(1, getWidthF(pageHandle));
                pageHeightPoints = Math.Max(1, getHeightF(pageHandle));
            }
            else if (NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageWidth", out var widthPointer) &&
                     NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageHeight", out var heightPointer))
            {
                var getWidth = Marshal.GetDelegateForFunctionPointer<FpdfGetPageWidthDelegate>(widthPointer);
                var getHeight = Marshal.GetDelegateForFunctionPointer<FpdfGetPageHeightDelegate>(heightPointer);
                pageWidthPoints = Math.Max(1, getWidth(pageHandle));
                pageHeightPoints = Math.Max(1, getHeight(pageHandle));
            }
        }
        catch
        {
            pageWidthPoints = 595;
            pageHeightPoints = 842;
        }

        return (pageWidthPoints, pageHeightPoints);
    }

    private static (int Width, int Height) GetPdfiumMergeRenderSize(double pageWidthPoints, double pageHeightPoints)
    {
        const double targetDpi = 180d;
        const double maxSide = 2600d;
        var widthAtTargetDpi = Math.Max(1d, pageWidthPoints / 72d * targetDpi);
        var heightAtTargetDpi = Math.Max(1d, pageHeightPoints / 72d * targetDpi);
        var scale = Math.Min(1d, Math.Min(maxSide / widthAtTargetDpi, maxSide / heightAtTargetDpi));
        var width = Math.Max(1, (int)Math.Round(widthAtTargetDpi * scale));
        var height = Math.Max(1, (int)Math.Round(heightAtTargetDpi * scale));
        return (width, height);
    }

    private static void WriteMultiImagePdf(string outputPath, IReadOnlyList<RenderedPdfPageFile> pages)
    {
        using var stream = File.Create(outputPath);
        var offsets = new List<long> { 0 };
        var objectCount = 2 + pages.Count * 3;
        var pageObjectNumbers = Enumerable.Range(0, pages.Count).Select(index => 3 + index * 3).ToArray();

        WriteAscii(stream, "%PDF-1.4\n%ToolBridge PDF Merge\n");
        WritePdfObject(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WritePdfObject(stream, offsets, 2, $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] /Count {pages.Count} >>");

        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            var pageObjectNumber = 3 + index * 3;
            var contentObjectNumber = pageObjectNumber + 1;
            var imageObjectNumber = pageObjectNumber + 2;
            var pageWidth = Math.Max(1, page.PageWidthPoints);
            var pageHeight = Math.Max(1, page.PageHeightPoints);
            var pageObject = FormattableString.Invariant($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth:0.###} {pageHeight:0.###}] /Resources << /XObject << /Im0 {imageObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
            WritePdfObject(stream, offsets, pageObjectNumber, pageObject);

            var content = FormattableString.Invariant($"q\n{pageWidth:0.###} 0 0 {pageHeight:0.###} 0 0 cm\n/Im0 Do\nQ\n");
            var contentBytes = Encoding.ASCII.GetBytes(content);
            WritePdfStreamObject(stream, offsets, contentObjectNumber, "", contentBytes);

            var imageBytes = File.ReadAllBytes(page.JpegPath);
            var imageHeader = FormattableString.Invariant($"/Type /XObject /Subtype /Image /Width {page.PixelWidth} /Height {page.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode");
            WritePdfStreamObject(stream, offsets, imageObjectNumber, imageHeader, imageBytes);
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, FormattableString.Invariant($"xref\n0 {objectCount + 1}\n0000000000 65535 f \n"));
        for (var objectNumber = 1; objectNumber <= objectCount; objectNumber++)
        {
            WriteAscii(stream, FormattableString.Invariant($"{offsets[objectNumber]:0000000000} 00000 n \n"));
        }

        WriteAscii(stream, FormattableString.Invariant($"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
    }

    private static void WritePdfObject(Stream stream, IList<long> offsets, int objectNumber, string body)
    {
        while (offsets.Count <= objectNumber)
        {
            offsets.Add(0);
        }

        offsets[objectNumber] = stream.Position;
        WriteAscii(stream, FormattableString.Invariant($"{objectNumber} 0 obj\n{body}\nendobj\n"));
    }

    private static void WritePdfStreamObject(Stream stream, IList<long> offsets, int objectNumber, string dictionaryBody, byte[] streamBytes)
    {
        while (offsets.Count <= objectNumber)
        {
            offsets.Add(0);
        }

        offsets[objectNumber] = stream.Position;
        var dictionary = string.IsNullOrWhiteSpace(dictionaryBody)
            ? FormattableString.Invariant($"<< /Length {streamBytes.Length} >>")
            : FormattableString.Invariant($"<< {dictionaryBody} /Length {streamBytes.Length} >>");
        WriteAscii(stream, FormattableString.Invariant($"{objectNumber} 0 obj\n{dictionary}\nstream\n"));
        stream.Write(streamBytes, 0, streamBytes.Length);
        WriteAscii(stream, "\nendstream\nendobj\n");
    }

    private bool TryConvertFile(string filePath, string targetFormat, string outputFolder, out string outputPath, out string errorMessage)
    {
        outputPath = string.Empty;
        errorMessage = string.Empty;

        var normalizedTarget = NormalizeConvertTarget(targetFormat);
        if (string.IsNullOrWhiteSpace(normalizedTarget) || !IsKnownConvertFormat(normalizedTarget))
        {
            errorMessage = "Hedef format geçerli değil.";
            return false;
        }

        var sourceFormat = NormalizeSourceFormat(filePath);
        var targetExtension = GetTargetExtension(normalizedTarget);
        var sourceExtension = GetTargetExtension(sourceFormat);
        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(filePath));
        outputPath = GetUniqueOutputPath(outputFolder, baseName, targetExtension);

        if (FormatsAreEquivalent(sourceFormat, normalizedTarget))
        {
            File.Copy(filePath, outputPath, overwrite: false);
            return true;
        }

        var conversionErrors = new List<string>();

        if (TryConvertImageToPdfBuiltIn(filePath, sourceFormat, normalizedTarget, outputPath, out var imagePdfError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Dahili görsel PDF dönüştürücü", imagePdfError);

        if (TryConvertRasterImageBuiltIn(filePath, sourceFormat, normalizedTarget, outputPath, out var builtInImageError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Dahili görsel dönüştürücü", builtInImageError);

        if (TryConvertPdfFirstPageWithDocnet(filePath, sourceFormat, normalizedTarget, outputPath, out var pdfImageError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "PDF görsel dönüştürücü", pdfImageError);

        if (TryConvertPdfToEditableBuiltIn(filePath, sourceFormat, normalizedTarget, outputPath, out var pdfEditableError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Dahili PDF metin dönüştürücü", pdfEditableError);

        if (TryConvertOpenXmlBuiltIn(filePath, sourceFormat, normalizedTarget, outputPath, out var openXmlError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Dahili Office Open XML dönüştürücü", openXmlError);

        if (TryConvertViaUniversalContentBridge(filePath, sourceFormat, normalizedTarget, outputFolder, out var universalBridgeOutputPath, out var universalBridgeError))
        {
            outputPath = universalBridgeOutputPath;
            return true;
        }
        AddConversionError(conversionErrors, "Akıllı içerik köprüsü", universalBridgeError);

        if (TryConvertLegacyOfficeViaLibreOfficeOpenXmlBridge(filePath, sourceFormat, normalizedTarget, outputPath, out var legacyBridgeError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "LibreOffice ara dönüştürücü", legacyBridgeError);

        if (TryConvertDocumentToImageViaPdf(filePath, sourceFormat, normalizedTarget, outputPath, out var documentImageError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Belge görsel dönüştürücü", documentImageError);

        if (TryConvertArchive(filePath, sourceFormat, normalizedTarget, outputFolder, out var archiveOutputPath, out var archiveError))
        {
            outputPath = archiveOutputPath;
            return true;
        }
        AddConversionError(conversionErrors, "Arşiv", archiveError);

        if (TryConvertWithLibreOffice(filePath, sourceFormat, normalizedTarget, outputFolder, out var libreOfficeOutputPath, out var libreOfficeError))
        {
            outputPath = libreOfficeOutputPath;
            return true;
        }
        AddConversionError(conversionErrors, "LibreOffice", libreOfficeError);

        if (TryConvertWithImageMagick(filePath, sourceFormat, normalizedTarget, outputPath, out var imageMagickError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "ImageMagick", imageMagickError);

        if (TryConvertWithFFmpeg(filePath, sourceFormat, normalizedTarget, outputPath, out var ffmpegError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "FFmpeg", ffmpegError);

        if (TryConvertWithCalibre(filePath, sourceFormat, normalizedTarget, outputPath, out var calibreError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Calibre", calibreError);

        if (TryConvertWithInkscape(filePath, sourceFormat, normalizedTarget, outputPath, out var inkscapeError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Inkscape", inkscapeError);

        if (TryConvertWithFontForge(filePath, sourceFormat, normalizedTarget, outputPath, out var fontForgeError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "FontForge", fontForgeError);

        if (TryConvertWithMicrosoftOffice(filePath, sourceFormat, normalizedTarget, outputPath, out var officeError))
        {
            return true;
        }
        AddConversionError(conversionErrors, "Microsoft Office", officeError);

        if (IsTextExtension(sourceExtension) && IsTextTarget(normalizedTarget))
        {
            if (TryConvertText(filePath, outputPath, out var textError))
            {
                return true;
            }

            AddConversionError(conversionErrors, "Metin", textError);
        }

        errorMessage = conversionErrors.Count == 0
            ? "Bu dönüşüm için uygun motor bulunamadı."
            : $"Bu dönüşüm tamamlanamadı. {string.Join(" | ", conversionErrors.Take(5))}";
        return false;
    }



    private bool TryConvertViaUniversalContentBridge(string filePath, string sourceFormat, string targetFormat, string outputFolder, out string outputPath, out string errorMessage)
    {
        outputPath = string.Empty;
        errorMessage = string.Empty;

        var normalizedSource = NormalizeConvertTarget(sourceFormat);
        var normalizedTarget = NormalizeConvertTarget(targetFormat);

        if (!IsUniversalContentBridgeSource(normalizedSource) || !IsUniversalContentBridgeTarget(normalizedTarget))
        {
            return false;
        }

        if (!TryExtractUniversalBridgeContent(filePath, normalizedSource, out var lines, out var rows, out var extractError))
        {
            errorMessage = extractError;
            return false;
        }

        if (lines.Count == 0 && rows.Count > 0)
        {
            lines = rows.Select(row => string.Join("\t", row)).ToList();
        }

        if (rows.Count == 0 && lines.Count > 0)
        {
            rows = lines.Select(line => new List<string> { line }).ToList();
        }

        if (lines.Count == 0)
        {
            lines.Add("Boş içerik");
            rows.Add(new List<string> { "Boş içerik" });
        }

        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(filePath));
        var targetExtension = GetTargetExtension(normalizedTarget);
        var requestedOutputPath = GetUniqueOutputPath(outputFolder, baseName, targetExtension);

        try
        {
            Directory.CreateDirectory(outputFolder);

            switch (normalizedTarget)
            {
                case "TXT":
                    File.WriteAllLines(requestedOutputPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    outputPath = requestedOutputPath;
                    return true;
                case "CSV":
                    WriteTableRowsAsCsv(requestedOutputPath, rows);
                    outputPath = requestedOutputPath;
                    return true;
                case "HTML":
                    WriteOpenXmlContentAsHtml(requestedOutputPath, lines, rows);
                    outputPath = requestedOutputPath;
                    return true;
                case "RTF":
                case "DOC":
                    WriteLinesAsRtf(requestedOutputPath, lines);
                    outputPath = requestedOutputPath;
                    return true;
                case "DOCX":
                    WriteLinesAsDocx(requestedOutputPath, lines);
                    outputPath = requestedOutputPath;
                    return true;
                case "ODT":
                    WriteLinesAsOdt(requestedOutputPath, lines);
                    outputPath = requestedOutputPath;
                    return true;
                case "XLS":
                    WriteTableRowsAsExcelHtml(requestedOutputPath, rows);
                    outputPath = requestedOutputPath;
                    return true;
                case "XLSX":
                    WriteTableRowsAsXlsx(requestedOutputPath, rows);
                    outputPath = requestedOutputPath;
                    return true;
                case "ODS":
                    WriteRowsAsOds(requestedOutputPath, rows);
                    outputPath = requestedOutputPath;
                    return true;
                case "PDF":
                    WriteLinesAsSimplePdf(requestedOutputPath, lines);
                    outputPath = requestedOutputPath;
                    return true;
                case "PPTX":
                    WriteLinesAsPptx(requestedOutputPath, lines);
                    outputPath = requestedOutputPath;
                    return true;
                case "ODP":
                    WriteLinesAsOdp(requestedOutputPath, lines);
                    outputPath = requestedOutputPath;
                    return true;
                case "PPT":
                    return TryWritePptViaLibreOfficeBridge(lines, outputFolder, baseName, out outputPath, out errorMessage);
                default:
                    errorMessage = $"{normalizedTarget} hedefi akıllı içerik köprüsü tarafından desteklenmiyor.";
                    return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool TryExtractUniversalBridgeContent(string filePath, string sourceFormat, out List<string> lines, out List<List<string>> rows, out string errorMessage)
    {
        lines = new List<string>();
        rows = new List<List<string>>();
        errorMessage = string.Empty;

        var normalizedSource = NormalizeConvertTarget(sourceFormat);

        try
        {
            if (IsOfficeOpenXmlSource(normalizedSource))
            {
                return TryExtractOpenXmlContent(filePath, normalizedSource, out lines, out rows, out errorMessage);
            }

            if (normalizedSource == "PDF")
            {
                if (!TryExtractTextFromPdf(filePath, out var pages, out errorMessage))
                {
                    return false;
                }

                lines = pages.SelectMany(GetPdfTextLines).ToList();
                rows = BuildPdfTableRows(pages);
                return true;
            }

            if (normalizedSource == "CSV")
            {
                rows = File.ReadAllLines(filePath, Encoding.UTF8)
                    .Select(ParseDelimitedTextRow)
                    .Where(row => row.Count > 0)
                    .ToList();
                lines = rows.Select(row => string.Join("\t", row)).ToList();
                return true;
            }

            if (normalizedSource is "TXT" or "MD" or "RST" or "TEX")
            {
                lines = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
                rows = lines.Select(line => new List<string> { line }).ToList();
                return true;
            }

            if (normalizedSource is "HTML" or "HTM")
            {
                var html = File.ReadAllText(filePath, Encoding.UTF8);
                var text = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, "</(p|div|tr|li|h[1-6])>", "\n", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, "<[^>]+>", " ");
                text = WebUtility.HtmlDecode(text);
                lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Select(line => Regex.Replace(line, "\\s+", " ").Trim())
                    .Where(line => line.Length > 0)
                    .ToList();
                rows = lines.Select(line => new List<string> { line }).ToList();
                return true;
            }

            var intermediateTarget = GetLibreOfficeBridgeIntermediateTarget(normalizedSource);
            if (!string.IsNullOrWhiteSpace(intermediateTarget))
            {
                var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridgeUniversalBridge", Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tempFolder);

                    if (!TryConvertWithLibreOffice(filePath, normalizedSource, intermediateTarget, tempFolder, out var intermediatePath, out var libreOfficeError))
                    {
                        errorMessage = string.IsNullOrWhiteSpace(libreOfficeError)
                            ? $"{normalizedSource} dosyası {intermediateTarget} ara formatına çevrilemedi."
                            : libreOfficeError;
                        return false;
                    }

                    if (!File.Exists(intermediatePath))
                    {
                        errorMessage = $"{intermediateTarget} ara dosyası oluşmadı.";
                        return false;
                    }

                    return TryExtractOpenXmlContent(intermediatePath, intermediateTarget, out lines, out rows, out errorMessage);
                }
                finally
                {
                    TryDeleteDirectory(tempFolder);
                }
            }

            errorMessage = $"{normalizedSource} kaynağından içerik çıkarılamadı.";
            return false;
        }
        catch (DecoderFallbackException)
        {
            try
            {
                lines = File.ReadAllLines(filePath, Encoding.Default).ToList();
                rows = lines.Select(line => new List<string> { line }).ToList();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool TryWritePptViaLibreOfficeBridge(IReadOnlyList<string> lines, string outputFolder, string baseName, out string outputPath, out string errorMessage)
    {
        outputPath = string.Empty;
        errorMessage = string.Empty;
        var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridgePptBridge", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempFolder);
            var pptxPath = Path.Combine(tempFolder, baseName + ".pptx");
            WriteLinesAsPptx(pptxPath, lines);

            if (TryConvertWithLibreOffice(pptxPath, "PPTX", "PPT", outputFolder, out var convertedPath, out var libreOfficeError))
            {
                outputPath = convertedPath;
                return true;
            }

            errorMessage = string.IsNullOrWhiteSpace(libreOfficeError)
                ? "PPTX ara sunumu PPT formatına çevrilemedi."
                : libreOfficeError;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempFolder);
        }
    }

    private static string GetLibreOfficeBridgeIntermediateTarget(string sourceFormat)
    {
        var normalizedSource = NormalizeConvertTarget(sourceFormat);

        if (IsLibreOfficeWriterFamily(normalizedSource))
        {
            return "DOCX";
        }

        if (IsLibreOfficeCalcFamily(normalizedSource))
        {
            return "XLSX";
        }

        if (IsLibreOfficeImpressFamily(normalizedSource))
        {
            return "PPTX";
        }

        return string.Empty;
    }

    private static bool IsUniversalContentBridgeSource(string format)
    {
        var normalized = NormalizeConvertTarget(format);
        return normalized == "PDF" || IsLibreOfficeWriterFamily(normalized) || IsLibreOfficeCalcFamily(normalized) || IsLibreOfficeImpressFamily(normalized) || normalized is "HTML" or "HTM" or "CSV" or "TXT" or "MD" or "RST" or "TEX";
    }

    private static bool IsUniversalContentBridgeTarget(string format)
    {
        var normalized = NormalizeConvertTarget(format);
        return normalized is "PDF" or "TXT" or "CSV" or "HTML" or "RTF" or "DOC" or "DOCX" or "ODT" or "XLS" or "XLSX" or "ODS" or "PPT" or "PPTX" or "ODP";
    }

    private static List<string> ParseDelimitedTextRow(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return new List<string>();
        }

        var delimiter = line.Contains(';') ? ';' : line.Contains('\t') ? '\t' : ',';
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == delimiter && !inQuotes)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }

            cell.Append(character);
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static bool TryConvertOpenXmlBuiltIn(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedSource = NormalizeConvertTarget(sourceFormat);
        var normalizedTarget = NormalizeConvertTarget(targetFormat);

        if (!IsOfficeOpenXmlSource(normalizedSource))
        {
            return false;
        }

        if (!IsBuiltInOpenXmlTarget(normalizedTarget))
        {
            return false;
        }

        try
        {
            if (!TryExtractOpenXmlContent(filePath, normalizedSource, out var lines, out var rows, out var extractError))
            {
                errorMessage = extractError;
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);

            switch (normalizedTarget)
            {
                case "TXT":
                    File.WriteAllLines(outputPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    return true;
                case "CSV":
                    WriteTableRowsAsCsv(outputPath, rows);
                    return true;
                case "HTML":
                    WriteOpenXmlContentAsHtml(outputPath, lines, rows);
                    return true;
                case "RTF":
                case "DOC":
                    WriteLinesAsRtf(outputPath, lines);
                    return true;
                case "DOCX":
                    WriteLinesAsDocx(outputPath, lines);
                    return true;
                case "XLS":
                    WriteTableRowsAsExcelHtml(outputPath, rows);
                    return true;
                case "XLSX":
                    WriteTableRowsAsXlsx(outputPath, rows);
                    return true;
                case "ODS":
                    WriteRowsAsOds(outputPath, rows);
                    return true;
                case "ODT":
                    WriteLinesAsOdt(outputPath, lines);
                    return true;
                case "PPTX":
                    WriteLinesAsPptx(outputPath, lines);
                    return true;
                case "ODP":
                    WriteLinesAsOdp(outputPath, lines);
                    return true;
                case "PDF":
                    WriteLinesAsSimplePdf(outputPath, lines);
                    return true;
                default:
                    errorMessage = $"{normalizedTarget} hedefi için dahili Open XML dönüştürme desteklenmiyor.";
                    return false;
            }
        }
        catch (InvalidDataException ex)
        {
            errorMessage = $"Dosya yapısı okunamadı: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool IsOfficeOpenXmlSource(string sourceFormat)
    {
        var normalized = NormalizeConvertTarget(sourceFormat);
        return normalized is "DOCX" or "XLSX" or "PPTX";
    }

    private static bool IsBuiltInOpenXmlTarget(string targetFormat)
    {
        var normalized = NormalizeConvertTarget(targetFormat);
        return normalized is "PDF" or "TXT" or "CSV" or "HTML" or "RTF" or "DOC" or "DOCX" or "XLS" or "XLSX" or "ODS" or "ODT" or "PPTX" or "ODP";
    }

    private bool TryConvertLegacyOfficeViaLibreOfficeOpenXmlBridge(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedSource = NormalizeConvertTarget(sourceFormat);
        var normalizedTarget = NormalizeConvertTarget(targetFormat);

        if (normalizedTarget == "PDF" || !IsBuiltInOpenXmlTarget(normalizedTarget))
        {
            return false;
        }

        var intermediateTarget = normalizedSource switch
        {
            "XLS" => "XLSX",
            "PPT" or "PPS" => "PPTX",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(intermediateTarget))
        {
            return false;
        }

        if (!IsLibreOfficeCrossFamilyDirectlyUnsupported(normalizedSource, normalizedTarget))
        {
            return false;
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridgeConvertBridge", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempFolder);

            if (!TryConvertWithLibreOffice(filePath, normalizedSource, intermediateTarget, tempFolder, out var intermediatePath, out var libreOfficeError))
            {
                errorMessage = string.IsNullOrWhiteSpace(libreOfficeError)
                    ? $"{normalizedSource} dosyası {intermediateTarget} ara formatına çevrilemedi."
                    : libreOfficeError;
                return false;
            }

            if (!File.Exists(intermediatePath))
            {
                errorMessage = $"{intermediateTarget} ara dosyası oluşmadı.";
                return false;
            }

            if (TryConvertOpenXmlBuiltIn(intermediatePath, intermediateTarget, normalizedTarget, outputPath, out var openXmlError))
            {
                return true;
            }

            errorMessage = string.IsNullOrWhiteSpace(openXmlError)
                ? $"{intermediateTarget} ara formatı {normalizedTarget} hedefine çevrilemedi."
                : openXmlError;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempFolder);
        }
    }

    private static bool TryExtractOpenXmlContent(string filePath, string sourceFormat, out List<string> lines, out List<List<string>> rows, out string errorMessage)
    {
        lines = new List<string>();
        rows = new List<List<string>>();
        errorMessage = string.Empty;

        var normalizedSource = NormalizeConvertTarget(sourceFormat);
        using var archive = ZipFile.OpenRead(filePath);

        switch (normalizedSource)
        {
            case "DOCX":
                lines = ExtractDocxLines(archive);
                rows = lines.Select(line => new List<string> { line }).ToList();
                break;
            case "XLSX":
                rows = ExtractXlsxRows(archive);
                lines = rows.Select(row => string.Join("\t", row)).ToList();
                break;
            case "PPTX":
                lines = ExtractPptxLines(archive);
                rows = lines.Select(line => new List<string> { line }).ToList();
                break;
            default:
                errorMessage = $"{normalizedSource} için dahili Open XML okuyucu yok.";
                return false;
        }

        lines = lines.Select(line => NormalizeOfficeExtractedText(line))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        rows = rows.Select(row => row.Select(cell => NormalizeOfficeExtractedText(cell)).ToList())
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        if (lines.Count == 0 && rows.Count == 0)
        {
            errorMessage = "Dosya içinden dönüştürülebilir metin çıkarılamadı.";
            return false;
        }

        if (rows.Count == 0)
        {
            rows = lines.Select(line => new List<string> { line }).ToList();
        }

        if (lines.Count == 0)
        {
            lines = rows.Select(row => string.Join("\t", row)).ToList();
        }

        return true;
    }

    private static List<string> ExtractDocxLines(ZipArchive archive)
    {
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        var xml = ReadZipEntryText(entry);
        xml = Regex.Replace(xml, @"<w:tab\b[^>]*/>", "\t", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, @"<w:br\b[^>]*/>", "\n", RegexOptions.IgnoreCase);

        var paragraphs = Regex.Split(xml, @"</w:p>", RegexOptions.IgnoreCase);
        var lines = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            var builder = new StringBuilder();
            foreach (Match match in Regex.Matches(paragraph, @"<w:t(?:\s[^>]*)?>(.*?)</w:t>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                builder.Append(DecodeXmlText(match.Groups[1].Value));
            }

            var line = builder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static List<List<string>> ExtractXlsxRows(ZipArchive archive)
    {
        var sharedStrings = ExtractXlsxSharedStrings(archive);
        var sheetEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<List<string>>();
        foreach (var sheetEntry in sheetEntries.Take(1))
        {
            var xml = ReadZipEntryText(sheetEntry);
            foreach (Match rowMatch in Regex.Matches(xml, @"<row\b[^>]*>(.*?)</row>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var row = new List<string>();
                foreach (Match cellMatch in Regex.Matches(rowMatch.Groups[1].Value, @"<c\b([^>]*)>(.*?)</c>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    var attributes = cellMatch.Groups[1].Value;
                    var content = cellMatch.Groups[2].Value;
                    row.Add(ExtractXlsxCellText(attributes, content, sharedStrings));
                }

                if (row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                {
                    rows.Add(row);
                }
            }
        }

        return rows;
    }

    private static List<string> ExtractXlsxSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        var xml = ReadZipEntryText(entry);
        var values = new List<string>();
        foreach (Match siMatch in Regex.Matches(xml, @"<si\b[^>]*>(.*?)</si>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            var builder = new StringBuilder();
            foreach (Match textMatch in Regex.Matches(siMatch.Groups[1].Value, @"<t(?:\s[^>]*)?>(.*?)</t>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                builder.Append(DecodeXmlText(textMatch.Groups[1].Value));
            }
            values.Add(builder.ToString());
        }

        return values;
    }

    private static string ExtractXlsxCellText(string attributes, string content, IReadOnlyList<string> sharedStrings)
    {
        var typeMatch = Regex.Match(attributes, "t=\\\"(?<t>[^\\\"]+)\\\"", RegexOptions.IgnoreCase);
        var cellType = typeMatch.Success ? typeMatch.Groups["t"].Value : string.Empty;

        if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            var inlineBuilder = new StringBuilder();
            foreach (Match textMatch in Regex.Matches(content, @"<t(?:\s[^>]*)?>(.*?)</t>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                inlineBuilder.Append(DecodeXmlText(textMatch.Groups[1].Value));
            }
            return inlineBuilder.ToString();
        }

        var valueMatch = Regex.Match(content, @"<v>(.*?)</v>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!valueMatch.Success)
        {
            return string.Empty;
        }

        var rawValue = DecodeXmlText(valueMatch.Groups[1].Value).Trim();
        if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase) && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return rawValue;
    }

    private static List<string> ExtractPptxLines(ZipArchive archive)
    {
        var lines = new List<string>();
        var slideEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase);

        foreach (var slideEntry in slideEntries)
        {
            var xml = ReadZipEntryText(slideEntry);
            foreach (Match match in Regex.Matches(xml, @"<a:t>(.*?)</a:t>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var text = DecodeXmlText(match.Groups[1].Value).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }
        }

        return lines;
    }

    private static string ReadZipEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string DecodeXmlText(string value)
    {
        return System.Net.WebUtility.HtmlDecode(value ?? string.Empty);
    }

    private static string NormalizeOfficeExtractedText(string value)
    {
        return (value ?? string.Empty)
            .Replace("\0", string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\u00A0", " ")
            .Trim();
    }

    private static void WriteTableRowsAsCsv(string outputPath, IReadOnlyList<List<string>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(";", row.Select(EscapeCsvCell)));
        }
        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WriteOpenXmlContentAsHtml(string outputPath, IReadOnlyList<string> lines, IReadOnlyList<List<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>ToolBridge Dönüşümü</title>");
        builder.AppendLine("<style>body{font-family:Arial,sans-serif;font-size:14px;line-height:1.45}table{border-collapse:collapse}td{border:1px solid #ddd;padding:6px}</style></head><body>");
        if (rows.Any(row => row.Count > 1))
        {
            builder.AppendLine("<table>");
            foreach (var row in rows)
            {
                builder.Append("<tr>");
                foreach (var cell in row)
                {
                    builder.Append("<td>");
                    builder.Append(EscapeXml(cell));
                    builder.Append("</td>");
                }
                builder.AppendLine("</tr>");
            }
            builder.AppendLine("</table>");
        }
        else
        {
            foreach (var line in lines)
            {
                builder.Append("<p>");
                builder.Append(EscapeXml(line));
                builder.AppendLine("</p>");
            }
        }
        builder.AppendLine("</body></html>");
        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WriteLinesAsRtf(string outputPath, IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\fs22 ");
        foreach (var line in lines)
        {
            builder.Append(EscapeRtf(line));
            builder.Append(@"\par ");
        }
        builder.Append('}');
        File.WriteAllText(outputPath, builder.ToString(), Encoding.ASCII);
    }

    private static void WriteLinesAsDocx(string outputPath, IReadOnlyList<string> lines)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>
""");
        AddZipTextEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>
""");
        var document = new StringBuilder();
        document.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        document.AppendLine("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
        foreach (var line in lines)
        {
            document.Append("<w:p><w:r><w:t xml:space=\"preserve\">");
            document.Append(EscapeXml(line));
            document.AppendLine("</w:t></w:r></w:p>");
        }
        document.AppendLine("<w:sectPr/></w:body></w:document>");
        AddZipTextEntry(archive, "word/document.xml", document.ToString());
    }

    private static void WriteTableRowsAsExcelHtml(string outputPath, IReadOnlyList<List<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"></head><body><table border=\"1\">");
        foreach (var row in rows)
        {
            builder.Append("<tr>");
            foreach (var cell in row)
            {
                builder.Append("<td>");
                builder.Append(EscapeXml(cell));
                builder.Append("</td>");
            }
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</table></body></html>");
        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WriteTableRowsAsXlsx(string outputPath, IReadOnlyList<List<string>> rows)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
""");
        AddZipTextEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
""");
        AddZipTextEntry(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
""");
        AddZipTextEntry(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="ToolBridge" sheetId="1" r:id="rId1"/></sheets></workbook>
""");
        AddZipTextEntry(archive, "xl/styles.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="1"><xf xfId="0"/></cellXfs></styleSheet>
""");

        var sheet = new StringBuilder();
        sheet.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var rowNumber = 1;
        foreach (var row in rows.Take(1048576))
        {
            sheet.Append($"<row r=\"{rowNumber}\">");
            for (var columnIndex = 0; columnIndex < row.Count && columnIndex < 16384; columnIndex++)
            {
                var reference = $"{GetExcelColumnName(columnIndex + 1)}{rowNumber}";
                sheet.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
                sheet.Append(EscapeXml(row[columnIndex]));
                sheet.Append("</t></is></c>");
            }
            sheet.AppendLine("</row>");
            rowNumber++;
        }
        sheet.AppendLine("</sheetData></worksheet>");
        AddZipTextEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    private static void WriteLinesAsPptx(string outputPath, IReadOnlyList<string> lines)
    {
        var slides = BuildPresentationSlides(lines);
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        var contentTypes = new StringBuilder();
        contentTypes.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        contentTypes.AppendLine("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>");
        for (var index = 0; index < slides.Count; index++)
        {
            contentTypes.AppendLine($"<Override PartName=\"/ppt/slides/slide{index + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>");
        }
        contentTypes.AppendLine("</Types>");
        AddZipTextEntry(archive, "[Content_Types].xml", contentTypes.ToString());

        AddZipTextEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/></Relationships>
""");

        var presentation = new StringBuilder();
        presentation.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        presentation.AppendLine("<p:presentation xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:sldIdLst>");
        for (var index = 0; index < slides.Count; index++)
        {
            presentation.AppendLine($"<p:sldId id=\"{256 + index}\" r:id=\"rId{index + 1}\"/>");
        }
        presentation.AppendLine("</p:sldIdLst><p:sldSz cx=\"9144000\" cy=\"6858000\" type=\"screen4x3\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/></p:presentation>");
        AddZipTextEntry(archive, "ppt/presentation.xml", presentation.ToString());

        var presentationRels = new StringBuilder();
        presentationRels.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        presentationRels.AppendLine("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var index = 0; index < slides.Count; index++)
        {
            presentationRels.AppendLine($"<Relationship Id=\"rId{index + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide{index + 1}.xml\"/>");
        }
        presentationRels.AppendLine("</Relationships>");
        AddZipTextEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRels.ToString());

        for (var index = 0; index < slides.Count; index++)
        {
            AddZipTextEntry(archive, $"ppt/slides/slide{index + 1}.xml", BuildPptxSlideXml(slides[index], index + 1));
        }
    }

    private static string BuildPptxSlideXml(IReadOnlyList<string> lines, int slideNumber)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.AppendLine("<p:sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld><p:spTree>");
        builder.AppendLine("<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>");
        builder.AppendLine($"<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"ToolBridge Slide {slideNumber}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"457200\" y=\"457200\"/><a:ext cx=\"8229600\" cy=\"5943600\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></p:spPr><p:txBody><a:bodyPr wrap=\"square\"/><a:lstStyle/>");

        foreach (var line in lines)
        {
            builder.Append("<a:p><a:r><a:rPr lang=\"tr-TR\" sz=\"2200\"/><a:t>");
            builder.Append(EscapeXml(line));
            builder.AppendLine("</a:t></a:r></a:p>");
        }

        builder.AppendLine("</p:txBody></p:sp></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>");
        return builder.ToString();
    }

    private static void WriteLinesAsOdp(string outputPath, IReadOnlyList<string> lines)
    {
        var slides = BuildPresentationSlides(lines);
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "mimetype", "application/vnd.oasis.opendocument.presentation", CompressionLevel.NoCompression);
        AddZipTextEntry(archive, "META-INF/manifest.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"><manifest:file-entry manifest:media-type="application/vnd.oasis.opendocument.presentation" manifest:full-path="/"/><manifest:file-entry manifest:media-type="text/xml" manifest:full-path="content.xml"/></manifest:manifest>
""");

        var content = new StringBuilder();
        content.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        content.AppendLine("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.2\"><office:body><office:presentation>");
        for (var index = 0; index < slides.Count; index++)
        {
            content.AppendLine($"<draw:page draw:name=\"Sayfa {index + 1}\" presentation:presentation-page-layout-name=\"AL1T0\">");
            content.AppendLine("<draw:frame svg:x=\"1cm\" svg:y=\"1cm\" svg:width=\"24cm\" svg:height=\"16cm\"><draw:text-box>");
            foreach (var line in slides[index])
            {
                content.Append("<text:p>");
                content.Append(EscapeXml(line));
                content.AppendLine("</text:p>");
            }
            content.AppendLine("</draw:text-box></draw:frame></draw:page>");
        }
        content.AppendLine("</office:presentation></office:body></office:document-content>");
        AddZipTextEntry(archive, "content.xml", content.ToString());
    }

    private static List<List<string>> BuildPresentationSlides(IReadOnlyList<string> lines)
    {
        var normalizedLines = lines
            .Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : Regex.Replace(line.Trim(), "\\s+", " "))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (normalizedLines.Count == 0)
        {
            normalizedLines.Add("Boş içerik");
        }

        var slides = new List<List<string>>();
        for (var index = 0; index < normalizedLines.Count; index += 8)
        {
            slides.Add(normalizedLines.Skip(index).Take(8).ToList());
        }

        return slides;
    }

    private static void WriteLinesAsOdt(string outputPath, IReadOnlyList<string> lines)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression);
        AddZipTextEntry(archive, "META-INF/manifest.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"><manifest:file-entry manifest:media-type="application/vnd.oasis.opendocument.text" manifest:full-path="/"/><manifest:file-entry manifest:media-type="text/xml" manifest:full-path="content.xml"/></manifest:manifest>
""");
        var content = new StringBuilder();
        content.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        content.AppendLine("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:text>");
        foreach (var line in lines)
        {
            content.Append("<text:p>");
            content.Append(EscapeXml(line));
            content.AppendLine("</text:p>");
        }
        content.AppendLine("</office:text></office:body></office:document-content>");
        AddZipTextEntry(archive, "content.xml", content.ToString());
    }

    private static void WriteRowsAsOds(string outputPath, IReadOnlyList<List<string>> rows)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "mimetype", "application/vnd.oasis.opendocument.spreadsheet", CompressionLevel.NoCompression);
        AddZipTextEntry(archive, "META-INF/manifest.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"><manifest:file-entry manifest:media-type="application/vnd.oasis.opendocument.spreadsheet" manifest:full-path="/"/><manifest:file-entry manifest:media-type="text/xml" manifest:full-path="content.xml"/></manifest:manifest>
""");
        var content = new StringBuilder();
        content.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        content.AppendLine("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:spreadsheet><table:table table:name=\"ToolBridge\">");
        foreach (var row in rows)
        {
            content.Append("<table:table-row>");
            foreach (var cell in row)
            {
                content.Append("<table:table-cell office:value-type=\"string\"><text:p>");
                content.Append(EscapeXml(cell));
                content.Append("</text:p></table:table-cell>");
            }
            content.AppendLine("</table:table-row>");
        }
        content.AppendLine("</table:table></office:spreadsheet></office:body></office:document-content>");
        AddZipTextEntry(archive, "content.xml", content.ToString());
    }

    private static void WriteLinesAsSimplePdf(string outputPath, IReadOnlyList<string> lines)
    {
        var normalizedLines = lines.Count == 0 ? new List<string> { "Dönüştürülebilir metin bulunamadı." } : lines.ToList();
        const int linesPerPage = 42;
        var pages = normalizedLines.Chunk(linesPerPage).Select(chunk => chunk.ToList()).ToList();
        if (pages.Count == 0)
        {
            pages.Add(new List<string> { "Dönüştürülebilir metin bulunamadı." });
        }

        var objects = new List<string>();
        var pageObjectNumbers = new List<int>();
        var contentsObjectNumbers = new List<int>();

        // 1: catalog, 2: pages, 3: font
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add(string.Empty); // pages object is filled after page objects are known.
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        foreach (var pageLines in pages)
        {
            var contentStream = BuildSimplePdfContentStream(pageLines);
            var contentObjectNumber = objects.Count + 1;
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}\nendstream");
            contentsObjectNumbers.Add(contentObjectNumber);

            var pageObjectNumber = objects.Count + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
            pageObjectNumbers.Add(pageObjectNumber);
        }

        objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] /Count {pageObjectNumbers.Count} >>";

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, Encoding.ASCII);
        writer.WriteLine("%PDF-1.4");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            writer.Flush();
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
        }

        writer.Flush();
        var xrefOffset = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            writer.WriteLine($"{offset:0000000000} 00000 n ");
        }
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("%%EOF");
    }

    private static string BuildSimplePdfContentStream(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 11 Tf");
        builder.AppendLine("50 790 Td");
        var first = true;
        foreach (var line in lines)
        {
            if (!first)
            {
                builder.AppendLine("0 -16 Td");
            }
            builder.Append('(');
            builder.Append(EscapePdfText(TransliterateForPdf(line)));
            builder.AppendLine(") Tj");
            first = false;
        }
        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static string EscapePdfText(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static string TransliterateForPdf(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ü', 'u')
            .Replace('Ü', 'U')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ö', 'o')
            .Replace('Ö', 'O')
            .Replace('ç', 'c')
            .Replace('Ç', 'C');
    }

    private bool TryConvertDocumentToImageViaPdf(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsPdfImageExportTarget(sourceFormat, targetFormat))
        {
            return false;
        }

        var normalizedSource = NormalizeConvertTarget(sourceFormat);
        if (normalizedSource == "PDF" || IsRasterImageFormat(normalizedSource))
        {
            return false;
        }

        if (!IsDocumentFormat(normalizedSource) && !IsSpreadsheetFormat(normalizedSource) && !IsPresentationFormat(normalizedSource))
        {
            return false;
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridgeConvert", Guid.NewGuid().ToString("N"));
        var errors = new List<string>();

        try
        {
            Directory.CreateDirectory(tempFolder);

            if (TryConvertWithLibreOffice(filePath, normalizedSource, "PDF", tempFolder, out var libreOfficePdfPath, out var libreOfficeError) &&
                File.Exists(libreOfficePdfPath) &&
                TryConvertPdfFirstPageWithDocnet(libreOfficePdfPath, "PDF", targetFormat, outputPath, out var librePdfImageError))
            {
                return true;
            }

            AddConversionError(errors, "LibreOffice > PDF > görsel", libreOfficeError);

            if (TryConvertWithMicrosoftOffice(filePath, normalizedSource, "PDF", Path.Combine(tempFolder, SanitizeFileName(Path.GetFileNameWithoutExtension(filePath)) + ".pdf"), out var officeError) &&
                File.Exists(Path.Combine(tempFolder, SanitizeFileName(Path.GetFileNameWithoutExtension(filePath)) + ".pdf")) &&
                TryConvertPdfFirstPageWithDocnet(Path.Combine(tempFolder, SanitizeFileName(Path.GetFileNameWithoutExtension(filePath)) + ".pdf"), "PDF", targetFormat, outputPath, out var officePdfImageError))
            {
                return true;
            }

            AddConversionError(errors, "Microsoft Office > PDF > görsel", officeError);

            errorMessage = errors.Count == 0
                ? "Belgeyi PNG/JPG için önce PDF'e dönüştürecek motor bulunamadı."
                : string.Join(" | ", errors.Take(3));
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempFolder);
        }
    }

    private static bool TryConvertPdfToEditableBuiltIn(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (NormalizeConvertTarget(sourceFormat) != "PDF" || !IsPdfEditableExportTarget(targetFormat))
        {
            return false;
        }

        try
        {
            if (!TryExtractTextFromPdf(filePath, out var pages, out var extractError))
            {
                errorMessage = extractError;
                return false;
            }

            var normalizedTarget = NormalizeConvertTarget(targetFormat);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);

            switch (normalizedTarget)
            {
                case "TXT":
                    File.WriteAllText(outputPath, BuildPlainTextFromPdfPages(pages), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    break;
                case "CSV":
                    WritePdfTextAsCsv(outputPath, pages);
                    break;
                case "HTML":
                    WritePdfTextAsHtml(outputPath, pages);
                    break;
                case "RTF":
                case "DOC":
                    WritePdfTextAsRtf(outputPath, pages);
                    break;
                case "DOCX":
                    WritePdfTextAsDocx(outputPath, pages);
                    break;
                case "XLS":
                    WritePdfTextAsExcelHtml(outputPath, pages);
                    break;
                case "XLSX":
                    WritePdfTextAsXlsx(outputPath, pages);
                    break;
                case "ODT":
                    WritePdfTextAsOdt(outputPath, pages);
                    break;
                case "ODS":
                    WritePdfTextAsOds(outputPath, pages);
                    break;
                default:
                    errorMessage = "PDF bu hedef formata dahili olarak dönüştürülemez.";
                    return false;
            }

            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool IsPdfEditableExportTarget(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) is "TXT" or "CSV" or "HTML" or "RTF" or "DOC" or "DOCX" or "XLS" or "XLSX" or "ODT" or "ODS";
    }

    private static bool TryExtractTextFromPdf(string filePath, out List<string> pages, out string errorMessage)
    {
        if (TryExtractTextFromPdfWithPdfium(filePath, out pages, out errorMessage))
        {
            return true;
        }

        if (TryExtractTextFromPdfStreams(filePath, out pages, out var streamError))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? streamError
            : $"{errorMessage} {streamError}".Trim();
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "PDF metni çıkarılamadı. Dosya taranmış görsel PDF olabilir.";
        }

        return false;
    }

    private static bool TryExtractTextFromPdfWithPdfium(string filePath, out List<string> pages, out string errorMessage)
    {
        pages = new List<string>();
        errorMessage = string.Empty;

        var pdfiumPath = FindPdfiumLibrary();
        if (string.IsNullOrWhiteSpace(pdfiumPath))
        {
            errorMessage = "PDFium metin motoru bulunamadı.";
            return false;
        }

        IntPtr libraryHandle = IntPtr.Zero;
        IntPtr documentHandle = IntPtr.Zero;

        try
        {
            libraryHandle = NativeLibrary.Load(pdfiumPath);
            if (libraryHandle == IntPtr.Zero ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_InitLibrary", out var initLibraryPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadDocument", out var loadDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageCount", out var getPageCountPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadPage", out var loadPagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_ClosePage", out var closePagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFText_LoadPage", out var textLoadPagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFText_CountChars", out var textCountCharsPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFText_GetUnicode", out var textGetUnicodePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFText_ClosePage", out var textClosePagePointer))
            {
                errorMessage = "PDFium metin okuma fonksiyonları bulunamadı.";
                return false;
            }

            var initLibrary = Marshal.GetDelegateForFunctionPointer<FpdfInitLibraryDelegate>(initLibraryPointer);
            var loadDocument = Marshal.GetDelegateForFunctionPointer<FpdfLoadDocumentDelegate>(loadDocumentPointer);
            var getPageCount = Marshal.GetDelegateForFunctionPointer<FpdfGetPageCountDelegate>(getPageCountPointer);
            var loadPage = Marshal.GetDelegateForFunctionPointer<FpdfLoadPageDelegate>(loadPagePointer);
            var closePage = Marshal.GetDelegateForFunctionPointer<FpdfClosePageDelegate>(closePagePointer);
            var closeDocument = Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer);
            var textLoadPage = Marshal.GetDelegateForFunctionPointer<FpdfTextLoadPageDelegate>(textLoadPagePointer);
            var textCountChars = Marshal.GetDelegateForFunctionPointer<FpdfTextCountCharsDelegate>(textCountCharsPointer);
            var textGetUnicode = Marshal.GetDelegateForFunctionPointer<FpdfTextGetUnicodeDelegate>(textGetUnicodePointer);
            var textClosePage = Marshal.GetDelegateForFunctionPointer<FpdfTextClosePageDelegate>(textClosePagePointer);

            initLibrary();
            documentHandle = loadDocument(filePath, null);
            if (documentHandle == IntPtr.Zero)
            {
                errorMessage = "PDF dosyası açılamadı.";
                return false;
            }

            var pageCount = Math.Max(0, getPageCount(documentHandle));
            if (pageCount == 0)
            {
                errorMessage = "PDF sayfa sayısı okunamadı.";
                return false;
            }

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                IntPtr pageHandle = IntPtr.Zero;
                IntPtr textPageHandle = IntPtr.Zero;

                try
                {
                    pageHandle = loadPage(documentHandle, pageIndex);
                    if (pageHandle == IntPtr.Zero)
                    {
                        pages.Add(string.Empty);
                        continue;
                    }

                    textPageHandle = textLoadPage(pageHandle);
                    if (textPageHandle == IntPtr.Zero)
                    {
                        pages.Add(string.Empty);
                        continue;
                    }

                    var charCount = Math.Max(0, textCountChars(textPageHandle));
                    var builder = new StringBuilder(Math.Min(charCount, 1024 * 1024));
                    for (var charIndex = 0; charIndex < charCount; charIndex++)
                    {
                        var unicode = textGetUnicode(textPageHandle, charIndex);
                        if (unicode == 0)
                        {
                            continue;
                        }

                        try
                        {
                            builder.Append(char.ConvertFromUtf32((int)unicode));
                        }
                        catch
                        {
                            // Geçersiz Unicode karakteri varsa metin çıkarma akışı bozulmasın.
                        }
                    }

                    pages.Add(NormalizeExtractedPdfText(builder.ToString()));
                }
                finally
                {
                    if (textPageHandle != IntPtr.Zero)
                    {
                        try { textClosePage(textPageHandle); } catch { }
                    }

                    if (pageHandle != IntPtr.Zero)
                    {
                        try { closePage(pageHandle); } catch { }
                    }
                }
            }

            if (!pages.Any(page => !string.IsNullOrWhiteSpace(page)))
            {
                errorMessage = "PDF içinde seçilebilir metin bulunamadı. Dosya taranmış görsel PDF olabilir.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            if (documentHandle != IntPtr.Zero)
            {
                try
                {
                    if (libraryHandle != IntPtr.Zero && NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer))
                    {
                        Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer)(documentHandle);
                    }
                }
                catch
                {
                    // Belge kapatma hatası dönüştürme sonucunu etkilememeli.
                }
            }
        }
    }

    private static string FindPdfiumLibrary()
    {
        var docnetPath = FindDocnetCoreAssembly();
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "pdfium.dll"),
            Path.Combine(AppContext.BaseDirectory, "pdfium.dll"),
            string.IsNullOrWhiteSpace(docnetPath) ? string.Empty : Path.Combine(Path.GetDirectoryName(docnetPath) ?? string.Empty, "pdfium.dll")
        };

        return possiblePaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)) ?? string.Empty;
    }

    private static bool TryExtractTextFromPdfStreams(string filePath, out List<string> pages, out string errorMessage)
    {
        pages = new List<string>();
        errorMessage = string.Empty;

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var extractedBlocks = new List<string>();
            var searchIndex = 0;

            while (searchIndex < bytes.Length)
            {
                var streamStart = IndexOfAscii(bytes, "stream", searchIndex);
                if (streamStart < 0)
                {
                    break;
                }

                var dataStart = streamStart + 6;
                if (dataStart < bytes.Length && bytes[dataStart] == 13) dataStart++;
                if (dataStart < bytes.Length && bytes[dataStart] == 10) dataStart++;

                var streamEnd = IndexOfAscii(bytes, "endstream", dataStart);
                if (streamEnd < 0)
                {
                    break;
                }

                var dataEnd = streamEnd;
                while (dataEnd > dataStart && (bytes[dataEnd - 1] == 10 || bytes[dataEnd - 1] == 13 || bytes[dataEnd - 1] == 32))
                {
                    dataEnd--;
                }

                var streamData = bytes.Skip(dataStart).Take(Math.Max(0, dataEnd - dataStart)).ToArray();
                var headerStart = Math.Max(0, streamStart - 2000);
                var header = Encoding.Latin1.GetString(bytes, headerStart, streamStart - headerStart);
                var decodedData = header.Contains("/FlateDecode", StringComparison.OrdinalIgnoreCase)
                    ? TryDecodePdfFlateStream(streamData)
                    : streamData;

                if (decodedData.Length > 0)
                {
                    var content = Encoding.Latin1.GetString(decodedData);
                    var text = ExtractTextFromPdfContentStream(content);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        extractedBlocks.Add(text);
                    }
                }

                searchIndex = streamEnd + 9;
            }

            var merged = NormalizeExtractedPdfText(string.Join(Environment.NewLine, extractedBlocks));
            if (string.IsNullOrWhiteSpace(merged))
            {
                errorMessage = "PDF içerik akışından metin çıkarılamadı.";
                return false;
            }

            pages.Add(merged);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static byte[] TryDecodePdfFlateStream(byte[] data)
    {
        if (data.Length == 0)
        {
            return Array.Empty<byte>();
        }

        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            try
            {
                using var input = new MemoryStream(data);
                if (input.Length > 2)
                {
                    input.Position = 2;
                }

                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }
    }

    private static string ExtractTextFromPdfContentStream(string content)
    {
        var pendingStrings = new List<string>();
        var builder = new StringBuilder();

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '(')
            {
                pendingStrings.Add(ReadPdfLiteralString(content, ref index));
                continue;
            }

            if (character == '<' && index + 1 < content.Length && content[index + 1] != '<')
            {
                pendingStrings.Add(ReadPdfHexString(content, ref index));
                continue;
            }

            if (!IsPdfOperatorStart(character))
            {
                continue;
            }

            var start = index;
            while (index < content.Length && IsPdfOperatorStart(content[index]))
            {
                index++;
            }

            var token = content.Substring(start, index - start);
            index--;

            if (token is "Tj" or "TJ" or "'" or "\"")
            {
                AppendPendingPdfStrings(builder, pendingStrings);
            }
            else if (token is "Td" or "TD" or "T*")
            {
                AppendLineBreakIfNeeded(builder);
            }
        }

        return NormalizeExtractedPdfText(builder.ToString());
    }

    private static bool IsPdfOperatorStart(char character)
    {
        return char.IsLetter(character) || character == '*' || character == '\'' || character == '"';
    }

    private static void AppendPendingPdfStrings(StringBuilder builder, List<string> pendingStrings)
    {
        if (pendingStrings.Count == 0)
        {
            return;
        }

        foreach (var value in pendingStrings)
        {
            builder.Append(value);
        }

        pendingStrings.Clear();
    }

    private static void AppendLineBreakIfNeeded(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.AppendLine();
        }
    }

    private static string ReadPdfLiteralString(string content, ref int index)
    {
        var bytes = new List<byte>();
        var depth = 1;

        for (index++; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '\\' && index + 1 < content.Length)
            {
                var next = content[++index];
                switch (next)
                {
                    case 'n': bytes.Add((byte)'\n'); break;
                    case 'r': bytes.Add((byte)'\r'); break;
                    case 't': bytes.Add((byte)'\t'); break;
                    case 'b': bytes.Add(8); break;
                    case 'f': bytes.Add(12); break;
                    case '(':
                    case ')':
                    case '\\':
                        bytes.Add((byte)next);
                        break;
                    case '\r':
                        if (index + 1 < content.Length && content[index + 1] == '\n') index++;
                        break;
                    case '\n':
                        break;
                    default:
                        if (next is >= '0' and <= '7')
                        {
                            var octal = new StringBuilder();
                            octal.Append(next);
                            for (var count = 0; count < 2 && index + 1 < content.Length && content[index + 1] is >= '0' and <= '7'; count++)
                            {
                                octal.Append(content[++index]);
                            }

                            bytes.Add(Convert.ToByte(octal.ToString(), 8));
                        }
                        else
                        {
                            bytes.Add((byte)next);
                        }
                        break;
                }

                continue;
            }

            if (character == '(')
            {
                depth++;
                bytes.Add((byte)'(');
                continue;
            }

            if (character == ')')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                bytes.Add((byte)')');
                continue;
            }

            bytes.Add((byte)character);
        }

        return DecodePdfStringBytes(bytes.ToArray());
    }

    private static string ReadPdfHexString(string content, ref int index)
    {
        var hex = new StringBuilder();
        for (index++; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '>')
            {
                break;
            }

            if (Uri.IsHexDigit(character))
            {
                hex.Append(character);
            }
        }

        if (hex.Length % 2 == 1)
        {
            hex.Append('0');
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.ToString(i * 2, 2), 16);
        }

        return DecodePdfStringBytes(bytes);
    }

    private static string DecodePdfStringBytes(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return Encoding.Latin1.GetString(bytes);
    }

    private static int IndexOfAscii(byte[] bytes, string value, int startIndex)
    {
        if (bytes.Length == 0 || string.IsNullOrEmpty(value) || startIndex >= bytes.Length)
        {
            return -1;
        }

        var needle = Encoding.ASCII.GetBytes(value);
        for (var index = Math.Max(0, startIndex); index <= bytes.Length - needle.Length; index++)
        {
            var found = true;
            for (var needleIndex = 0; needleIndex < needle.Length; needleIndex++)
            {
                if (bytes[index + needleIndex] != needle[needleIndex])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return index;
            }
        }

        return -1;
    }

    private static string BuildPlainTextFromPdfPages(IReadOnlyList<string> pages)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < pages.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"--- Sayfa {index + 1} ---");
            }

            builder.AppendLine(NormalizeExtractedPdfText(pages[index]));
        }

        return builder.ToString().TrimEnd();
    }

    private static void WritePdfTextAsCsv(string outputPath, IReadOnlyList<string> pages)
    {
        var rows = BuildPdfTableRows(pages);
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(";", row.Select(EscapeCsvCell)));
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WritePdfTextAsHtml(string outputPath, IReadOnlyList<string> pages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><title>PDF Dönüşümü</title>");
        builder.AppendLine("<style>body{font-family:Arial, sans-serif;font-size:14px;line-height:1.45} pre{white-space:pre-wrap}</style></head><body>");
        for (var index = 0; index < pages.Count; index++)
        {
            builder.AppendLine($"<h2>Sayfa {index + 1}</h2>");
            builder.AppendLine($"<pre>{EscapeXml(NormalizeExtractedPdfText(pages[index]))}</pre>");
        }

        builder.AppendLine("</body></html>");
        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WritePdfTextAsRtf(string outputPath, IReadOnlyList<string> pages)
    {
        var builder = new StringBuilder();
        builder.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\fs22 ");
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            if (pageIndex > 0)
            {
                builder.Append(@"\page ");
            }

            foreach (var line in GetPdfTextLines(pages[pageIndex]))
            {
                builder.Append(EscapeRtf(line));
                builder.Append(@"\par ");
            }
        }

        builder.Append('}');
        File.WriteAllText(outputPath, builder.ToString(), Encoding.ASCII);
    }

    private static void WritePdfTextAsDocx(string outputPath, IReadOnlyList<string> pages)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>
""");
        AddZipTextEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>
""");

        var document = new StringBuilder();
        document.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        document.AppendLine("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            if (pageIndex > 0)
            {
                document.AppendLine("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
            }

            foreach (var line in GetPdfTextLines(pages[pageIndex]))
            {
                document.Append("<w:p><w:r><w:t xml:space=\"preserve\">");
                document.Append(EscapeXml(line));
                document.AppendLine("</w:t></w:r></w:p>");
            }
        }

        document.AppendLine("<w:sectPr/></w:body></w:document>");
        AddZipTextEntry(archive, "word/document.xml", document.ToString());
    }

    private static void WritePdfTextAsExcelHtml(string outputPath, IReadOnlyList<string> pages)
    {
        var rows = BuildPdfTableRows(pages);
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"></head><body><table border=\"1\">");
        foreach (var row in rows)
        {
            builder.Append("<tr>");
            foreach (var cell in row)
            {
                builder.Append("<td>");
                builder.Append(EscapeXml(cell));
                builder.Append("</td>");
            }
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</table></body></html>");
        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WritePdfTextAsXlsx(string outputPath, IReadOnlyList<string> pages)
    {
        var rows = BuildPdfTableRows(pages);
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
""");
        AddZipTextEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
""");
        AddZipTextEntry(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
""");
        AddZipTextEntry(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="PDF" sheetId="1" r:id="rId1"/></sheets></workbook>
""");
        AddZipTextEntry(archive, "xl/styles.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="1"><xf xfId="0"/></cellXfs></styleSheet>
""");

        var sheet = new StringBuilder();
        sheet.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        var rowNumber = 1;
        foreach (var row in rows.Take(1048576))
        {
            sheet.Append($"<row r=\"{rowNumber}\">");
            for (var columnIndex = 0; columnIndex < row.Count && columnIndex < 16384; columnIndex++)
            {
                var reference = $"{GetExcelColumnName(columnIndex + 1)}{rowNumber}";
                sheet.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
                sheet.Append(EscapeXml(row[columnIndex]));
                sheet.Append("</t></is></c>");
            }
            sheet.AppendLine("</row>");
            rowNumber++;
        }

        sheet.AppendLine("</sheetData></worksheet>");
        AddZipTextEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    private static void WritePdfTextAsOdt(string outputPath, IReadOnlyList<string> pages)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression);
        AddZipTextEntry(archive, "META-INF/manifest.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"><manifest:file-entry manifest:media-type="application/vnd.oasis.opendocument.text" manifest:full-path="/"/><manifest:file-entry manifest:media-type="text/xml" manifest:full-path="content.xml"/></manifest:manifest>
""");
        var content = new StringBuilder();
        content.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        content.AppendLine("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:text>");
        foreach (var page in pages)
        {
            foreach (var line in GetPdfTextLines(page))
            {
                content.Append("<text:p>");
                content.Append(EscapeXml(line));
                content.AppendLine("</text:p>");
            }
        }
        content.AppendLine("</office:text></office:body></office:document-content>");
        AddZipTextEntry(archive, "content.xml", content.ToString());
    }

    private static void WritePdfTextAsOds(string outputPath, IReadOnlyList<string> pages)
    {
        var rows = BuildPdfTableRows(pages);
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        AddZipTextEntry(archive, "mimetype", "application/vnd.oasis.opendocument.spreadsheet", CompressionLevel.NoCompression);
        AddZipTextEntry(archive, "META-INF/manifest.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"><manifest:file-entry manifest:media-type="application/vnd.oasis.opendocument.spreadsheet" manifest:full-path="/"/><manifest:file-entry manifest:media-type="text/xml" manifest:full-path="content.xml"/></manifest:manifest>
""");
        var content = new StringBuilder();
        content.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        content.AppendLine("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.2\"><office:body><office:spreadsheet><table:table table:name=\"PDF\">");
        foreach (var row in rows)
        {
            content.Append("<table:table-row>");
            foreach (var cell in row)
            {
                content.Append("<table:table-cell office:value-type=\"string\"><text:p>");
                content.Append(EscapeXml(cell));
                content.Append("</text:p></table:table-cell>");
            }
            content.AppendLine("</table:table-row>");
        }
        content.AppendLine("</table:table></office:spreadsheet></office:body></office:document-content>");
        AddZipTextEntry(archive, "content.xml", content.ToString());
    }

    private static List<List<string>> BuildPdfTableRows(IReadOnlyList<string> pages)
    {
        var rows = new List<List<string>>();
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            if (pageIndex > 0)
            {
                rows.Add(new List<string> { $"Sayfa {pageIndex + 1}" });
            }

            foreach (var line in GetPdfTextLines(pages[pageIndex]))
            {
                var cells = SplitPdfTextLineIntoCells(line);
                if (cells.Count > 0)
                {
                    rows.Add(cells);
                }
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new List<string> { "PDF metni çıkarılamadı" });
        }

        return rows;
    }

    private static List<string> SplitPdfTextLineIntoCells(string line)
    {
        var normalizedLine = (line ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedLine))
        {
            return new List<string>();
        }

        var tabCells = normalizedLine.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(cell => !string.IsNullOrWhiteSpace(cell))
            .ToList();
        if (tabCells.Count > 1)
        {
            return tabCells;
        }

        var spacedCells = Regex.Split(normalizedLine, @"\s{2,}")
            .Select(cell => cell.Trim())
            .Where(cell => !string.IsNullOrWhiteSpace(cell))
            .ToList();

        return spacedCells.Count > 1
            ? spacedCells
            : new List<string> { normalizedLine };
    }

    private static IEnumerable<string> GetPdfTextLines(string text)
    {
        return NormalizeExtractedPdfText(text)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string NormalizeExtractedPdfText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Replace("\0", string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\u00A0", " ")
            .Replace("\n", Environment.NewLine)
            .Trim();
    }

    private static string EscapeCsvCell(string value)
    {
        var normalized = value ?? string.Empty;
        return normalized.Contains(';') || normalized.Contains('"') || normalized.Contains('\n') || normalized.Contains('\r')
            ? $"\"{normalized.Replace("\"", "\"\"")}\""
            : normalized;
    }

    private static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string EscapeRtf(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value ?? string.Empty)
        {
            switch (character)
            {
                case '\\': builder.Append(@"\\"); break;
                case '{': builder.Append(@"\{"); break;
                case '}': builder.Append(@"\}"); break;
                case '\n': builder.Append(@"\par "); break;
                case '\r': break;
                default:
                    if (character <= 0x7f)
                    {
                        builder.Append(character);
                    }
                    else
                    {
                        builder.Append($@"\u{(short)character}?");
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private static string GetExcelColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return string.IsNullOrWhiteSpace(columnName) ? "A" : columnName;
    }

    private static void AddZipTextEntry(ZipArchive archive, string entryName, string content, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(entryName, compressionLevel);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static bool TryConvertImageToPdfBuiltIn(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsRasterImageFormat(sourceFormat) || NormalizeConvertTarget(targetFormat) != "PDF")
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            using var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(inputStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null)
            {
                errorMessage = "Görsel okunamadı.";
                return false;
            }

            var flattened = FlattenBitmapToWhiteBackground(frame);
            var jpegBytes = EncodeBitmapToJpegBytes(flattened, 94);
            if (jpegBytes.Length == 0)
            {
                errorMessage = "Görsel PDF içine gömülecek JPEG verisine dönüştürülemedi.";
                return false;
            }

            var dpiX = flattened.DpiX > 1 ? flattened.DpiX : 96;
            var dpiY = flattened.DpiY > 1 ? flattened.DpiY : 96;
            var pageWidth = Math.Max(1, flattened.PixelWidth * 72.0 / dpiX);
            var pageHeight = Math.Max(1, flattened.PixelHeight * 72.0 / dpiY);

            WriteSingleImagePdf(outputPath, jpegBytes, flattened.PixelWidth, flattened.PixelHeight, pageWidth, pageHeight);
            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryConvertRasterImageBuiltIn(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsBuiltInRasterImageCandidate(sourceFormat, targetFormat))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            using var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(inputStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null)
            {
                errorMessage = "Görsel okunamadı.";
                return false;
            }

            BitmapSource bitmap = frame;
            if (NormalizeConvertTarget(targetFormat) is "JPG" or "JPEG")
            {
                // JPEG alfa kanalını desteklemez. Şeffaf alanlar siyaha düşmesin diye beyaz zemine düzleştirilir.
                bitmap = FlattenBitmapToWhiteBackground(bitmap);
            }

            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            var encoder = CreateBitmapEncoder(targetFormat);
            if (encoder is null)
            {
                errorMessage = "Bu görsel hedef formatı dahili dönüştürücü tarafından desteklenmiyor.";
                return false;
            }

            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var outputStream = File.Create(outputPath);
            encoder.Save(outputStream);
            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryConvertPdfFirstPageWithDocnet(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        // İsim geriye dönük uyumluluk için korunuyor; artık PDF içindeki tüm sayfaları dışa aktarır.
        // Tek sayfalı PDF'lerde çıktı adı kullanıcının beklediği gibi aynen korunur.
        errorMessage = string.Empty;

        if (!IsPdfImageExportTarget(sourceFormat, targetFormat) || NormalizeConvertTarget(sourceFormat) != "PDF")
        {
            return false;
        }

        var docnetPath = FindDocnetCoreAssembly();
        if (string.IsNullOrWhiteSpace(docnetPath))
        {
            errorMessage = "Docnet.Core bulunamadı.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var pdfiumPath = Path.Combine(Path.GetDirectoryName(docnetPath) ?? string.Empty, "pdfium.dll");
            var nativeRenderError = string.Empty;
            if (File.Exists(pdfiumPath))
            {
                try
                {
                    NativeLibrary.Load(pdfiumPath);
                }
                catch
                {
                    // Daha önce yüklenmişse devam edilir.
                }

                // Öncelikli yol: PDFium native render.
                // Docnet GetImage bazı PDF'lerde alfa/arka plan bilgisini siyah döndürerek JPG/JPEG çıktısını okunamaz hale getirebiliyor.
                // Native PDFium bitmap'i önce beyaz zeminle doldurup sonra sayfayı render ettiğimiz için beyaz sayfalar siyah zemine dönmez.
                if (TryExportPdfPagesWithPdfiumNative(pdfiumPath, filePath, targetFormat, outputPath, out nativeRenderError))
                {
                    return true;
                }
            }

            var assembly = Assembly.LoadFrom(docnetPath);
            var docLibType = assembly.GetType("Docnet.Core.DocLib");
            var pageDimensionsType = assembly.GetType("Docnet.Core.Models.PageDimensions");
            if (docLibType is null || pageDimensionsType is null)
            {
                errorMessage = "Docnet.Core beklenen PDF okuma tiplerini içermiyor.";
                return false;
            }

            var instance = docLibType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? docLibType.GetProperty("__Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (instance is null)
            {
                errorMessage = "Docnet.Core örneği oluşturulamadı.";
                return false;
            }

            // Daha yüksek render ölçüsü küçük yazıların okunabilirliğini artırır.
            // 2480x3508 yaklaşık A4 300 DPI seviyesidir; metin netliği ile bellek tüketimi dengelenir.
            var width = 2480;
            var height = 3508;
            var dimensions = CreatePageDimensions(pageDimensionsType, width, height);
            if (dimensions is null)
            {
                errorMessage = "PDF sayfa boyutu oluşturulamadı.";
                return false;
            }

            var documentReaderObject = InvokeGetDocReader(instance, filePath, dimensions);
            if (documentReaderObject is null)
            {
                errorMessage = "PDF dosyası okunamadı.";
                return false;
            }

            using var documentReaderDisposer = documentReaderObject as IDisposable;
            var pageCount = GetPdfPageCount(documentReaderObject, pdfiumPath, filePath);
            if (pageCount <= 0)
            {
                errorMessage = "PDF sayfa sayısı okunamadı.";
                return false;
            }

            var renderedCount = 0;
            var firstOutputPath = outputPath;
            var outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            var outputExtension = Path.GetExtension(outputPath);
            var outputBaseName = Path.GetFileNameWithoutExtension(outputPath);

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                // İlk sayfa ana çıktı adını kullanır. Diğer sayfalar aynı klasörde _sayfa_002, _sayfa_003... olarak kaydedilir.
                var pageOutputPath = pageIndex == 0
                    ? outputPath
                    : GetUniqueOutputPath(outputDirectory, $"{outputBaseName}_sayfa_{pageIndex + 1:000}", outputExtension);

                using var pageReader = InvokeGetPageReader(documentReaderObject, pageIndex) as IDisposable;
                if (pageReader is null)
                {
                    errorMessage = $"PDF'in {pageIndex + 1}. sayfası okunamadı.";
                    return renderedCount > 0;
                }

                if (!TryRenderPdfPageReaderToImage(pageReader, targetFormat, pageOutputPath, out var pageError))
                {
                    errorMessage = string.IsNullOrWhiteSpace(pageError)
                        ? $"PDF'in {pageIndex + 1}. sayfası görsele dönüştürülemedi."
                        : $"PDF'in {pageIndex + 1}. sayfası dönüştürülemedi: {pageError}";
                    return renderedCount > 0;
                }

                if (renderedCount == 0)
                {
                    firstOutputPath = pageOutputPath;
                }

                renderedCount++;
            }

            if (renderedCount == 0)
            {
                errorMessage = "PDF içinde dışa aktarılacak sayfa bulunamadı.";
                return false;
            }

            // Çok sayfalı çıktıların ilk sayfasını kullanıcıya hızlı açılacak ana çıktı olarak bırakırız.
            if (!string.Equals(firstOutputPath, outputPath, StringComparison.OrdinalIgnoreCase) && !File.Exists(outputPath))
            {
                // OutputPath çağıran kod tarafından zaten atanmış olabilir; dosya yoksa ilk sayfa yolu açılır.
            }

            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            errorMessage = ex.InnerException.Message;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryExportPdfPagesWithPdfiumNative(string pdfiumPath, string filePath, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(pdfiumPath) || !File.Exists(pdfiumPath) || !File.Exists(filePath))
        {
            errorMessage = "PDFium dosyası bulunamadı.";
            return false;
        }

        IntPtr libraryHandle = IntPtr.Zero;
        IntPtr documentHandle = IntPtr.Zero;

        try
        {
            libraryHandle = NativeLibrary.Load(pdfiumPath);
            if (libraryHandle == IntPtr.Zero ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_InitLibrary", out var initLibraryPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadDocument", out var loadDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageCount", out var getPageCountPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadPage", out var loadPagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_ClosePage", out var closePagePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_Create", out var createBitmapPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_FillRect", out var fillRectPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_RenderPageBitmap", out var renderPageBitmapPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_GetBuffer", out var getBufferPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_GetStride", out var getStridePointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDFBitmap_Destroy", out var destroyBitmapPointer))
            {
                errorMessage = "PDFium native render fonksiyonları bulunamadı.";
                return false;
            }

            var initLibrary = Marshal.GetDelegateForFunctionPointer<FpdfInitLibraryDelegate>(initLibraryPointer);
            var loadDocument = Marshal.GetDelegateForFunctionPointer<FpdfLoadDocumentDelegate>(loadDocumentPointer);
            var getPageCount = Marshal.GetDelegateForFunctionPointer<FpdfGetPageCountDelegate>(getPageCountPointer);
            var loadPage = Marshal.GetDelegateForFunctionPointer<FpdfLoadPageDelegate>(loadPagePointer);
            var closePage = Marshal.GetDelegateForFunctionPointer<FpdfClosePageDelegate>(closePagePointer);
            var closeDocument = Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer);
            var createBitmap = Marshal.GetDelegateForFunctionPointer<FpdfBitmapCreateDelegate>(createBitmapPointer);
            var fillRect = Marshal.GetDelegateForFunctionPointer<FpdfBitmapFillRectDelegate>(fillRectPointer);
            var renderPageBitmap = Marshal.GetDelegateForFunctionPointer<FpdfRenderPageBitmapDelegate>(renderPageBitmapPointer);
            var getBuffer = Marshal.GetDelegateForFunctionPointer<FpdfBitmapGetBufferDelegate>(getBufferPointer);
            var getStride = Marshal.GetDelegateForFunctionPointer<FpdfBitmapGetStrideDelegate>(getStridePointer);
            var destroyBitmap = Marshal.GetDelegateForFunctionPointer<FpdfBitmapDestroyDelegate>(destroyBitmapPointer);

            initLibrary();
            documentHandle = loadDocument(filePath, null);
            if (documentHandle == IntPtr.Zero)
            {
                errorMessage = "PDFium PDF dosyasını açamadı.";
                return false;
            }

            var pageCount = Math.Max(0, getPageCount(documentHandle));
            if (pageCount == 0)
            {
                errorMessage = "PDFium sayfa sayısını okuyamadı.";
                return false;
            }

            var renderedCount = 0;
            var outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            var outputExtension = Path.GetExtension(outputPath);
            var outputBaseName = Path.GetFileNameWithoutExtension(outputPath);
            Directory.CreateDirectory(outputDirectory);

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var pageHandle = IntPtr.Zero;
                var bitmapHandle = IntPtr.Zero;

                try
                {
                    pageHandle = loadPage(documentHandle, pageIndex);
                    if (pageHandle == IntPtr.Zero)
                    {
                        errorMessage = $"PDFium {pageIndex + 1}. sayfayı açamadı.";
                        return renderedCount > 0;
                    }

                    var (renderWidth, renderHeight) = GetPdfiumRenderSize(libraryHandle, pageHandle);
                    bitmapHandle = createBitmap(renderWidth, renderHeight, 0);
                    if (bitmapHandle == IntPtr.Zero)
                    {
                        errorMessage = "PDFium bitmap oluşturamadı.";
                        return renderedCount > 0;
                    }

                    // 0xFFFFFFFF: beyaz zemin. PDFium sayfayı bunun üzerine işler.
                    // Bu adım eksikse bazı sayfalar JPG/JPEG çıktısında siyah zeminle oluşur.
                    fillRect(bitmapHandle, 0, 0, renderWidth, renderHeight, 0xFFFFFFFF);

                    const int renderAnnotations = 0x01;
                    const int lcdText = 0x02;
                    renderPageBitmap(bitmapHandle, pageHandle, 0, 0, renderWidth, renderHeight, 0, renderAnnotations | lcdText);

                    var buffer = getBuffer(bitmapHandle);
                    var stride = getStride(bitmapHandle);
                    if (buffer == IntPtr.Zero || stride <= 0)
                    {
                        errorMessage = "PDFium bitmap belleği okunamadı.";
                        return renderedCount > 0;
                    }

                    var outputBitmap = CreateWhiteBitmapSourceFromPdfiumNativeBuffer(buffer, stride, renderWidth, renderHeight);
                    if (outputBitmap.CanFreeze)
                    {
                        outputBitmap.Freeze();
                    }

                    var encoder = CreateBitmapEncoder(targetFormat);
                    if (encoder is null)
                    {
                        errorMessage = "Bu hedef görsel formatı desteklenmiyor.";
                        return false;
                    }

                    var pageOutputPath = pageIndex == 0
                        ? outputPath
                        : GetUniqueOutputPath(outputDirectory, $"{outputBaseName}_sayfa_{pageIndex + 1:000}", outputExtension);

                    encoder.Frames.Add(BitmapFrame.Create(outputBitmap));
                    using var outputStream = File.Create(pageOutputPath);
                    encoder.Save(outputStream);
                    renderedCount++;
                }
                finally
                {
                    if (bitmapHandle != IntPtr.Zero)
                    {
                        destroyBitmap(bitmapHandle);
                    }

                    if (pageHandle != IntPtr.Zero)
                    {
                        closePage(pageHandle);
                    }
                }
            }

            return renderedCount > 0;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            if (documentHandle != IntPtr.Zero)
            {
                try
                {
                    if (libraryHandle != IntPtr.Zero && NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer))
                    {
                        Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer)(documentHandle);
                    }
                }
                catch
                {
                    // Kapatma hatası dönüştürme sonucunu etkilemez.
                }
            }
        }
    }

    private static (int Width, int Height) GetPdfiumRenderSize(IntPtr libraryHandle, IntPtr pageHandle)
    {
        double pageWidthPoints = 595;
        double pageHeightPoints = 842;

        try
        {
            if (NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageWidthF", out var widthFPointer) &&
                NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageHeightF", out var heightFPointer))
            {
                var getWidthF = Marshal.GetDelegateForFunctionPointer<FpdfGetPageWidthFDelegate>(widthFPointer);
                var getHeightF = Marshal.GetDelegateForFunctionPointer<FpdfGetPageHeightFDelegate>(heightFPointer);
                pageWidthPoints = Math.Max(1, getWidthF(pageHandle));
                pageHeightPoints = Math.Max(1, getHeightF(pageHandle));
            }
            else if (NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageWidth", out var widthPointer) &&
                     NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageHeight", out var heightPointer))
            {
                var getWidth = Marshal.GetDelegateForFunctionPointer<FpdfGetPageWidthDelegate>(widthPointer);
                var getHeight = Marshal.GetDelegateForFunctionPointer<FpdfGetPageHeightDelegate>(heightPointer);
                pageWidthPoints = Math.Max(1, getWidth(pageHandle));
                pageHeightPoints = Math.Max(1, getHeight(pageHandle));
            }
        }
        catch
        {
            pageWidthPoints = 595;
            pageHeightPoints = 842;
        }

        const double targetDpi = 300d;
        const double minSide = 800d;
        const double maxSide = 4200d;
        var widthAtTargetDpi = Math.Max(1d, pageWidthPoints / 72d * targetDpi);
        var heightAtTargetDpi = Math.Max(1d, pageHeightPoints / 72d * targetDpi);
        var scale = 1d;

        if (widthAtTargetDpi > maxSide || heightAtTargetDpi > maxSide)
        {
            scale = Math.Min(maxSide / widthAtTargetDpi, maxSide / heightAtTargetDpi);
        }
        else if (widthAtTargetDpi < minSide && heightAtTargetDpi < minSide)
        {
            scale = Math.Max(minSide / widthAtTargetDpi, minSide / heightAtTargetDpi);
        }

        var width = Math.Max(1, (int)Math.Round(widthAtTargetDpi * scale));
        var height = Math.Max(1, (int)Math.Round(heightAtTargetDpi * scale));
        return (width, height);
    }

    private static BitmapSource CreateWhiteBitmapSourceFromPdfiumNativeBuffer(IntPtr buffer, int sourceStride, int width, int height)
    {
        var sourceLength = Math.Max(1, sourceStride * height);
        var source = new byte[sourceLength];
        Marshal.Copy(buffer, source, 0, sourceLength);

        var targetStride = width * 3;
        var rgb = new byte[Math.Max(1, height * targetStride)];
        Array.Fill(rgb, (byte)255);

        for (var y = 0; y < height; y++)
        {
            var sourceRow = y * sourceStride;
            var targetRow = y * targetStride;

            for (var x = 0; x < width; x++)
            {
                var sourceIndex = sourceRow + x * 4;
                var targetIndex = targetRow + x * 3;
                if (sourceIndex + 2 >= source.Length || targetIndex + 2 >= rgb.Length)
                {
                    continue;
                }

                rgb[targetIndex] = source[sourceIndex];
                rgb[targetIndex + 1] = source[sourceIndex + 1];
                rgb[targetIndex + 2] = source[sourceIndex + 2];
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, rgb, targetStride);
    }

    private static int GetPdfPageCount(object documentReader, string pdfiumPath, string filePath)
    {
        var nativePageCount = TryGetPdfPageCountWithPdfium(pdfiumPath, filePath);
        if (nativePageCount > 0)
        {
            return nativePageCount;
        }

        return TryReadIntMember(documentReader, "PageCount")
            ?? TryReadIntMember(documentReader, "PagesCount")
            ?? TryReadIntMember(documentReader, "PageCountInternal")
            ?? TryReadIntMember(documentReader, "_pageCount")
            ?? TryInvokeIntMethod(documentReader, "GetPageCount")
            ?? TryInvokeIntMethod(documentReader, "GetPagesCount")
            ?? 1;
    }

    private static int TryGetPdfPageCountWithPdfium(string pdfiumPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(pdfiumPath) || !File.Exists(pdfiumPath) || !File.Exists(filePath))
        {
            return 0;
        }

        IntPtr libraryHandle = IntPtr.Zero;
        IntPtr documentHandle = IntPtr.Zero;

        try
        {
            libraryHandle = NativeLibrary.Load(pdfiumPath);
            if (libraryHandle == IntPtr.Zero ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_InitLibrary", out var initLibraryPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_LoadDocument", out var loadDocumentPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_GetPageCount", out var getPageCountPointer) ||
                !NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer))
            {
                return 0;
            }

            var initLibrary = Marshal.GetDelegateForFunctionPointer<FpdfInitLibraryDelegate>(initLibraryPointer);
            var loadDocument = Marshal.GetDelegateForFunctionPointer<FpdfLoadDocumentDelegate>(loadDocumentPointer);
            var getPageCount = Marshal.GetDelegateForFunctionPointer<FpdfGetPageCountDelegate>(getPageCountPointer);
            var closeDocument = Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer);

            initLibrary();
            documentHandle = loadDocument(filePath, null);
            return documentHandle == IntPtr.Zero ? 0 : Math.Max(0, getPageCount(documentHandle));
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (documentHandle != IntPtr.Zero)
            {
                try
                {
                    if (libraryHandle != IntPtr.Zero && NativeLibrary.TryGetExport(libraryHandle, "FPDF_CloseDocument", out var closeDocumentPointer))
                    {
                        Marshal.GetDelegateForFunctionPointer<FpdfCloseDocumentDelegate>(closeDocumentPointer)(documentHandle);
                    }
                }
                catch
                {
                    // PDFium belge kapatma hatası dönüştürme akışını bozmamalı.
                }
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfInitLibraryDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FpdfLoadDocumentDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string filePath, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FpdfGetPageCountDelegate(IntPtr document);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfCloseDocumentDelegate(IntPtr document);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FpdfLoadPageDelegate(IntPtr document, int pageIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfClosePageDelegate(IntPtr page);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FpdfTextLoadPageDelegate(IntPtr page);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FpdfTextCountCharsDelegate(IntPtr textPage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint FpdfTextGetUnicodeDelegate(IntPtr textPage, int index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfTextClosePageDelegate(IntPtr textPage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FpdfBitmapCreateDelegate(int width, int height, int alpha);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfBitmapFillRectDelegate(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfRenderPageBitmapDelegate(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FpdfBitmapGetBufferDelegate(IntPtr bitmap);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FpdfBitmapGetStrideDelegate(IntPtr bitmap);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FpdfBitmapDestroyDelegate(IntPtr bitmap);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate float FpdfGetPageWidthFDelegate(IntPtr page);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate float FpdfGetPageHeightFDelegate(IntPtr page);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate double FpdfGetPageWidthDelegate(IntPtr page);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate double FpdfGetPageHeightDelegate(IntPtr page);

    private static bool TryRenderPdfPageReaderToImage(object pageReader, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        var width = TryReadIntMember(pageReader, "Width")
            ?? TryReadIntMember(pageReader, "PageWidth")
            ?? TryInvokeIntMethod(pageReader, "GetPageWidth")
            ?? 0;
        var height = TryReadIntMember(pageReader, "Height")
            ?? TryReadIntMember(pageReader, "PageHeight")
            ?? TryInvokeIntMethod(pageReader, "GetPageHeight")
            ?? 0;

        var getImageMethod = pageReader.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method =>
                method.Name == "GetImage" &&
                method.ReturnType == typeof(byte[]) &&
                method.GetParameters().Length == 0);

        if (getImageMethod is null)
        {
            errorMessage = "PDF görsel verisini alacak uygun GetImage metodu bulunamadı.";
            return false;
        }

        var imageBytes = getImageMethod.Invoke(pageReader, null) as byte[];
        if (imageBytes is null || imageBytes.Length == 0)
        {
            errorMessage = "PDF sayfası görsele dönüştürülemedi.";
            return false;
        }

        if (width <= 0 || height <= 0 || imageBytes.Length != width * height * 4)
        {
            if (!TryInferBitmapSize(imageBytes.Length, width, height, out width, out height))
            {
                errorMessage = "PDF görsel boyutu hesaplanamadı.";
                return false;
            }
        }

        var outputBitmap = CreateWhiteCompositedBitmapFromPdfiumBgra(imageBytes, width, height);
        if (outputBitmap.CanFreeze)
        {
            outputBitmap.Freeze();
        }

        var encoder = CreateBitmapEncoder(targetFormat);
        if (encoder is null)
        {
            errorMessage = "Bu hedef görsel formatı desteklenmiyor.";
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
        encoder.Frames.Add(BitmapFrame.Create(outputBitmap));
        using var outputStream = File.Create(outputPath);
        encoder.Save(outputStream);
        return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    private static BitmapSource CreateWhiteCompositedBitmapFromPdfiumBgra(byte[] source, int width, int height)
    {
        var pixelCount = Math.Min(width * height, source.Length / 4);
        var stride = width * 3;
        var rgb = new byte[Math.Max(1, height * stride)];
        Array.Fill(rgb, (byte)255);

        // Docnet GetImage yedek yoludur. Native PDFium render önceliklidir.
        // Bu yedek yolda alfa 0 + RGB 0 gelen pikseller siyah sayfa üretmesin diye beyaz kabul edilir.
        var useAlpha = ShouldCompositePdfiumAlpha(source, pixelCount);

        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            var sourceIndex = pixelIndex * 4;
            var targetIndex = pixelIndex * 3;
            var blue = source[sourceIndex];
            var green = source[sourceIndex + 1];
            var red = source[sourceIndex + 2];
            var alpha = source[sourceIndex + 3];

            if (alpha == 0 && blue <= 2 && green <= 2 && red <= 2)
            {
                rgb[targetIndex] = 255;
                rgb[targetIndex + 1] = 255;
                rgb[targetIndex + 2] = 255;
                continue;
            }

            rgb[targetIndex] = useAlpha ? CompositeChannelOnWhite(blue, alpha) : blue;
            rgb[targetIndex + 1] = useAlpha ? CompositeChannelOnWhite(green, alpha) : green;
            rgb[targetIndex + 2] = useAlpha ? CompositeChannelOnWhite(red, alpha) : red;
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, rgb, stride);
    }

    private static bool ShouldCompositePdfiumAlpha(byte[] source, int pixelCount)
    {
        if (pixelCount <= 0)
        {
            return false;
        }

        var inspectedPixels = Math.Min(pixelCount, 200000);
        var transparentPixels = 0;
        var semiTransparentPixels = 0;
        var coloredTransparentPixels = 0;
        var step = Math.Max(1, pixelCount / inspectedPixels);

        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex += step)
        {
            var sourceIndex = pixelIndex * 4;
            var blue = source[sourceIndex];
            var green = source[sourceIndex + 1];
            var red = source[sourceIndex + 2];
            var alpha = source[sourceIndex + 3];

            if (alpha == 0)
            {
                transparentPixels++;
                if (blue < 245 || green < 245 || red < 245)
                {
                    coloredTransparentPixels++;
                }
            }
            else if (alpha < 255)
            {
                semiTransparentPixels++;
            }
        }

        // Alfa kanalı 0 olmasına rağmen renk kanalları doluysa PDFium alfa bilgisi güvenilir değildir.
        if (coloredTransparentPixels > inspectedPixels / 100)
        {
            return false;
        }

        return semiTransparentPixels > 0 || transparentPixels > inspectedPixels / 100;
    }

    private static byte CompositeChannelOnWhite(byte channel, byte alpha)
    {
        if (alpha == 0)
        {
            return 255;
        }

        if (alpha == 255)
        {
            return channel;
        }

        // PDFium/Docnet bazı sayfalarda premultiplied, bazı sayfalarda straight alpha benzeri değer döndürebiliyor.
        // Kanal değeri alfa değerinden küçük/eşitse premultiplied kabul edip beyaza elle kompoze ederiz.
        // Böylece siyah zemin, gölgeli/okunmaz yazı ve alfa kaynaklı kararma engellenir.
        var blended = channel <= alpha
            ? channel + 255 - alpha
            : ((channel * alpha) + (255 * (255 - alpha)) + 127) / 255;

        return (byte)Math.Clamp(blended, 0, 255);
    }

    private static byte[] NormalizePdfiumBgraBuffer(byte[] source)
    {
        var normalized = new byte[source.Length];
        Buffer.BlockCopy(source, 0, normalized, 0, source.Length);

        if (normalized.Length < 4)
        {
            return normalized;
        }

        var pixelCount = normalized.Length / 4;
        var alphaZeroCount = 0;

        for (var index = 3; index < normalized.Length; index += 4)
        {
            if (normalized[index] == 0)
            {
                alphaZeroCount++;
            }
        }

        // PDFium/Docnet bazı PDF'lerde beyaz zeminleri B=0,G=0,R=0,A=0 olarak döndürür.
        // Eski davranış sadece alfa değerini 255 yaptığı için bu pikseller siyah zemine dönüşüyordu.
        // Alfa boş gelen piksellerde renk bilgisi yoksa beyaz zemin kabul edilir; renk bilgisi varsa korunur.
        if (alphaZeroCount > 0)
        {
            for (var index = 0; index + 3 < normalized.Length; index += 4)
            {
                var alpha = normalized[index + 3];
                if (alpha != 0)
                {
                    continue;
                }

                var blue = normalized[index];
                var green = normalized[index + 1];
                var red = normalized[index + 2];

                if (blue == 0 && green == 0 && red == 0)
                {
                    normalized[index] = 255;
                    normalized[index + 1] = 255;
                    normalized[index + 2] = 255;
                }

                normalized[index + 3] = 255;
            }
        }

        return normalized;
    }

    private static BitmapSource FlattenBitmapToWhiteBackground(BitmapSource source)
    {
        var width = Math.Max(1, source.PixelWidth);
        var height = Math.Max(1, source.PixelHeight);
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            context.DrawImage(source, new Rect(0, 0, width, height));
        }

        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();

        var flattened = new FormatConvertedBitmap(rendered, PixelFormats.Bgr24, null, 0);
        flattened.Freeze();
        return flattened;
    }

    private static byte[] EncodeBitmapToJpegBytes(BitmapSource bitmap, int qualityLevel)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(qualityLevel, 1, 100) };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void WriteSingleImagePdf(string outputPath, byte[] jpegBytes, int pixelWidth, int pixelHeight, double pageWidth, double pageHeight)
    {
        using var stream = File.Create(outputPath);
        var offsets = new List<long> { 0 };

        WriteAscii(stream, "%PDF-1.4\n%ToolBridge\n");

        offsets.Add(stream.Position);
        WriteAscii(stream, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets.Add(stream.Position);
        WriteAscii(stream, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets.Add(stream.Position);
        WriteAscii(stream, FormattableString.Invariant($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth:0.###} {pageHeight:0.###}] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>\nendobj\n"));

        var content = FormattableString.Invariant($"q\n{pageWidth:0.###} 0 0 {pageHeight:0.###} 0 0 cm\n/Im0 Do\nQ\n");
        var contentBytes = Encoding.ASCII.GetBytes(content);
        offsets.Add(stream.Position);
        WriteAscii(stream, FormattableString.Invariant($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n"));
        stream.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii(stream, "endstream\nendobj\n");

        offsets.Add(stream.Position);
        WriteAscii(stream, FormattableString.Invariant($"5 0 obj\n<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegBytes.Length} >>\nstream\n"));
        stream.Write(jpegBytes, 0, jpegBytes.Length);
        WriteAscii(stream, "\nendstream\nendobj\n");

        var xrefOffset = stream.Position;
        WriteAscii(stream, "xref\n0 6\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            WriteAscii(stream, FormattableString.Invariant($"{offsets[index]:0000000000} 00000 n \n"));
        }

        WriteAscii(stream, FormattableString.Invariant($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string FindDocnetCoreAssembly()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "Docnet.Core.dll"),
            Path.Combine(AppContext.BaseDirectory, "Docnet.Core.dll"),
            Path.Combine(AppContext.BaseDirectory, "Docnet", "Docnet.Core.dll")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static object? CreatePageDimensions(Type pageDimensionsType, int width, int height)
    {
        foreach (var constructor in pageDimensionsType.GetConstructors())
        {
            var parameters = constructor.GetParameters();
            try
            {
                if (parameters.Length == 2)
                {
                    return constructor.Invoke(new object[]
                    {
                        Convert.ChangeType(width, parameters[0].ParameterType, CultureInfo.InvariantCulture),
                        Convert.ChangeType(height, parameters[1].ParameterType, CultureInfo.InvariantCulture)
                    });
                }

                if (parameters.Length == 0)
                {
                    var instance = constructor.Invoke(null);
                    SetIntMember(instance, "Width", width);
                    SetIntMember(instance, "Height", height);
                    return instance;
                }
            }
            catch
            {
                // Bir sonraki kurucu denenir.
            }
        }

        return null;
    }

    private static object? InvokeGetDocReader(object docLib, string filePath, object dimensions)
    {
        foreach (var method in docLib.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.Name == "GetDocReader"))
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 2)
            {
                if (TryInvoke(method, docLib, new[] { filePath, dimensions }, out var reader))
                {
                    return reader;
                }
            }

            if (parameters.Length == 3)
            {
                if (TryInvoke(method, docLib, new object?[] { filePath, dimensions, null }, out var reader) ||
                    TryInvoke(method, docLib, new object?[] { filePath, string.Empty, dimensions }, out reader))
                {
                    return reader;
                }
            }
        }

        return null;
    }

    private static bool TryInvoke(MethodInfo method, object? instance, object?[] arguments, out object? result)
    {
        try
        {
            result = method.Invoke(instance, arguments);
            return result is not null;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    private static object? InvokeGetPageReader(object documentReader, int pageIndex)
    {
        var method = documentReader.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method => method.Name == "GetPageReader" && method.GetParameters().Length == 1);
        return method?.Invoke(documentReader, new object[] { pageIndex });
    }

    private static int? TryReadIntMember(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(instance) is object propertyValue && TryToInt(propertyValue, out var propertyInt))
        {
            return propertyInt;
        }

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (field?.GetValue(instance) is object fieldValue && TryToInt(fieldValue, out var fieldInt))
        {
            return fieldInt;
        }

        return null;
    }

    private static int? TryInvokeIntMethod(object instance, string methodName)
    {
        try
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method?.Invoke(instance, null) is object value && TryToInt(value, out var result))
            {
                return result;
            }
        }
        catch
        {
            // Yok sayılır.
        }

        return null;
    }

    private static void SetIntMember(object instance, string memberName, int value)
    {
        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite == true)
        {
            property.SetValue(instance, Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture));
        }
    }

    private static bool TryToInt(object value, out int result)
    {
        try
        {
            result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return result > 0;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private static bool TryInferBitmapSize(int byteLength, int preferredWidth, int preferredHeight, out int width, out int height)
    {
        width = preferredWidth;
        height = preferredHeight;

        if (byteLength <= 0 || byteLength % 4 != 0)
        {
            return false;
        }

        var pixels = byteLength / 4;
        if (preferredWidth > 0 && pixels % preferredWidth == 0)
        {
            height = pixels / preferredWidth;
            return height > 0;
        }

        if (preferredHeight > 0 && pixels % preferredHeight == 0)
        {
            width = pixels / preferredHeight;
            return width > 0;
        }

        var square = (int)Math.Sqrt(pixels);
        if (square * square == pixels)
        {
            width = square;
            height = square;
            return true;
        }

        return false;
    }

    private static BitmapEncoder? CreateBitmapEncoder(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) switch
        {
            "BMP" => new BmpBitmapEncoder(),
            "GIF" => new GifBitmapEncoder(),
            "JPG" or "JPEG" => new JpegBitmapEncoder { QualityLevel = 94 },
            "PNG" => new PngBitmapEncoder(),
            "TIF" or "TIFF" => new TiffBitmapEncoder(),
            _ => null
        };
    }

    private bool TryConvertWithLibreOffice(string filePath, string sourceFormat, string targetFormat, string outputFolder, out string outputPath, out string errorMessage)
    {
        outputPath = string.Empty;
        errorMessage = string.Empty;

        if (!IsLibreOfficeCandidate(sourceFormat, targetFormat))
        {
            return false;
        }

        if (IsLibreOfficeCrossFamilyDirectlyUnsupported(sourceFormat, targetFormat))
        {
            errorMessage = $"{NormalizeConvertTarget(sourceFormat)} -> {NormalizeConvertTarget(targetFormat)} doğrudan desteklenmiyor. PDF, XLSX/ODS/CSV veya uygun hedef format seçin.";
            return false;
        }

        var libreOfficePath = FindLibreOfficeExecutable();
        if (string.IsNullOrWhiteSpace(libreOfficePath))
        {
            errorMessage = "LibreOffice bulunamadı.";
            return false;
        }

        var libreTarget = MapLibreOfficeTargetFormat(sourceFormat, targetFormat);
        if (string.IsNullOrWhiteSpace(libreTarget))
        {
            errorMessage = "LibreOffice hedef formatı belirlenemedi.";
            return false;
        }

        var targetExtension = GetTargetExtension(targetFormat);
        var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridgeConvert", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempFolder);
            var arguments = $"--headless --nologo --nofirststartwizard --convert-to {libreTarget} --outdir {Quote(tempFolder)} {Quote(filePath)}";
            if (!RunHiddenProcess(libreOfficePath, arguments, 180000, out var standardOutput, out var standardError, out var exitCode))
            {
                errorMessage = "LibreOffice işlemi başlatılamadı.";
                return false;
            }

            var convertedFile = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories)
                .FirstOrDefault(file => string.Equals(Path.GetExtension(file), targetExtension, StringComparison.OrdinalIgnoreCase))
                ?? Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories).FirstOrDefault();

            if (exitCode != 0 || string.IsNullOrWhiteSpace(convertedFile) || !File.Exists(convertedFile))
            {
                var detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
                errorMessage = string.IsNullOrWhiteSpace(detail)
                    ? $"LibreOffice çıkış kodu: {exitCode}."
                    : $"LibreOffice çıkış kodu: {exitCode}. {detail}";
                return false;
            }

            var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(filePath));
            outputPath = GetUniqueOutputPath(outputFolder, baseName, targetExtension);
            Directory.CreateDirectory(outputFolder);
            File.Move(convertedFile, outputPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempFolder);
        }
    }

    private static string FindLibreOfficeExecutable()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "LibreOfficePortable", "App", "LibreOffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "LibreOffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
        };

        AddLibreOfficeProgramFilesCandidates(possiblePaths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddLibreOfficeProgramFilesCandidates(possiblePaths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("soffice.exe") ?? string.Empty;
    }

    private static void AddLibreOfficeProgramFilesCandidates(List<string> possiblePaths, string programRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(programRoot) || !Directory.Exists(programRoot))
            {
                return;
            }

            possiblePaths.AddRange(Directory.EnumerateDirectories(programRoot, "LibreOffice*", SearchOption.TopDirectoryOnly)
                .Select(folder => Path.Combine(folder, "program", "soffice.exe")));
        }
        catch
        {
            // Program Files taranamazsa PATH kontrolü devam eder.
        }
    }

    private static string MapLibreOfficeTargetFormat(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);

        if (target == "PDF")
        {
            if (IsLibreOfficeCalcFamily(source))
            {
                return "pdf:calc_pdf_Export";
            }

            if (IsLibreOfficeImpressFamily(source))
            {
                return "pdf:impress_pdf_Export";
            }

            if (IsImageFormat(source))
            {
                return "pdf:draw_pdf_Export";
            }

            return "pdf:writer_pdf_Export";
        }

        return target switch
        {
            "JPEG" => "jpg",
            "TIFF" => "tiff",
            "HTML" => "html",
            var value when !string.IsNullOrWhiteSpace(value) => value.ToLowerInvariant(),
            _ => string.Empty
        };
    }

    private static bool TryConvertWithImageMagick(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsImageMagickCandidate(sourceFormat, targetFormat))
        {
            return false;
        }

        var executable = FindImageMagickExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            errorMessage = "ImageMagick bulunamadı.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var input = RequiresFirstPageSelector(sourceFormat, targetFormat) ? $"{filePath}[0]" : filePath;
            var arguments = $"-quiet {Quote(input)} {Quote(outputPath)}";

            if (!RunHiddenProcess(executable, arguments, 180000, out var standardOutput, out var standardError, out var exitCode))
            {
                errorMessage = "ImageMagick işlemi başlatılamadı.";
                return false;
            }

            if (exitCode == 0 && File.Exists(outputPath))
            {
                return true;
            }

            var detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
            errorMessage = string.IsNullOrWhiteSpace(detail) ? $"ImageMagick çıkış kodu: {exitCode}." : detail;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string FindImageMagickExecutable()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "ImageMagick", "magick.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "magick.exe"),
            Path.Combine(AppContext.BaseDirectory, "ImageMagick", "magick.exe"),
            Path.Combine(AppContext.BaseDirectory, "magick.exe")
        };

        foreach (var programRoot in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            try
            {
                if (Directory.Exists(programRoot))
                {
                    possiblePaths.AddRange(Directory.EnumerateDirectories(programRoot, "ImageMagick*", SearchOption.TopDirectoryOnly)
                        .Select(folder => Path.Combine(folder, "magick.exe")));
                }
            }
            catch
            {
                // Program Files taranamazsa PATH kontrolü devam eder.
            }
        }

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("magick.exe");
    }

    private static bool TryConvertWithFFmpeg(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsMediaFormat(sourceFormat) || !IsMediaFormat(targetFormat))
        {
            return false;
        }

        var executable = FindExecutableWithToolFallback("ffmpeg.exe", "FFmpeg", "bin");
        if (string.IsNullOrWhiteSpace(executable))
        {
            errorMessage = "FFmpeg bulunamadı.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var arguments = $"-y -hide_banner -loglevel error -i {Quote(filePath)} {Quote(outputPath)}";
            if (!RunHiddenProcess(executable, arguments, 180000, out var standardOutput, out var standardError, out var exitCode))
            {
                errorMessage = "FFmpeg işlemi başlatılamadı.";
                return false;
            }

            if (exitCode == 0 && File.Exists(outputPath))
            {
                return true;
            }

            var detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
            errorMessage = string.IsNullOrWhiteSpace(detail) ? $"FFmpeg çıkış kodu: {exitCode}." : detail;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryConvertWithCalibre(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsCalibreCandidate(sourceFormat, targetFormat))
        {
            return false;
        }

        var executable = FindExecutableWithToolFallback("ebook-convert.exe", "Calibre");
        if (string.IsNullOrWhiteSpace(executable))
        {
            errorMessage = "Calibre ebook-convert bulunamadı.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var arguments = $"{Quote(filePath)} {Quote(outputPath)}";
            if (!RunHiddenProcess(executable, arguments, 180000, out var standardOutput, out var standardError, out var exitCode))
            {
                errorMessage = "Calibre işlemi başlatılamadı.";
                return false;
            }

            if (exitCode == 0 && File.Exists(outputPath))
            {
                return true;
            }

            var detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
            errorMessage = string.IsNullOrWhiteSpace(detail) ? $"Calibre çıkış kodu: {exitCode}." : detail;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryConvertWithInkscape(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsInkscapeCandidate(sourceFormat, targetFormat))
        {
            return false;
        }

        var executable = FindInkscapeExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            errorMessage = "Inkscape bulunamadı.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var arguments = $"{Quote(filePath)} --export-filename={Quote(outputPath)}";
            if (!RunHiddenProcess(executable, arguments, 180000, out var standardOutput, out var standardError, out var exitCode))
            {
                errorMessage = "Inkscape işlemi başlatılamadı.";
                return false;
            }

            if (exitCode == 0 && File.Exists(outputPath))
            {
                return true;
            }

            var detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
            errorMessage = string.IsNullOrWhiteSpace(detail) ? $"Inkscape çıkış kodu: {exitCode}." : detail;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string FindInkscapeExecutable()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "inkscape.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Inkscape", "bin", "inkscape.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Inkscape", "bin", "inkscape.exe")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("inkscape.exe");
    }

    private static bool TryConvertWithFontForge(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsFontFormat(sourceFormat) || !IsFontFormat(targetFormat))
        {
            return false;
        }

        var executable = FindExecutableWithToolFallback("fontforge.exe", "FontForge", "bin");
        if (string.IsNullOrWhiteSpace(executable))
        {
            errorMessage = "FontForge bulunamadı.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var script = "Open($1); Generate($2)";
            var arguments = $"-lang=ff -c {Quote(script)} {Quote(filePath)} {Quote(outputPath)}";
            if (!RunHiddenProcess(executable, arguments, 180000, out var standardOutput, out var standardError, out var exitCode))
            {
                errorMessage = "FontForge işlemi başlatılamadı.";
                return false;
            }

            if (exitCode == 0 && File.Exists(outputPath))
            {
                return true;
            }

            var detail = !string.IsNullOrWhiteSpace(standardError) ? standardError : standardOutput;
            errorMessage = string.IsNullOrWhiteSpace(detail) ? $"FontForge çıkış kodu: {exitCode}." : detail;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool TryConvertWithMicrosoftOffice(string filePath, string sourceFormat, string targetFormat, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (IsWordFormat(sourceFormat) && IsWordExportTarget(targetFormat))
        {
            return TryConvertWordDocument(filePath, outputPath, targetFormat, out errorMessage);
        }

        if (IsSpreadsheetFormat(sourceFormat) && IsExcelExportTarget(targetFormat))
        {
            return TryConvertExcelDocument(filePath, outputPath, targetFormat, out errorMessage);
        }

        if (IsPresentationFormat(sourceFormat) && IsPowerPointExportTarget(targetFormat))
        {
            return TryConvertPowerPointDocument(filePath, outputPath, targetFormat, out errorMessage);
        }

        return false;
    }

    private static bool TryConvertWordDocument(string filePath, string outputPath, string targetFormat, out string errorMessage)
    {
        errorMessage = string.Empty;
        dynamic? word = null;
        dynamic? document = null;

        try
        {
            var wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType is null)
            {
                errorMessage = "Microsoft Word yüklü değil.";
                return false;
            }

            word = Activator.CreateInstance(wordType);
            if (word is null)
            {
                errorMessage = "Microsoft Word başlatılamadı.";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            word.Visible = false;
            word.DisplayAlerts = 0;
            document = word.Documents.Open(filePath, false, true, false);

            switch (NormalizeConvertTarget(targetFormat))
            {
                case "PDF":
                    document.ExportAsFixedFormat(outputPath, 17);
                    break;
                case "DOCX":
                    document.SaveAs2(outputPath, 16);
                    break;
                case "DOC":
                    document.SaveAs2(outputPath, 0);
                    break;
                case "RTF":
                    document.SaveAs2(outputPath, 6);
                    break;
                case "TXT":
                    document.SaveAs2(outputPath, 2);
                    break;
                case "HTML":
                    document.SaveAs2(outputPath, 8);
                    break;
                default:
                    errorMessage = "Word bu hedefe dışa aktaramıyor.";
                    return false;
            }

            document.Close(false);
            word.Quit(false);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            try { document?.Close(false); } catch { }
            try { word?.Quit(false); } catch { }
            return false;
        }
    }

    private static bool TryConvertExcelDocument(string filePath, string outputPath, string targetFormat, out string errorMessage)
    {
        errorMessage = string.Empty;
        dynamic? excel = null;
        dynamic? workbook = null;

        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType is null)
            {
                errorMessage = "Microsoft Excel yüklü değil.";
                return false;
            }

            excel = Activator.CreateInstance(excelType);
            if (excel is null)
            {
                errorMessage = "Microsoft Excel başlatılamadı.";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbook = excel.Workbooks.Open(filePath, ReadOnly: true);

            switch (NormalizeConvertTarget(targetFormat))
            {
                case "PDF":
                    workbook.ExportAsFixedFormat(0, outputPath);
                    break;
                case "XLSX":
                    workbook.SaveAs(outputPath, 51);
                    break;
                case "XLS":
                    workbook.SaveAs(outputPath, -4143);
                    break;
                case "CSV":
                    workbook.SaveAs(outputPath, 6);
                    break;
                default:
                    errorMessage = "Excel bu hedefe dışa aktaramıyor.";
                    return false;
            }

            workbook.Close(false);
            excel.Quit();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            try { workbook?.Close(false); } catch { }
            try { excel?.Quit(); } catch { }
            return false;
        }
    }

    private static bool TryConvertPowerPointDocument(string filePath, string outputPath, string targetFormat, out string errorMessage)
    {
        errorMessage = string.Empty;
        dynamic? powerPoint = null;
        dynamic? presentation = null;

        try
        {
            var powerPointType = Type.GetTypeFromProgID("PowerPoint.Application");
            if (powerPointType is null)
            {
                errorMessage = "Microsoft PowerPoint yüklü değil.";
                return false;
            }

            powerPoint = Activator.CreateInstance(powerPointType);
            if (powerPoint is null)
            {
                errorMessage = "Microsoft PowerPoint başlatılamadı.";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            presentation = powerPoint.Presentations.Open(filePath, true, false, false);

            switch (NormalizeConvertTarget(targetFormat))
            {
                case "PDF":
                    presentation.SaveAs(outputPath, 32);
                    break;
                case "PPTX":
                    presentation.SaveAs(outputPath, 24);
                    break;
                case "PPT":
                    presentation.SaveAs(outputPath, 1);
                    break;
                case "PNG":
                    presentation.SaveAs(outputPath, 18);
                    break;
                case "JPG":
                case "JPEG":
                    presentation.SaveAs(outputPath, 17);
                    break;
                default:
                    errorMessage = "PowerPoint bu hedefe dışa aktaramıyor.";
                    return false;
            }

            presentation.Close();
            powerPoint.Quit();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            try { presentation?.Close(); } catch { }
            try { powerPoint?.Quit(); } catch { }
            return false;
        }
    }

    private static bool TryConvertArchive(string filePath, string sourceFormat, string targetFormat, string outputFolder, out string outputPath, out string errorMessage)
    {
        outputPath = string.Empty;
        errorMessage = string.Empty;

        if (!IsArchiveFormat(sourceFormat) && !IsArchiveFormat(targetFormat))
        {
            return false;
        }

        var targetExtension = GetTargetExtension(targetFormat);
        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(filePath));
        outputPath = GetUniqueOutputPath(outputFolder, baseName, targetExtension);

        if (!IsArchiveFormat(targetFormat))
        {
            errorMessage = "Arşiv kaynakları yalnızca arşiv formatlarına dönüştürülebilir.";
            return false;
        }

        if (targetFormat == "ZIP" && !IsArchiveFormat(sourceFormat))
        {
            try
            {
                Directory.CreateDirectory(outputFolder);
                using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath), CompressionLevel.Optimal);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        var sevenZip = Find7ZipExecutable();
        if (string.IsNullOrWhiteSpace(sevenZip))
        {
            errorMessage = "7-Zip bulunamadı.";
            return false;
        }

        if (targetFormat == "RAR")
        {
            errorMessage = "7-Zip RAR oluşturamaz; RAR hedefi için WinRAR gerekir.";
            return false;
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), "ToolBridgeArchive", Guid.NewGuid().ToString("N"));
        var extractFolder = Path.Combine(tempFolder, "in");

        try
        {
            Directory.CreateDirectory(extractFolder);
            if (IsArchiveFormat(sourceFormat))
            {
                var extractArguments = $"x -y -o{Quote(extractFolder)} {Quote(filePath)}";
                if (!RunHiddenProcess(sevenZip, extractArguments, 180000, out var extractOutput, out var extractError, out var extractExitCode) || extractExitCode != 0)
                {
                    var detail = !string.IsNullOrWhiteSpace(extractError) ? extractError : extractOutput;
                    errorMessage = string.IsNullOrWhiteSpace(detail) ? $"7-Zip çıkarma çıkış kodu: {extractExitCode}." : detail;
                    return false;
                }
            }
            else
            {
                File.Copy(filePath, Path.Combine(extractFolder, Path.GetFileName(filePath)), overwrite: false);
            }

            Directory.CreateDirectory(outputFolder);
            if (targetFormat is "TAR.GZ" or "TGZ" or "TAR.BZ2" or "TAR.XZ")
            {
                var tempTar = Path.Combine(tempFolder, "archive.tar");
                if (!RunHiddenProcess(sevenZip, $"a -ttar {Quote(tempTar)} {Quote(Path.Combine(extractFolder, "*"))}", 180000, out var tarOutput, out var tarError, out var tarExitCode) || tarExitCode != 0)
                {
                    errorMessage = !string.IsNullOrWhiteSpace(tarError) ? tarError : tarOutput;
                    return false;
                }

                var compressionType = targetFormat switch
                {
                    "TAR.BZ2" => "bzip2",
                    "TAR.XZ" => "xz",
                    _ => "gzip"
                };

                if (!RunHiddenProcess(sevenZip, $"a -t{compressionType} {Quote(outputPath)} {Quote(tempTar)}", 180000, out var compressedOutput, out var compressedError, out var compressedExitCode) || compressedExitCode != 0)
                {
                    errorMessage = !string.IsNullOrWhiteSpace(compressedError) ? compressedError : compressedOutput;
                    return false;
                }

                return File.Exists(outputPath);
            }

            var archiveType = targetFormat switch
            {
                "7Z" => "7z",
                "ZIP" => "zip",
                "TAR" => "tar",
                "GZ" => "gzip",
                "BZ2" => "bzip2",
                _ => targetFormat.ToLowerInvariant()
            };

            if (!RunHiddenProcess(sevenZip, $"a -t{archiveType} {Quote(outputPath)} {Quote(Path.Combine(extractFolder, "*"))}", 180000, out var packOutput, out var packError, out var packExitCode) || packExitCode != 0)
            {
                errorMessage = !string.IsNullOrWhiteSpace(packError) ? packError : packOutput;
                return false;
            }

            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempFolder);
        }
    }

    private static string Find7ZipExecutable()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("7z.exe");
    }

    private static bool TryConvertText(string filePath, string outputPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            var text = File.ReadAllText(filePath);
            File.WriteAllText(outputPath, text, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string GetSafeWorkingDirectory(string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(fileName);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : AppContext.BaseDirectory;
        }
        catch
        {
            return AppContext.BaseDirectory;
        }
    }

    private static bool RunHiddenProcess(string fileName, string arguments, int timeoutMilliseconds, out string standardOutput, out string standardError, out int exitCode)
    {
        standardOutput = string.Empty;
        standardError = string.Empty;
        exitCode = -1;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = GetSafeWorkingDirectory(fileName),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                standardOutput = GetCompletedProcessOutput(outputTask);
                standardError = "İşlem zaman aşımına uğradı.";
                exitCode = -1;
                return true;
            }

            Task.WaitAll(new Task[] { outputTask, errorTask }, TimeSpan.FromSeconds(2));
            standardOutput = GetCompletedProcessOutput(outputTask);
            standardError = GetCompletedProcessOutput(errorTask);
            exitCode = process.ExitCode;
            return true;
        }
        catch (Exception ex)
        {
            standardError = ex.Message;
            return false;
        }
    }

    private static string GetCompletedProcessOutput(Task<string> outputTask)
    {
        if (!outputTask.IsCompletedSuccessfully)
        {
            return string.Empty;
        }

        try
        {
            return outputTask.Result.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AddConversionError(ICollection<string> errors, string engineName, string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return;
        }

        errors.Add($"{engineName}: {error}");
    }

    private static bool FormatsAreEquivalent(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);

        if (source == target)
        {
            return true;
        }

        return (source, target) is ("JPG", "JPEG") or ("JPEG", "JPG") or ("TIF", "TIFF") or ("TIFF", "TIF") or ("HTM", "HTML") or ("HTML", "HTM");
    }

    private static string NormalizeConvertTarget(string? targetFormat)
    {
        return string.IsNullOrWhiteSpace(targetFormat)
            ? string.Empty
            : targetFormat.Trim().TrimStart('.').ToUpperInvariant();
    }

    private static string NormalizeSourceFormat(string filePath)
    {
        var name = Path.GetFileName(filePath).ToUpperInvariant();
        if (name.EndsWith(".TAR.GZ", StringComparison.OrdinalIgnoreCase)) return "TAR.GZ";
        if (name.EndsWith(".TGZ", StringComparison.OrdinalIgnoreCase)) return "TGZ";
        if (name.EndsWith(".TAR.BZ2", StringComparison.OrdinalIgnoreCase)) return "TAR.BZ2";
        if (name.EndsWith(".TAR.XZ", StringComparison.OrdinalIgnoreCase)) return "TAR.XZ";

        return Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant() switch
        {
            "HTM" => "HTML",
            "JPEG" => "JPG",
            "TIF" => "TIFF",
            "MPE" => "MPEG",
            var value => value
        };
    }

    private static string GetTargetExtension(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) switch
        {
            "JPG" => ".jpg",
            "JPEG" => ".jpg",
            "TIFF" => ".tiff",
            "HTML" => ".html",
            "TAR.GZ" => ".tar.gz",
            "TGZ" => ".tgz",
            "TAR.BZ2" => ".tar.bz2",
            "TAR.XZ" => ".tar.xz",
            var value when !string.IsNullOrWhiteSpace(value) => $".{value.ToLowerInvariant()}",
            _ => ".out"
        };
    }

    private static bool IsKnownConvertFormat(string format)
    {
        return IsArchiveFormat(format) || IsDocumentFormat(format) || IsSpreadsheetFormat(format) || IsPresentationFormat(format) ||
               IsImageFormat(format) || IsEbookFormat(format) || IsMediaFormat(format) || IsFontFormat(format) || IsCadFormat(format);
    }

    private static bool IsArchiveFormat(string format)
    {
        return NormalizeConvertTarget(format) is "7Z" or "BZ2" or "RAR" or "TAR" or "GZ" or "TAR.GZ" or "TGZ" or "TAR.BZ2" or "TAR.XZ" or "ZIP";
    }

    private static bool IsDocumentFormat(string format)
    {
        return NormalizeConvertTarget(format) is "ABW" or "DJVU" or "DOC" or "DOCM" or "DOCX" or "DOT" or "DOTX" or "HTML" or "HTM" or "HWP" or "LWP" or "MD" or "ODT" or "PAGES" or "PDF" or "RST" or "RTF" or "TEX" or "TXT" or "WPD" or "WPS" or "ZABW";
    }

    private static bool IsWordFormat(string format)
    {
        return NormalizeConvertTarget(format) is "DOC" or "DOCM" or "DOCX" or "DOT" or "DOTX" or "HTML" or "HTM" or "MD" or "ODT" or "PDF" or "RTF" or "TXT";
    }

    private static bool IsSpreadsheetFormat(string format)
    {
        return NormalizeConvertTarget(format) is "CSV" or "ODS" or "XLS" or "XLSM" or "XLSX" or "NUMBERS";
    }

    private static bool IsPresentationFormat(string format)
    {
        return NormalizeConvertTarget(format) is "KEY" or "ODP" or "PPS" or "PPSX" or "PPT" or "PPTX";
    }

    private static bool IsImageFormat(string format)
    {
        return NormalizeConvertTarget(format) is "AVIF" or "BMP" or "EPS" or "GIF" or "HEIC" or "ICO" or "JPEG" or "JPG" or "PNG" or "PSD" or "SVG" or "TGA" or "TIF" or "TIFF" or "WEBP";
    }

    private static bool IsRasterImageFormat(string format)
    {
        return NormalizeConvertTarget(format) is "BMP" or "GIF" or "ICO" or "JPEG" or "JPG" or "PNG" or "TIF" or "TIFF";
    }

    private static bool IsBuiltInRasterImageCandidate(string sourceFormat, string targetFormat)
    {
        return IsRasterImageFormat(sourceFormat) && NormalizeConvertTarget(targetFormat) is "BMP" or "GIF" or "JPEG" or "JPG" or "PNG" or "TIF" or "TIFF";
    }

    private static bool IsPdfImageExportTarget(string sourceFormat, string targetFormat)
    {
        return (NormalizeConvertTarget(sourceFormat) == "PDF" || IsDocumentFormat(sourceFormat) || IsSpreadsheetFormat(sourceFormat) || IsPresentationFormat(sourceFormat))
               && NormalizeConvertTarget(targetFormat) is "JPEG" or "JPG" or "PNG";
    }

    private static bool IsEbookFormat(string format)
    {
        return NormalizeConvertTarget(format) is "AZW" or "AZW3" or "EPUB" or "FB2" or "LIT" or "LRF" or "MOBI" or "OEB" or "PDB" or "TCR";
    }

    private static bool IsMediaFormat(string format)
    {
        return NormalizeConvertTarget(format) is "AAC" or "AIFF" or "AVI" or "FLAC" or "M4A" or "MKV" or "MOV" or "MP3" or "MP4" or "MPEG" or "OGG" or "OPUS" or "WAV" or "WEBM" or "WMA" or "WMV";
    }

    private static bool IsFontFormat(string format)
    {
        return NormalizeConvertTarget(format) is "OTF" or "TTF" or "WOFF" or "WOFF2";
    }

    private static bool IsCadFormat(string format)
    {
        return NormalizeConvertTarget(format) is "DWG" or "DXF";
    }

    private static bool IsTextTarget(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) is "TXT" or "CSV" or "MD" or "RST" or "TEX";
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".ico" or ".svg" or ".heic" or ".avif" or ".tga";
    }

    private static bool IsTextExtension(string extension)
    {
        return extension.ToLowerInvariant() is ".txt" or ".log" or ".csv" or ".json" or ".xml" or ".md" or ".rst" or ".tex";
    }

    private static bool IsLibreOfficeWriterFamily(string format)
    {
        return NormalizeConvertTarget(format) is "ABW" or "DOC" or "DOCM" or "DOCX" or "DOT" or "DOTX" or "HWP" or "MD" or "ODT" or "RTF" or "TXT" or "WPD" or "WPS" or "ZABW";
    }

    private static bool IsLibreOfficeCalcFamily(string format)
    {
        return NormalizeConvertTarget(format) is "CSV" or "NUMBERS" or "ODS" or "XLS" or "XLSM" or "XLSX";
    }

    private static bool IsLibreOfficeImpressFamily(string format)
    {
        return NormalizeConvertTarget(format) is "ODP" or "PPS" or "PPSX" or "PPT" or "PPTX";
    }

    private static bool IsLibreOfficeCrossFamilyDirectlyUnsupported(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || target == "PDF")
        {
            return false;
        }

        var sourceFamily = IsLibreOfficeWriterFamily(source) ? "writer"
            : IsLibreOfficeCalcFamily(source) ? "calc"
            : IsLibreOfficeImpressFamily(source) ? "impress"
            : string.Empty;

        var targetFamily = IsLibreOfficeWriterFamily(target) ? "writer"
            : IsLibreOfficeCalcFamily(target) ? "calc"
            : IsLibreOfficeImpressFamily(target) ? "impress"
            : string.Empty;

        return !string.IsNullOrWhiteSpace(sourceFamily) &&
               !string.IsNullOrWhiteSpace(targetFamily) &&
               !string.Equals(sourceFamily, targetFamily, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLibreOfficeCandidate(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);
        var libreInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ABW", "DOC", "DOCM", "DOCX", "DOT", "DOTX", "HTML", "HWP", "MD", "ODT", "PDF", "RTF", "TXT", "WPD", "WPS", "ZABW",
            "CSV", "NUMBERS", "ODS", "XLS", "XLSM", "XLSX",
            "ODP", "PPS", "PPSX", "PPT", "PPTX",
            "BMP", "GIF", "JPEG", "JPG", "PNG", "SVG", "TIFF", "WEBP"
        };
        var libreOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DOC", "DOCX", "HTML", "ODT", "PDF", "RTF", "TXT",
            "CSV", "ODS", "XLS", "XLSX",
            "ODP", "PPT", "PPTX",
            "BMP", "GIF", "JPEG", "JPG", "PNG", "SVG", "TIFF", "WEBP"
        };

        return libreInputs.Contains(source) && libreOutputs.Contains(target);
    }

    private static bool IsImageMagickCandidate(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);
        var rasterVectorAndPdf = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AVIF", "BMP", "DJVU", "EPS", "GIF", "HEIC", "ICO", "JPEG", "JPG", "PDF", "PNG", "PSD", "SVG", "TGA", "TIF", "TIFF", "WEBP"
        };

        return rasterVectorAndPdf.Contains(source) && rasterVectorAndPdf.Contains(target);
    }

    private static bool RequiresFirstPageSelector(string sourceFormat, string targetFormat)
    {
        return (NormalizeConvertTarget(sourceFormat) is "PDF" or "DJVU") && IsImageFormat(targetFormat);
    }

    private static bool IsCalibreCandidate(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);
        var calibreFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AZW", "AZW3", "DOCX", "EPUB", "FB2", "HTML", "LIT", "LRF", "MOBI", "OEB", "PDB", "PDF", "RTF", "TXT", "TCR"
        };

        return calibreFormats.Contains(source) && calibreFormats.Contains(target);
    }

    private static bool IsInkscapeCandidate(string sourceFormat, string targetFormat)
    {
        var source = NormalizeConvertTarget(sourceFormat);
        var target = NormalizeConvertTarget(targetFormat);
        return (source is "SVG" or "PDF" or "EPS") && (target is "SVG" or "PDF" or "EPS" or "PNG");
    }

    private static bool IsWordExportTarget(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) is "PDF" or "DOC" or "DOCX" or "RTF" or "TXT" or "HTML";
    }

    private static bool IsExcelExportTarget(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) is "PDF" or "XLS" or "XLSX" or "CSV";
    }

    private static bool IsPowerPointExportTarget(string targetFormat)
    {
        return NormalizeConvertTarget(targetFormat) is "PDF" or "PPT" or "PPTX" or "PNG" or "JPG" or "JPEG";
    }

    private static string FindExecutableWithToolFallback(string executableName, string toolFolderName, string? childFolder = null)
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", executableName),
            childFolder is null
                ? Path.Combine(AppContext.BaseDirectory, toolFolderName, executableName)
                : Path.Combine(AppContext.BaseDirectory, toolFolderName, childFolder, executableName),
            childFolder is null
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), toolFolderName, executableName)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), toolFolderName, childFolder, executableName),
            childFolder is null
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), toolFolderName, executableName)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), toolFolderName, childFolder, executableName)
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath(executableName);
    }

    private static string FindExecutableInPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return string.Empty;
        }

        foreach (var folder in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Hatalı PATH girdileri yoksayılır.
            }
        }

        return string.Empty;
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static void TryDeleteDirectory(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch
        {
            // Geçici klasör temizlenemezse uygulama çalışmaya devam eder.
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        return builder.Length == 0 ? "converted" : builder.ToString();
    }

    private static string GetUniqueOutputPath(string outputFolder, string baseName, string extension)
    {
        Directory.CreateDirectory(outputFolder);
        var outputPath = Path.Combine(outputFolder, $"{baseName}{extension}");
        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        for (var index = 1; index < 10000; index++)
        {
            outputPath = Path.Combine(outputFolder, $"{baseName}_{index:000}{extension}");
            if (!File.Exists(outputPath))
            {
                return outputPath;
            }
        }

        return Path.Combine(outputFolder, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }

    private static Task<T> RunOnStaThreadAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration cancellationRegistration = default;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
        }

        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completionSource.TrySetResult(action());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completionSource.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
            finally
            {
                cancellationRegistration.Dispose();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return completionSource.Task;
    }

    private bool TryPrintFileSilently(string filePath, PrinterDeviceItem printer, out string errorMessage)
    {
        errorMessage = string.Empty;
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        TryApplySelectedPrintTicketToQueue(printer);

        try
        {
            return extension switch
            {
                ".pdf" => TryPrintPdfSilently(filePath, printer, out errorMessage),
                ".doc" or ".docx" or ".rtf" => TryPrintWordDocument(filePath, printer.QueueValue, out errorMessage),
                ".xls" or ".xlsx" or ".csv" => TryPrintExcelDocument(filePath, printer.QueueValue, out errorMessage),
                ".ppt" or ".pptx" => TryPrintPowerPointDocument(filePath, printer.QueueValue, out errorMessage),
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" => TryPrintImageDirect(filePath, printer.QueueValue, out errorMessage),
                ".txt" or ".log" => TryPrintTextDirect(filePath, printer.QueueValue, out errorMessage),
                _ => TrySendRawFileToPrinter(filePath, printer, out errorMessage)
            };
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool TryPrintPdfSilently(string filePath, PrinterDeviceItem printer, out string errorMessage)
    {
        // PDF yazdırmada ekran açılmasını engellemek için yalnızca sessiz yöntemler kullanılır:
        // 1) Paketle gelen SumatraPDF.exe ile -silent yazdırma,
        // 2) PDF Direct destekleyen yazıcılara RAW 9100 gönderimi.
        // Adobe/Windows PrintTo kullanılmaz; çünkü bazı sistemlerde dosyayı ekranda açar.
        if (TryPrintPdfWithSumatra(filePath, printer, out errorMessage))
        {
            return true;
        }

        var sumatraError = errorMessage;

        if (TrySendRawFileToPrinter(filePath, printer, out var rawError, usePjlSettings: true))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"PDF yazdırılamadı. SumatraPDF: {sumatraError} RAW 9100: {rawError}";
        return false;
    }

    private bool TryPrintPdfWithSumatra(string filePath, PrinterDeviceItem printer, out string errorMessage)
    {
        errorMessage = string.Empty;
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "SumatraPDF.exe"),
            Path.Combine(AppContext.BaseDirectory, "SumatraPDF.exe"),
            Path.Combine(AppContext.BaseDirectory, "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF", "SumatraPDF.exe")
        };

        var sumatraPath = possiblePaths.FirstOrDefault(File.Exists);
        if (sumatraPath is null)
        {
            errorMessage = "SumatraPDF.exe bulunamadı.";
            return false;
        }

        var printerQueueName = ResolveWindowsPrinterName(printer, out var printerResolveWarning);
        if (string.IsNullOrWhiteSpace(printerQueueName))
        {
            errorMessage = "Windows yazıcı kuyruğu belirlenemedi.";
            return false;
        }

        try
        {
            var arguments = $"-silent -print-to \"{printerQueueName}\" -print-settings \"{BuildSumatraPrintSettings()}\" \"{filePath}\"";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = sumatraPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            if (process is null)
            {
                errorMessage = "SumatraPDF işlemi başlatılamadı.";
                return false;
            }

            if (!process.WaitForExit(2500))
            {
                AppLogger.Log("SumatraPDF fast batch mode: print process still running; continuing with next document.");
                return true;
            }

            var standardError = process.StandardError.ReadToEnd().Trim();
            var standardOutput = process.StandardOutput.ReadToEnd().Trim();
            if (process.ExitCode == 0)
            {
                return true;
            }

            var detailParts = new List<string> { $"çıkış kodu {process.ExitCode}" };
            if (!string.IsNullOrWhiteSpace(printerResolveWarning))
            {
                detailParts.Add(printerResolveWarning);
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                detailParts.Add(standardError);
            }
            else if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                detailParts.Add(standardOutput);
            }

            detailParts.Add($"kullanılan yazıcı: {printerQueueName}");
            errorMessage = string.Join("; ", detailParts);
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string ResolveWindowsPrinterName(PrinterDeviceItem printer, out string warning)
    {
        warning = string.Empty;
        var preferredNames = new[] { printer.QueueValue, printer.Name }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (preferredNames.Count == 0)
        {
            return string.Empty;
        }

        try
        {
            using var printServer = new LocalPrintServer();
            var queues = printServer.GetPrintQueues(new[]
                {
                    EnumeratedPrintQueueTypes.Local,
                    EnumeratedPrintQueueTypes.Connections
                })
                .Select(queue => new
                {
                    Queue = queue,
                    DisplayName = string.IsNullOrWhiteSpace(queue.FullName) ? queue.Name : queue.FullName,
                    SearchName = $"{queue.Name} {queue.FullName}"
                })
                .ToList();

            foreach (var preferredName in preferredNames)
            {
                var exact = queues.FirstOrDefault(queue =>
                    string.Equals(queue.Queue.Name, preferredName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(queue.Queue.FullName, preferredName, StringComparison.OrdinalIgnoreCase));

                if (exact is not null)
                {
                    return exact.DisplayName;
                }
            }

            foreach (var preferredName in preferredNames)
            {
                var preferredKey = NormalizePrinterSearchKey(preferredName);
                var normalizedExact = queues.FirstOrDefault(queue =>
                    NormalizePrinterSearchKey(queue.Queue.Name) == preferredKey ||
                    NormalizePrinterSearchKey(queue.Queue.FullName) == preferredKey);

                if (normalizedExact is not null)
                {
                    return normalizedExact.DisplayName;
                }
            }

            foreach (var preferredName in preferredNames)
            {
                var preferredKey = NormalizePrinterSearchKey(preferredName);
                if (string.IsNullOrWhiteSpace(preferredKey))
                {
                    continue;
                }

                var partialMatches = queues
                    .Where(queue => NormalizePrinterSearchKey(queue.SearchName).Contains(preferredKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (partialMatches.Count == 1)
                {
                    return partialMatches[0].DisplayName;
                }
            }

            warning = $"'{preferredNames[0]}' adlı Windows yazıcı kuyruğu bulunamadı. Ayarlar > Yazıcı Kuyruğu alanı Windows'taki yazıcı adıyla birebir aynı olmalı.";
            return preferredNames[0];
        }
        catch (Exception ex)
        {
            warning = $"Windows yazıcı kuyruğu okunamadı: {ex.Message}";
            return preferredNames[0];
        }
    }

    private static string NormalizePrinterSearchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = character switch
            {
                'ı' or 'İ' => 'I',
                'ğ' or 'Ğ' => 'G',
                'ü' or 'Ü' => 'U',
                'ş' or 'Ş' => 'S',
                'ö' or 'Ö' => 'O',
                'ç' or 'Ç' => 'C',
                _ => char.ToUpperInvariant(character)
            };

            builder.Append(mapped);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private bool TryPrintPdfWithAdobe(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe", "Acrobat DC", "Acrobat", "Acrobat.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Adobe", "Acrobat DC", "Acrobat", "Acrobat.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe", "Acrobat", "Acrobat.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Adobe", "Acrobat Reader", "Reader", "AcroRd32.exe")
        };

        var adobePath = possiblePaths.FirstOrDefault(File.Exists);
        if (adobePath is null)
        {
            errorMessage = "Adobe Reader/Acrobat bulunamadı.";
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = adobePath,
                Arguments = $"/t \"{filePath}\" \"{printerQueueName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                errorMessage = "Adobe yazdırma işlemi başlatılamadı.";
                return false;
            }

            // Acrobat/Reader bazı sürümlerde yazdırmayı kuyruğa verdikten sonra açık kalır.
            // Bu durumda uygulamayı kilitlememek için işlemi başarılı kabul ediyoruz.
            if (!process.WaitForExit(20000))
            {
                return true;
            }

            if (process.ExitCode == 0)
            {
                return true;
            }

            errorMessage = $"Adobe çıkış kodu: {process.ExitCode}.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool TryPrintPdfWithShellPrintTo(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                Verb = "printto",
                Arguments = $"\"{printerQueueName}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                errorMessage = "Windows PrintTo işlemi başlatılamadı.";
                return false;
            }

            // Bazı PDF uygulamaları yazdırmayı kuyruğa verip açık kalır.
            // Süre dolduysa işlemi başarılı kabul ediyoruz; uygulamayı kapatıp yazdırmayı iptal etmiyoruz.
            if (!process.WaitForExit(15000))
            {
                return true;
            }

            if (process.ExitCode == 0)
            {
                return true;
            }

            errorMessage = $"Windows PrintTo çıkış kodu: {process.ExitCode}.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private string BuildSumatraPrintSettings()
    {
        var settings = new List<string>
        {
            $"{GetCopyCount()}x",
            $"paper={GetPaperCode()}"
        };

        settings.Add(IsDuplexPrintRequested() ? "duplex" : "simplex");
        settings.Add(SelectedOrientation.Contains("Yatay", StringComparison.OrdinalIgnoreCase) ? "landscape" : "portrait");
        settings.Add(SelectedPrintColor.Contains("Renkli", StringComparison.OrdinalIgnoreCase) ? "color" : "monochrome");

        if (SelectedScaleMode.Contains("Sığdır", StringComparison.OrdinalIgnoreCase))
        {
            settings.Add("fit");
        }
        else
        {
            settings.Add("noscale");
        }

        if (!string.IsNullOrWhiteSpace(PrintPageRange))
        {
            settings.Add(PrintPageRange.Trim());
        }

        return string.Join(',', settings);
    }

    private bool TrySendRawFileToPrinter(string filePath, PrinterDeviceItem printer, out string errorMessage, bool usePjlSettings = false)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(printer.IpAddress))
        {
            errorMessage = "Yazıcı IP adresi tanımlı değil.";
            return false;
        }

        try
        {
            var copies = GetCopyCount();
            for (var i = 0; i < copies; i++)
            {
                using var client = new TcpClient();
                if (!TryConnectTcpClient(client, printer.IpAddress, 9100, TimeSpan.FromSeconds(5), out var connectError))
                {
                    errorMessage = string.IsNullOrWhiteSpace(connectError) ? "Yazıcıya 9100 portundan bağlanılamadı." : connectError;
                    return false;
                }

                using var networkStream = client.GetStream();

                if (usePjlSettings)
                {
                    using var writer = new StreamWriter(networkStream, System.Text.Encoding.ASCII, 1024, leaveOpen: true)
                    {
                        NewLine = "\r\n"
                    };

                    writer.Write("\u001B%-12345X");
                    writer.WriteLine("@PJL JOB NAME=\"ToolBridge\"");
                    writer.WriteLine($"@PJL SET PAPER={GetPaperCode()}");
                    writer.WriteLine($"@PJL SET MEDIASIZE={GetPaperCode()}");
                    writer.WriteLine($"@PJL SET ORIENTATION={(SelectedOrientation.Contains("Yatay", StringComparison.OrdinalIgnoreCase) ? "LANDSCAPE" : "PORTRAIT")}");
                    writer.WriteLine($"@PJL SET DUPLEX={(IsDuplexPrintRequested() ? "ON" : "OFF")}");
                    writer.WriteLine($"@PJL SET SIDES={GetPjlSidesMode()}");
                    writer.WriteLine("@PJL ENTER LANGUAGE=PDF");
                    writer.Flush();
                }

                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(networkStream);

                if (usePjlSettings)
                {
                    using var writer = new StreamWriter(networkStream, System.Text.Encoding.ASCII, 1024, leaveOpen: true)
                    {
                        NewLine = "\r\n"
                    };

                    writer.WriteLine();
                    writer.WriteLine("\u001B%-12345X");
                    writer.Flush();
                }

                networkStream.Flush();
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"RAW 9100 gönderimi başarısız: {ex.Message}";
            return false;
        }
    }

    private static bool TryConnectTcpClient(TcpClient client, string host, int port, TimeSpan timeout, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            client.Client.Blocking = false;

            try
            {
                client.Client.Connect(host, port);
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress or SocketError.AlreadyInProgress)
            {
                // Non-blocking bağlantı bekleniyor. Poll ile zaman aşımı kontrollü izlenir.
            }

            var timeoutMicroseconds = Math.Max(1, (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds * 1000));
            if (!client.Client.Poll(timeoutMicroseconds, SelectMode.SelectWrite) || !client.Client.Connected)
            {
                errorMessage = $"Yazıcıya {timeout.TotalSeconds:0} saniye içinde bağlanılamadı.";
                return false;
            }

            var socketError = Convert.ToInt32(client.Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error), CultureInfo.InvariantCulture);
            if (socketError != 0)
            {
                errorMessage = $"Yazıcı bağlantısı socket hata kodu döndürdü: {socketError}.";
                return false;
            }

            client.Client.Blocking = true;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Yazıcı bağlantısı başarısız: {ex.Message}";
            return false;
        }
        finally
        {
            try { client.Client.Blocking = true; } catch { }
        }
    }

    private bool TryPrintImageDirect(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var printDialog = CreatePrintDialog(printerQueueName);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                Width = printDialog.PrintableAreaWidth,
                Height = printDialog.PrintableAreaHeight
            };

            var border = new Border
            {
                Background = Brushes.White,
                Width = printDialog.PrintableAreaWidth,
                Height = printDialog.PrintableAreaHeight,
                Child = image
            };

            border.Measure(new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
            border.Arrange(new Rect(new Point(0, 0), new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight)));

            for (var i = 0; i < GetCopyCount(); i++)
            {
                printDialog.PrintVisual(border, Path.GetFileName(filePath));
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool TryPrintTextDirect(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var printDialog = CreatePrintDialog(printerQueueName);
            var text = File.ReadAllText(filePath);
            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Width = printDialog.PrintableAreaWidth - 40
            };

            var border = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(20),
                Width = printDialog.PrintableAreaWidth,
                Height = printDialog.PrintableAreaHeight,
                Child = textBlock
            };

            border.Measure(new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
            border.Arrange(new Rect(new Point(0, 0), new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight)));

            for (var i = 0; i < GetCopyCount(); i++)
            {
                printDialog.PrintVisual(border, Path.GetFileName(filePath));
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private PrintDialog CreatePrintDialog(string printerQueueName)
    {
        var printDialog = new PrintDialog();
        var queue = new LocalPrintServer().GetPrintQueue(printerQueueName);
        printDialog.PrintQueue = queue;

        var baseTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();
        var requestedTicket = baseTicket.Clone();
        ApplyPrintTicketSettings(requestedTicket);

        try
        {
            var validation = queue.MergeAndValidatePrintTicket(baseTicket, requestedTicket);
            printDialog.PrintTicket = validation.ValidatedPrintTicket;
        }
        catch
        {
            printDialog.PrintTicket = requestedTicket;
        }

        return printDialog;
    }

    private void ApplyPrintTicketSettings(PrintTicket? printTicket)
    {
        if (printTicket is null)
        {
            return;
        }

        printTicket.CopyCount = GetCopyCount();
        printTicket.Duplexing = IsDuplexPrintRequested()
            ? Duplexing.TwoSidedLongEdge
            : Duplexing.OneSided;
        printTicket.OutputColor = SelectedPrintColor.Contains("Renkli", StringComparison.OrdinalIgnoreCase)
            ? OutputColor.Color
            : OutputColor.Grayscale;
        printTicket.PageMediaSize = GetPageMediaSize();
        printTicket.PageOrientation = SelectedOrientation.Contains("Yatay", StringComparison.OrdinalIgnoreCase)
            ? PageOrientation.Landscape
            : PageOrientation.Portrait;
    }

    private PageMediaSize GetPageMediaSize()
    {
        return GetPaperCode() switch
        {
            "A0" => new PageMediaSize(PageMediaSizeName.ISOA0),
            "A1" => new PageMediaSize(PageMediaSizeName.ISOA1),
            "A2" => new PageMediaSize(PageMediaSizeName.ISOA2),
            "A3" => new PageMediaSize(PageMediaSizeName.ISOA3),
            "A4" => new PageMediaSize(PageMediaSizeName.ISOA4),
            "A5" => new PageMediaSize(PageMediaSizeName.ISOA5),
            "A6" => new PageMediaSize(PageMediaSizeName.ISOA6),
            "B1" => new PageMediaSize(2751.50, 3892.91),
            "B2" => new PageMediaSize(1946.46, 2751.50),
            "B3" => new PageMediaSize(1375.75, 1946.46),
            "B4" => new PageMediaSize(972.28, 1375.75),
            _ => new PageMediaSize(PageMediaSizeName.ISOA4)
        };
    }

    private string GetPaperCode()
    {
        var selected = SelectedPaperSize.Trim();
        if (selected.StartsWith("A0", StringComparison.OrdinalIgnoreCase)) return "A0";
        if (selected.StartsWith("A1", StringComparison.OrdinalIgnoreCase)) return "A1";
        if (selected.StartsWith("A2", StringComparison.OrdinalIgnoreCase)) return "A2";
        if (selected.StartsWith("A3", StringComparison.OrdinalIgnoreCase)) return "A3";
        if (selected.StartsWith("A4", StringComparison.OrdinalIgnoreCase)) return "A4";
        if (selected.StartsWith("A5", StringComparison.OrdinalIgnoreCase)) return "A5";
        if (selected.StartsWith("A6", StringComparison.OrdinalIgnoreCase)) return "A6";
        if (selected.StartsWith("B1", StringComparison.OrdinalIgnoreCase)) return "B1";
        if (selected.StartsWith("B2", StringComparison.OrdinalIgnoreCase)) return "B2";
        if (selected.StartsWith("B3", StringComparison.OrdinalIgnoreCase)) return "B3";
        if (selected.StartsWith("B4", StringComparison.OrdinalIgnoreCase)) return "B4";
        return "A4";
    }

    private bool TryPrintWordDocument(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;
        dynamic? word = null;
        dynamic? document = null;

        try
        {
            var wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType is null)
            {
                errorMessage = "Microsoft Word yüklü değil.";
                return false;
            }

            word = Activator.CreateInstance(wordType);
            if (word is null)
            {
                errorMessage = "Microsoft Word başlatılamadı.";
                return false;
            }

            word.Visible = false;
            word.DisplayAlerts = 0;
            var oldPrinter = word.ActivePrinter;
            word.ActivePrinter = printerQueueName;
            document = word.Documents.Open(filePath, false, true, false);
            ApplyWordPrintSettings(document);

            var pageRange = PrintPageRange.Trim();
            if (string.IsNullOrWhiteSpace(pageRange))
            {
                document.PrintOut(Background: false, Copies: GetCopyCount().ToString());
            }
            else
            {
                document.PrintOut(Background: false, Copies: GetCopyCount().ToString(), Pages: pageRange);
            }

            document.Close(false);
            word.ActivePrinter = oldPrinter;
            word.Quit(false);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            try { document?.Close(false); } catch { }
            try { word?.Quit(false); } catch { }
            return false;
        }
    }

    private bool TryPrintExcelDocument(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;
        dynamic? excel = null;
        dynamic? workbook = null;

        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType is null)
            {
                errorMessage = "Microsoft Excel yüklü değil.";
                return false;
            }

            excel = Activator.CreateInstance(excelType);
            if (excel is null)
            {
                errorMessage = "Microsoft Excel başlatılamadı.";
                return false;
            }

            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbook = excel.Workbooks.Open(filePath, ReadOnly: true);
            ApplyExcelPrintSettings(workbook);

            var (fromPage, toPage) = GetPageRangeBounds();
            if (fromPage.HasValue && toPage.HasValue)
            {
                workbook.PrintOut(From: fromPage.Value, To: toPage.Value, Copies: GetCopyCount(), ActivePrinter: printerQueueName);
            }
            else
            {
                workbook.PrintOut(Copies: GetCopyCount(), ActivePrinter: printerQueueName);
            }

            workbook.Close(false);
            excel.Quit();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            try { workbook?.Close(false); } catch { }
            try { excel?.Quit(); } catch { }
            return false;
        }
    }

    private bool TryPrintPowerPointDocument(string filePath, string printerQueueName, out string errorMessage)
    {
        errorMessage = string.Empty;
        dynamic? powerPoint = null;
        dynamic? presentation = null;

        try
        {
            var powerPointType = Type.GetTypeFromProgID("PowerPoint.Application");
            if (powerPointType is null)
            {
                errorMessage = "Microsoft PowerPoint yüklü değil.";
                return false;
            }

            powerPoint = Activator.CreateInstance(powerPointType);
            if (powerPoint is null)
            {
                errorMessage = "Microsoft PowerPoint başlatılamadı.";
                return false;
            }

            presentation = powerPoint.Presentations.Open(filePath, true, false, false);
            presentation.PrintOptions.ActivePrinter = printerQueueName;
            var (fromPage, toPage) = GetPageRangeBounds();
            presentation.PrintOut(fromPage ?? 1, toPage ?? int.MaxValue, string.Empty, GetCopyCount(), false);
            presentation.Close();
            powerPoint.Quit();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            try { presentation?.Close(); } catch { }
            try { powerPoint?.Quit(); } catch { }
            return false;
        }
    }


    private void ApplyWordPrintSettings(dynamic document)
    {
        try
        {
            dynamic pageSetup = document.PageSetup;
            pageSetup.Orientation = SelectedOrientation.Contains("Yatay", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            pageSetup.PaperSize = GetWordPaperSize();

            var margins = GetMarginInPoints();
            pageSetup.TopMargin = margins.top;
            pageSetup.BottomMargin = margins.bottom;
            pageSetup.LeftMargin = margins.left;
            pageSetup.RightMargin = margins.right;
        }
        catch
        {
            // Office sürümü veya yazıcı sürücüsü ilgili ayarı desteklemiyorsa kalan ayarlarla devam edilir.
        }
    }

    private int GetWordPaperSize()
    {
        if (SelectedPaperSize.StartsWith("A3", StringComparison.OrdinalIgnoreCase)) return 6;
        if (SelectedPaperSize.StartsWith("A4", StringComparison.OrdinalIgnoreCase)) return 7;
        if (SelectedPaperSize.StartsWith("A5", StringComparison.OrdinalIgnoreCase)) return 9;
        if (SelectedPaperSize.StartsWith("B4", StringComparison.OrdinalIgnoreCase)) return 10;
        if (SelectedPaperSize.StartsWith("B5", StringComparison.OrdinalIgnoreCase)) return 11;
        return 7;
    }

    private void ApplyExcelPrintSettings(dynamic workbook)
    {
        try
        {
            foreach (dynamic worksheet in workbook.Worksheets)
            {
                dynamic pageSetup = worksheet.PageSetup;
                pageSetup.Orientation = SelectedOrientation.Contains("Yatay", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
                pageSetup.PaperSize = GetExcelPaperSize();
                pageSetup.BlackAndWhite = !SelectedPrintColor.Contains("Renkli", StringComparison.OrdinalIgnoreCase);

                var margins = GetMarginInInches();
                pageSetup.TopMargin = margins.top;
                pageSetup.BottomMargin = margins.bottom;
                pageSetup.LeftMargin = margins.left;
                pageSetup.RightMargin = margins.right;

                if (SelectedScaleMode.Contains("Sığdır", StringComparison.OrdinalIgnoreCase))
                {
                    pageSetup.Zoom = false;
                    pageSetup.FitToPagesWide = SelectedScaleMode.Contains("Satır", StringComparison.OrdinalIgnoreCase) ? (object)false : 1;
                    pageSetup.FitToPagesTall = SelectedScaleMode.Contains("Sütun", StringComparison.OrdinalIgnoreCase) ? (object)false : 1;
                }
                else
                {
                    pageSetup.Zoom = 100;
                }
            }
        }
        catch
        {
            // Excel yazdırma ayarlarından biri uygulanamazsa yazdırma işlemi engellenmez.
        }
    }

    private int GetExcelPaperSize()
    {
        if (SelectedPaperSize.StartsWith("A3", StringComparison.OrdinalIgnoreCase)) return 8;
        if (SelectedPaperSize.StartsWith("A4", StringComparison.OrdinalIgnoreCase)) return 9;
        if (SelectedPaperSize.StartsWith("A5", StringComparison.OrdinalIgnoreCase)) return 11;
        if (SelectedPaperSize.StartsWith("B4", StringComparison.OrdinalIgnoreCase)) return 12;
        if (SelectedPaperSize.StartsWith("B5", StringComparison.OrdinalIgnoreCase)) return 13;
        return 9;
    }

    private (int? from, int? to) GetPageRangeBounds()
    {
        var text = PrintPageRange.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        var separators = new[] { '-', '–', '—' };
        var parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out var page))
        {
            return (Math.Max(1, page), Math.Max(1, page));
        }

        if (parts.Length >= 2 && int.TryParse(parts[0], out var from) && int.TryParse(parts[1], out var to))
        {
            from = Math.Max(1, from);
            to = Math.Max(from, to);
            return (from, to);
        }

        return (null, null);
    }

    private (double top, double bottom, double left, double right) GetMarginInPoints()
    {
        var cm = GetMarginInCentimeters();
        const double pointsPerCm = 28.3464567;
        return (cm.top * pointsPerCm, cm.bottom * pointsPerCm, cm.left * pointsPerCm, cm.right * pointsPerCm);
    }

    private (double top, double bottom, double left, double right) GetMarginInInches()
    {
        var cm = GetMarginInCentimeters();
        const double inchesPerCm = 0.393700787;
        return (cm.top * inchesPerCm, cm.bottom * inchesPerCm, cm.left * inchesPerCm, cm.right * inchesPerCm);
    }

    private (double top, double bottom, double left, double right) GetMarginInCentimeters()
    {
        if (SelectedMarginProfile.Contains("Geniş", StringComparison.OrdinalIgnoreCase))
        {
            return (2.54, 2.54, 2.54, 2.54);
        }

        if (SelectedMarginProfile.Contains("Dar", StringComparison.OrdinalIgnoreCase))
        {
            return (1.91, 1.91, 0.64, 0.64);
        }

        if (SelectedMarginProfile.Contains("Son Özel", StringComparison.OrdinalIgnoreCase))
        {
            return (0.4, 0.6, 0.6, 0.6);
        }

        return (1.91, 1.91, 1.78, 1.78);
    }

    private int GetCopyCount()
    {
        return int.TryParse(PrintCopyCount, out var copyCount)
            ? Math.Clamp(copyCount, 1, 99)
            : 1;
    }

    private static void ShowPrintNotification(string title, string message, MessageBoxImage icon)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }

    private void AddPrintHistory(UploadFileItem file, string printerQueueName, string status)
    {
        var pageText = string.IsNullOrWhiteSpace(PrintPageRange) ? "Tümü" : PrintPageRange.Trim();
        var colorText = NormalizePrintColor(SelectedPrintColor);
        var isError = status == "Hata";

        PrintHistory.Insert(0, new PrintHistoryItem
        {
            DateText = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            FileName = file.FileName,
            PrinterName = printerQueueName,
            PageText = pageText,
            PaperText = SelectedPaperSize,
            ColorText = colorText,
            ColorBrush = GetHistoryColorBrush(colorText),
            ColorBorderBrush = GetHistoryColorBorderBrush(colorText),
            ColorBackgroundBrush = GetHistoryColorBackgroundBrush(colorText),
            StatusText = status,
            StatusBrush = isError ? Solid("#DC2626") : Solid("#16A34A"),
            StatusBorderBrush = isError ? Solid("#FCA5A5") : Solid("#86EFAC"),
            StatusBackgroundBrush = isError ? Solid("#FEF2F2") : Solid("#F0FDF4")
        });
    }

    private static string NormalizePrintColor(string selectedColor)
    {
        return selectedColor.Contains("Renkli", StringComparison.OrdinalIgnoreCase)
            ? "Renkli"
            : "Siyah Beyaz";
    }

    private static Brush GetHistoryColorBrush(string colorText)
    {
        return colorText == "Renkli" ? Solid("#F59E0B") : Solid("#64748B");
    }

    private Brush GetHistoryColorBorderBrush(string colorText)
    {
        if (IsDarkModeEnabled)
        {
            return colorText == "Renkli" ? Solid("#92400E") : Solid("#475569");
        }

        return colorText == "Renkli" ? Solid("#F59E0B") : Solid("#CBD5E1");
    }

    private Brush GetHistoryColorBackgroundBrush(string colorText)
    {
        if (IsDarkModeEnabled)
        {
            return colorText == "Renkli" ? Solid("#261704") : Solid("#1E293B");
        }

        return colorText == "Renkli" ? Solid("#FFFBEB") : Solid("#F8FAFC");
    }

    private void ClearPrintHistory()
    {
        PrintHistory.Clear();
        SetUploadStatus("Yazdırma geçmişi temizlendi.", false);
    }

    private void SetUploadStatus(string message, bool isError)
    {
        UploadStatusMessage = message;
        UploadStatusBrush = isError ? Solid("#DC2626") : Solid("#777783");
    }

    private void SetConvertStatus(string message, bool isError)
    {
        ConvertStatusMessage = message;
        ConvertStatusBrush = isError ? Solid("#DC2626") : Solid("#777783");
    }

    private void SetPdfMergeStatus(string message, bool isError)
    {
        PdfMergeStatusMessage = message;
        PdfMergeStatusBrush = isError ? Solid("#DC2626") : Solid("#777783");
    }

    private void NavigateToPage(string pageTitle)
    {
        var navItem = PrimaryNavigation
            .Concat(LibraryNavigation)
            .FirstOrDefault(item => string.Equals(item.Title, pageTitle, StringComparison.OrdinalIgnoreCase));

        if (navItem is not null)
        {
            SelectNavigation(navItem);
            return;
        }

        SelectedPage = pageTitle;
    }

    private void SelectNavigation(object? parameter)
    {
        if (parameter is not NavItem selected)
        {
            return;
        }

        foreach (var item in PrimaryNavigation.Concat(LibraryNavigation))
        {
            item.IsActive = ReferenceEquals(item, selected);
        }

        SelectedPage = selected.Title;
        if (string.Equals(SelectedPage, PageTransfer, StringComparison.OrdinalIgnoreCase))
        {
            MarkIncomingTransferNotificationsSeen();
        }
        ApplyActiveAccent(selected);
    }

    private void ApplyActiveAccent(NavItem selected)
    {
        var palette = ResolveAccentPalette(selected.Title, IsDarkModeEnabled);

        ActiveAccentBrush = Solid(palette.Primary);
        ActiveAccentSoftBrush = Solid(palette.Soft);
        ActiveAccentBorderBrush = Solid(palette.Border);
        ActiveAccentRingBrush = Solid(palette.Ring);

        if (Application.Current is null)
        {
            return;
        }

        SetResourceBrush("AccentBrush", palette.Primary);
        SetResourceBrush("AccentSoftBrush", palette.Soft);
        SetResourceBrush("ButtonPrimaryBrush", palette.Primary);
        SetResourceBrush("ButtonPrimaryHoverBrush", palette.Hover);
        SetResourceBrush("ButtonOutlineHoverBrush", palette.Soft);
        SetResourceBrush("ButtonRingBrush", palette.Ring);
    }

    private static (string Primary, string Hover, string Soft, string Border, string Ring) ResolveAccentPalette(string pageTitle, bool isDarkMode)
    {
        return pageTitle switch
        {
            "Yazdırma" => isDarkMode
                ? ("#F59E0B", "#D97706", "#261704", "#92400E", "#80F59E0B")
                : ("#F59E0B", "#D97706", "#FFFBEB", "#FCD34D", "#80F59E0B"),
            "Transfer" => isDarkMode
                ? ("#3B82F6", "#2563EB", "#0B1B33", "#1D4ED8", "#803B82F6")
                : ("#3B82F6", "#2563EB", "#EFF6FF", "#93C5FD", "#803B82F6"),
            "Convert" => isDarkMode
                ? ("#EF4444", "#DC2626", "#2A1116", "#7F1D1D", "#80EF4444")
                : ("#EF4444", "#DC2626", "#FEF2F2", "#FCA5A5", "#80EF4444"),
            "Ayarlar" => isDarkMode
                ? ("#22C55E", "#16A34A", "#0B2415", "#166534", "#8022C55E")
                : ("#22C55E", "#16A34A", "#F0FDF4", "#86EFAC", "#8022C55E"),
            _ => isDarkMode
                ? ("#FA233B", "#E01E35", "#2A1116", "#7F1D1D", "#80FA233B")
                : ("#FA233B", "#E01E35", "#FFE8EC", "#FF9AA8", "#80FA233B")
        };
    }

    private void SelectRegisteredPrinter(object? parameter)
    {
        if (parameter is not PrinterDeviceItem printer)
        {
            return;
        }

        foreach (var item in RegisteredPrinters)
        {
            item.IsSelected = ReferenceEquals(item, printer);
        }

        SelectedRegisteredPrinter = printer;
        SetSettingsStatus($"{printer.Name} seçildi.", false);
    }

    private void SelectPrintPrinter(object? parameter)
    {
        if (parameter is not PrinterDeviceItem printer)
        {
            return;
        }

        foreach (var item in RegisteredPrinters)
        {
            item.IsPrintSelected = ReferenceEquals(item, printer);
        }

        SelectedPrintPrinter = printer;
    }

    private void SetDefaultPrinter()
    {
        if (SelectedRegisteredPrinter is null)
        {
            SetSettingsStatus("Varsayılan yapmak için önce bir yazıcı seçin.", true);
            return;
        }

        foreach (var printer in RegisteredPrinters)
        {
            printer.IsDefault = ReferenceEquals(printer, SelectedRegisteredPrinter);
        }

        SaveRegisteredPrinters();
        UpdatePrinterDeviceCards();
        OnPropertyChanged(nameof(DefaultPrinterText));
        OnPropertyChanged(nameof(SelectedPrinterText));
        SetSettingsStatus($"{SelectedRegisteredPrinter.Name} varsayılan yazıcı olarak ayarlandı.", false);
    }


    private void RemoveRegisteredPrinter()
    {
        if (SelectedRegisteredPrinter is null)
        {
            SetSettingsStatus("Kaldırmak için önce bir yazıcı seçin.", true);
            return;
        }

        var removedPrinterName = SelectedRegisteredPrinter.Name;
        var removedWasDefault = SelectedRegisteredPrinter.IsDefault;
        var removedPrinter = SelectedRegisteredPrinter;
        RegisteredPrinters.Remove(SelectedRegisteredPrinter);

        foreach (var printer in RegisteredPrinters)
        {
            printer.IsSelected = false;
        }

        var nextPrinter = RegisteredPrinters.FirstOrDefault();
        if (nextPrinter is not null)
        {
            if (removedWasDefault || !RegisteredPrinters.Any(printer => printer.IsDefault))
            {
                foreach (var printer in RegisteredPrinters)
                {
                    printer.IsDefault = ReferenceEquals(printer, nextPrinter);
                }
            }

            nextPrinter.IsSelected = true;
            SelectedRegisteredPrinter = nextPrinter;
            if (ReferenceEquals(SelectedPrintPrinter, removedPrinter) || SelectedPrintPrinter is null)
            {
                SelectPrintPrinter(nextPrinter);
            }
        }
        else
        {
            SelectedRegisteredPrinter = null;
            SelectedPrintPrinter = null;
        }

        RenumberRegisteredPrinters();
        SaveRegisteredPrinters();
        UpdatePrinterDeviceCards();
        OnPropertyChanged(nameof(DefaultPrinterText));
        OnPropertyChanged(nameof(SelectedPrinterText));
        SetSettingsStatus($"{removedPrinterName} cihazı kaldırıldı.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanSaveManualPrinter()
    {
        return !string.IsNullOrWhiteSpace(ManualPrinterName) &&
               !string.IsNullOrWhiteSpace(ManualPrinterIp);
    }

    private void SaveManualPrinter()
    {
        var name = ManualPrinterName.Trim();
        var ipAddress = ManualPrinterIp.Trim();
        var queueName = ManualPrinterQueue.Trim();

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            SetSettingsStatus("Geçerli bir IP adresi girin. Örnek: 192.168.2.20", true);
            return;
        }

        if (RegisteredPrinters.Any(printer => string.Equals(printer.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            SetSettingsStatus($"{name} isimli yazıcı zaten kayıtlı.", true);
            return;
        }

        if (RegisteredPrinters.Any(printer => string.Equals(printer.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)))
        {
            SetSettingsStatus($"{ipAddress} IP adresi zaten kayıtlı.", true);
            return;
        }

        var printerItem = new PrinterDeviceItem
        {
            Name = name,
            IpAddress = ipAddress,
            QueueName = queueName,
            IsDefault = RegisteredPrinters.Count == 0
        };

        RegisteredPrinters.Add(printerItem);
        RenumberRegisteredPrinters();
        SelectRegisteredPrinter(printerItem);
        if (SelectedPrintPrinter is null || RegisteredPrinters.Count == 1)
        {
            SelectPrintPrinter(printerItem);
        }
        SaveRegisteredPrinters();
        UpdatePrinterDeviceCards();

        ManualPrinterName = string.Empty;
        ManualPrinterIp = string.Empty;
        ManualPrinterQueue = string.Empty;

        OnPropertyChanged(nameof(DefaultPrinterText));
        SetSettingsStatus($"{name} yazıcısı kaydedildi.", false);
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetSettingsStatus(string message, bool isError)
    {
        SettingsStatusMessage = message;
        SettingsStatusBrush = isError ? Solid("#DC2626") : Solid("#777783");
    }

    private void LoadPrintSettings()
    {
        _isLoadingPrintSettings = true;

        try
        {
            if (!File.Exists(PrintSettingsStorePath))
            {
                return;
            }

            var json = File.ReadAllText(PrintSettingsStorePath);
            var settings = JsonSerializer.Deserialize<SavedPrintSettings>(json);
            if (settings is null)
            {
                return;
            }

            _selectedPrintColor = string.IsNullOrWhiteSpace(settings.Color) ? _selectedPrintColor : settings.Color;
            _selectedPrintSide = string.IsNullOrWhiteSpace(settings.Side) ? _selectedPrintSide : settings.Side;
            _selectedPaperSize = string.IsNullOrWhiteSpace(settings.PaperSize) ? _selectedPaperSize : settings.PaperSize;
            _selectedMarginProfile = string.IsNullOrWhiteSpace(settings.MarginProfile) ? _selectedMarginProfile : settings.MarginProfile;
            _selectedScaleMode = string.IsNullOrWhiteSpace(settings.ScaleMode) ? _selectedScaleMode : settings.ScaleMode;
            _selectedOrientation = string.IsNullOrWhiteSpace(settings.Orientation) ? _selectedOrientation : settings.Orientation;
            _printCopyCount = string.IsNullOrWhiteSpace(settings.CopyCount) ? _printCopyCount : settings.CopyCount;
            _printPageRange = settings.PageRange ?? string.Empty;
            _isDarkModeEnabled = settings.DarkMode;
            _isTransferReceiveEnabled = settings.TransferReceiveEnabled;
            _transferDownloadFolder = settings.TransferDownloadFolder?.Trim() ?? string.Empty;
        }
        catch
        {
            // Ayar dosyası okunamazsa varsayılan değerlerle devam edilir.
        }
        finally
        {
            _isLoadingPrintSettings = false;
            ApplyVisualTheme();
        }
    }

    private void SavePrintSettings()
    {
        if (_isLoadingPrintSettings)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(PrintSettingsStorePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new SavedPrintSettings
            {
                Color = SelectedPrintColor,
                Side = SelectedPrintSide,
                PaperSize = SelectedPaperSize,
                MarginProfile = SelectedMarginProfile,
                ScaleMode = SelectedScaleMode,
                Orientation = SelectedOrientation,
                CopyCount = PrintCopyCount,
                PageRange = PrintPageRange,
                DarkMode = IsDarkModeEnabled,
                TransferReceiveEnabled = IsTransferReceiveEnabled,
                TransferDownloadFolder = TransferDownloadFolder
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PrintSettingsStorePath, json);
        }
        catch
        {
            // Yazdırma ayarları kaydedilemezse uygulama çalışmaya devam eder.
        }
    }

    private void LoadConvertSettings()
    {
        _isLoadingConvertSettings = true;

        try
        {
            if (!File.Exists(ConvertSettingsStorePath))
            {
                return;
            }

            var json = File.ReadAllText(ConvertSettingsStorePath);
            var settings = JsonSerializer.Deserialize<SavedConvertSettings>(json);
            if (!string.IsNullOrWhiteSpace(settings?.OutputFolder))
            {
                _convertOutputFolder = settings.OutputFolder.Trim();
                OnPropertyChanged(nameof(ConvertOutputFolder));
            }
        }
        catch
        {
            // Convert ayarları okunamazsa personel yeniden kayıt klasörü seçer.
        }
        finally
        {
            _isLoadingConvertSettings = false;
        }
    }

    private void SaveConvertSettings()
    {
        if (_isLoadingConvertSettings)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(ConvertSettingsStorePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new SavedConvertSettings
            {
                OutputFolder = ConvertOutputFolder
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConvertSettingsStorePath, json);
        }
        catch
        {
            // Convert ayarları kaydedilemezse uygulama çalışmaya devam eder; personel klasörü yeniden seçebilir.
        }
    }

    private void LoadRegisteredPrinters()
    {
        var savedPrinters = ReadSavedPrinters();

        if (savedPrinters.Count == 0)
        {
            savedPrinters.AddRange(new[]
            {
                new SavedPrinterDevice { Name = "RENKSİZ", IpAddress = "192.168.2.20", QueueName = "RENKSİZ", IsDefault = true },
                new SavedPrinterDevice { Name = "RENKLİ", IpAddress = "192.168.2.21", QueueName = "RENKLİ", IsDefault = false }
            });
        }

        foreach (var savedPrinter in savedPrinters)
        {
            RegisteredPrinters.Add(new PrinterDeviceItem
            {
                Name = savedPrinter.Name,
                IpAddress = savedPrinter.IpAddress,
                QueueName = savedPrinter.QueueName,
                IsDefault = savedPrinter.IsDefault
            });
        }

        RenumberRegisteredPrinters();

        var selected = RegisteredPrinters.FirstOrDefault(printer => printer.IsDefault) ?? RegisteredPrinters.FirstOrDefault();
        if (selected is not null)
        {
            selected.IsSelected = true;
            selected.IsPrintSelected = true;
            SelectedRegisteredPrinter = selected;
            SelectedPrintPrinter = selected;
        }

        if (!RegisteredPrinters.Any(printer => printer.IsDefault) && RegisteredPrinters.FirstOrDefault() is { } firstPrinter)
        {
            firstPrinter.IsDefault = true;
            if (SelectedPrintPrinter is null)
            {
                firstPrinter.IsPrintSelected = true;
                SelectedPrintPrinter = firstPrinter;
            }
        }

        UpdatePrinterDeviceCards();
    }

    private void RenumberRegisteredPrinters()
    {
        for (var index = 0; index < RegisteredPrinters.Count; index++)
        {
            RegisteredPrinters[index].Number = index + 1;
        }
    }

    private static List<SavedPrinterDevice> ReadSavedPrinters()
    {
        try
        {
            if (!File.Exists(PrinterStorePath))
            {
                return new List<SavedPrinterDevice>();
            }

            var json = File.ReadAllText(PrinterStorePath);
            return JsonSerializer.Deserialize<List<SavedPrinterDevice>>(json) ?? new List<SavedPrinterDevice>();
        }
        catch
        {
            return new List<SavedPrinterDevice>();
        }
    }

    private void SaveRegisteredPrinters()
    {
        try
        {
            var directory = Path.GetDirectoryName(PrinterStorePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var savedPrinters = RegisteredPrinters.Select(printer => new SavedPrinterDevice
            {
                Name = printer.Name,
                IpAddress = printer.IpAddress,
                QueueName = printer.QueueName,
                IsDefault = printer.IsDefault
            }).ToList();

            var json = JsonSerializer.Serialize(savedPrinters, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PrinterStorePath, json);
        }
        catch
        {
            SetSettingsStatus("Yazıcı kayıtları dosyaya yazılamadı.", true);
        }
    }

    private void UpdatePrinterDeviceCards()
    {
        PrinterDevices.Clear();

        var colorPairs = new[]
        {
            ("#FF7A00", "#FACC15"),
            ("#06B6D4", "#2563EB"),
            ("#7C3AED", "#EC4899"),
            ("#22C55E", "#14B8A6"),
            ("#64748B", "#111827")
        };

        for (var index = 0; index < RegisteredPrinters.Count; index++)
        {
            var printer = RegisteredPrinters[index];
            var colors = colorPairs[index % colorPairs.Length];
            printer.CoverBrush = Gradient(colors.Item1, colors.Item2);
            printer.IsPrintSelected = ReferenceEquals(printer, SelectedPrintPrinter);
            PrinterDevices.Add(new ToolCard
            {
                Kicker = printer.IsDefault ? "Varsayılan" : "Yazıcı",
                Title = printer.Name,
                Subtitle = printer.IpAddress,
                IsLarge = true,
                CoverBrush = Gradient(colors.Item1, colors.Item2)
            });
        }

        OnPropertyChanged(nameof(SelectedPrintPrinterCoverBrush));
    }

    private void ApplyVisualTheme()
    {
        if (Application.Current is null)
        {
            return;
        }

        if (IsDarkModeEnabled)
        {
            ApplyDarkAmberMinimalPalette();
        }
        else
        {
            ApplyLightPalette();
        }

        IEnumerable<NavItem> navigationItems = PrimaryNavigation ?? Enumerable.Empty<NavItem>();

        if (LibraryNavigation is not null)
        {
            navigationItems = navigationItems.Concat(LibraryNavigation);
        }

        var activeNavigation = navigationItems.FirstOrDefault(item => item.IsActive)
            ?? PrimaryNavigation?.FirstOrDefault();

        if (activeNavigation is not null)
        {
            ApplyActiveAccent(activeNavigation);
        }
    }

    private void RefreshThemeBoundItems()
    {
        foreach (var printer in RegisteredPrinters)
        {
            printer.RefreshTheme();
        }

    }

    private static void ApplyLightPalette()
    {
        SetResourceBrush("AccentBrush", "#FA233B");
        SetResourceBrush("AccentSoftBrush", "#FFE8EC");
        SetResourceBrush("SurfaceBrush", "#F5F5F7");
        SetResourceBrush("SurfaceAltBrush", "#F2F2F7");
        SetResourceBrush("PanelBrush", "#FFFFFF");
        SetResourceBrush("PanelAltBrush", "#FBFBFD");
        SetResourceBrush("PanelMutedBrush", "#F5F5F7");
        SetResourceBrush("LineBrush", "#E5E5EA");
        SetResourceBrush("MutedTextBrush", "#86868B");
        SetResourceBrush("PrimaryTextBrush", "#1D1D1F");
        SetResourceBrush("SoftTextBrush", "#515154");
        SetResourceBrush("ChipBrush", "#F2F2F7");

        SetResourceBrush("ButtonPrimaryBrush", "#FA233B");
        SetResourceBrush("ButtonPrimaryForegroundBrush", "#FFFFFF");
        SetResourceBrush("ButtonPrimaryHoverBrush", "#E01E35");
        SetResourceBrush("ButtonSecondaryBrush", "#F2F2F7");
        SetResourceBrush("ButtonSecondaryHoverBrush", "#E8E8ED");
        SetResourceBrush("ButtonOutlineHoverBrush", "#FFE8EC");
        SetResourceBrush("ButtonGhostHoverBrush", "#F2F2F7");
        SetResourceBrush("ButtonDestructiveBrush", "#EF4444");
        SetResourceBrush("ButtonDestructiveHoverBrush", "#DC2626");
        SetResourceBrush("ButtonRingBrush", "#80FA233B");
        SetResourceBrush("ButtonDarkHoverBrush", "#2A3342");
    }

    private static void ApplyDarkAmberMinimalPalette()
    {
        // Apple Music esintili koyu minimal palet: kırmızı vurgu, düşük kontrastlı kartlar, yumuşak ayraçlar.
        SetResourceBrush("AccentBrush", "#FA233B");
        SetResourceBrush("AccentSoftBrush", "#2A1116");
        SetResourceBrush("SurfaceBrush", "#111114");
        SetResourceBrush("SurfaceAltBrush", "#151518");
        SetResourceBrush("PanelBrush", "#1C1C1E");
        SetResourceBrush("PanelAltBrush", "#202024");
        SetResourceBrush("PanelMutedBrush", "#18181B");
        SetResourceBrush("LineBrush", "#2C2C30");
        SetResourceBrush("MutedTextBrush", "#9B9BA1");
        SetResourceBrush("PrimaryTextBrush", "#F5F5F7");
        SetResourceBrush("SoftTextBrush", "#D1D1D6");
        SetResourceBrush("ChipBrush", "#242428");

        SetResourceBrush("ButtonPrimaryBrush", "#FA233B");
        SetResourceBrush("ButtonPrimaryForegroundBrush", "#FFFFFF");
        SetResourceBrush("ButtonPrimaryHoverBrush", "#E01E35");
        SetResourceBrush("ButtonSecondaryBrush", "#242428");
        SetResourceBrush("ButtonSecondaryHoverBrush", "#2F2F35");
        SetResourceBrush("ButtonOutlineHoverBrush", "#2A1116");
        SetResourceBrush("ButtonGhostHoverBrush", "#242428");
        SetResourceBrush("ButtonDestructiveBrush", "#EF4444");
        SetResourceBrush("ButtonDestructiveHoverBrush", "#DC2626");
        SetResourceBrush("ButtonRingBrush", "#80FA233B");
        SetResourceBrush("ButtonDarkHoverBrush", "#242428");
    }

    private static void SetResourceBrush(string key, string color)
    {
        var converted = (Color)ColorConverter.ConvertFromString(color);
        if (Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = converted;
            return;
        }

        Application.Current.Resources[key] = new SolidColorBrush(converted);
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static RadialGradientBrush RadialGlow(string center, string middle, string edge)
    {
        var brush = new RadialGradientBrush
        {
            Center = new System.Windows.Point(0.5, 0.5),
            GradientOrigin = new System.Windows.Point(0.5, 0.5),
            RadiusX = 0.95,
            RadiusY = 0.95
        };

        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(center), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle), 0.55));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(edge), 1));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(string first, string second)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };

        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(first), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(second), 1));
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        try { DisposeBcDownloadWatcher(); } catch (Exception ex) { AppLogger.LogException("BC download watcher dispose failed", ex); }
        try
        {
            DisposeOperationJobs();
        }
        catch (Exception ex)
        {
            AppLogger.LogException("İş kuyruğu kapatılırken hata oluştu", ex);
        }

        try
        {
            _jobQueueGate.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.LogException("İş kuyruğu kilidi kapatılamadı", ex);
        }

        try
        {
            _onlineUserCleanupTimer.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Online kullanıcı temizleme zamanlayıcısı kapatılamadı", ex);
        }

        if (_fileTransferService is not null)
        {
            _fileTransferService.TransferReceived -= FileTransferService_TransferReceived;
            _fileTransferService.Dispose();
            _fileTransferService = null;
        }

        if (_presenceService is not null)
        {
            _presenceService.UserOnline -= PresenceService_UserOnline;
            _presenceService.UserOffline -= PresenceService_UserOffline;
            _presenceService.Dispose();
            _presenceService = null;
        }
    }

    private static string CreateLocalPresenceId()
    {
        var raw = $"{Environment.UserDomainName}|{Environment.UserName}|{Environment.MachineName}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateLocalUserDisplayName()
    {
        var userName = Environment.UserName?.Trim();
        var machineName = Environment.MachineName?.Trim();

        if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(machineName))
        {
            return $"{userName} ({machineName})";
        }

        return !string.IsNullOrWhiteSpace(userName)
            ? userName
            : string.IsNullOrWhiteSpace(machineName) ? "Bu bilgisayar" : machineName;
    }

    private static string GetPrimaryIPv4Address()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                ?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class TransferHistoryRecord
    {
        public string DateText { get; set; } = string.Empty;
        public string DirectionText { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Personel { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    private sealed class RenderedPdfPageFile
    {
        public RenderedPdfPageFile(string jpegPath, int pixelWidth, int pixelHeight, double pageWidthPoints, double pageHeightPoints)
        {
            JpegPath = jpegPath;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            PageWidthPoints = pageWidthPoints;
            PageHeightPoints = pageHeightPoints;
        }

        public string JpegPath { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public double PageWidthPoints { get; }
        public double PageHeightPoints { get; }
    }

    private sealed class SavedPrintSettings
    {
        public string Color { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string PaperSize { get; set; } = string.Empty;
        public string MarginProfile { get; set; } = string.Empty;
        public string ScaleMode { get; set; } = string.Empty;
        public string Orientation { get; set; } = string.Empty;
        public string CopyCount { get; set; } = string.Empty;
        public string PageRange { get; set; } = string.Empty;
        public bool DarkMode { get; set; }
        public bool TransferReceiveEnabled { get; set; } = true;
        public string TransferDownloadFolder { get; set; } = string.Empty;
    }

    private sealed class SavedConvertSettings
    {
        public string OutputFolder { get; set; } = string.Empty;
    }

    private sealed class SavedPrinterDevice
    {
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}

