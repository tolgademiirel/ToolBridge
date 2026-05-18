using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class ConvertFileItem : ObservableObject
{
    private int _progress;
    private string _statusText = "Hazır";
    private Brush _statusBrush = Solid("#777783");
    private string _outputPath = string.Empty;
    private bool _isSelectedForPrintPool;

    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string SizeText { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;

    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        set => SetProperty(ref _statusBrush, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(OutputName));
                OnPropertyChanged(nameof(HasOutputFile));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsSelectedForPrintPool
    {
        get => _isSelectedForPrintPool;
        set
        {
            if (SetProperty(ref _isSelectedForPrintPool, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string OutputName => string.IsNullOrWhiteSpace(OutputPath) ? "Çıktı bekleniyor" : Path.GetFileName(OutputPath);
    public bool HasOutputFile => !string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath);
    public string TypeLabel => string.IsNullOrWhiteSpace(Extension) ? "FILE" : Extension;

    public void MarkReady()
    {
        Progress = 0;
        StatusText = "Hazır";
        StatusBrush = Solid("#777783");
        OutputPath = string.Empty;
    }

    public void MarkConverting()
    {
        Progress = 45;
        StatusText = "Dönüştürülüyor";
        StatusBrush = Solid("#F59E0B");
    }

    public void MarkCompleted(string outputPath)
    {
        Progress = 100;
        StatusText = "Tamamlandı";
        StatusBrush = Solid("#16A34A");
        OutputPath = outputPath;
    }

    public void MarkError(string message)
    {
        Progress = 100;
        StatusText = string.IsNullOrWhiteSpace(message) ? "Hata" : message;
        StatusBrush = Solid("#DC2626");
    }

    public static ConvertFileItem FromFileInfo(FileInfo fileInfo)
    {
        return new ConvertFileItem
        {
            FileName = fileInfo.Name,
            FullPath = fileInfo.FullName,
            SizeBytes = fileInfo.Length,
            SizeText = UploadFileItem.FormatSize(fileInfo.Length),
            Extension = fileInfo.Extension.TrimStart('.').ToUpperInvariant()
        };
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
