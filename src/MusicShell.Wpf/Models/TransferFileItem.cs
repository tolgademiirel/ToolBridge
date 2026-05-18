using System;
using System.IO;
using System.Windows;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class TransferFileItem : ObservableObject
{
    private int _progress;
    private bool _isInProgress;
    private string _statusText = "Hazır";
    private string _destinationPath = string.Empty;
    private string _sourceChecksum = string.Empty;
    private string _verifiedChecksum = string.Empty;

    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string SizeText { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public string TypeLabel => string.IsNullOrWhiteSpace(Extension) ? "FILE" : Extension;
    public bool IsLargeTransfer => SizeBytes > 50L * 1024 * 1024;
    public string TransferModeText => IsLargeTransfer ? "Parçalı aktarım + checksum" : "Standart aktarım";

    public string SourceChecksum
    {
        get => _sourceChecksum;
        set
        {
            if (SetProperty(ref _sourceChecksum, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(ChecksumText));
            }
        }
    }

    public string VerifiedChecksum
    {
        get => _verifiedChecksum;
        set
        {
            if (SetProperty(ref _verifiedChecksum, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(ChecksumText));
            }
        }
    }

    public string ChecksumText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(VerifiedChecksum))
            {
                return $"Checksum doğrulandı: {VerifiedChecksum[..Math.Min(12, VerifiedChecksum.Length)]}...";
            }

            if (!string.IsNullOrWhiteSpace(SourceChecksum))
            {
                return $"Checksum hazır: {SourceChecksum[..Math.Min(12, SourceChecksum.Length)]}...";
            }

            return IsLargeTransfer ? "Checksum hazırlanacak" : string.Empty;
        }
    }

    public int Progress
    {
        get => _progress;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _progress, normalized))
            {
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }
    }

    public bool IsInProgress
    {
        get => _isInProgress;
        set
        {
            if (SetProperty(ref _isInProgress, value))
            {
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, string.IsNullOrWhiteSpace(value) ? "Hazır" : value.Trim()))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value?.Trim() ?? string.Empty);
    }

    public string ProgressText => IsInProgress || Progress > 0
        ? $"{StatusText} • %{Progress}"
        : StatusText;

    public Visibility ProgressVisibility => IsInProgress || (Progress > 0 && Progress < 100)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public void MarkReady()
    {
        Progress = 0;
        IsInProgress = false;
        StatusText = "Hazır";
        DestinationPath = string.Empty;
        SourceChecksum = string.Empty;
        VerifiedChecksum = string.Empty;
    }

    public void MarkQueued()
    {
        Progress = 0;
        IsInProgress = false;
        StatusText = IsLargeTransfer ? "Kabul bekliyor • Parçalı" : "Kabul bekliyor";
    }

    public void MarkSending(int progress)
    {
        IsInProgress = true;
        StatusText = IsLargeTransfer ? "Parçalı gönderiliyor" : "Gönderiliyor";
        Progress = progress;
    }

    public void MarkReceiving(int progress)
    {
        IsInProgress = true;
        StatusText = IsLargeTransfer ? "Parçalı indiriliyor" : "İndiriliyor";
        Progress = progress;
    }

    public void MarkCompleted(string destinationPath = "")
    {
        if (!string.IsNullOrWhiteSpace(destinationPath))
        {
            DestinationPath = destinationPath;
        }

        Progress = 100;
        IsInProgress = false;
        StatusText = "Tamamlandı";
    }

    public void MarkError(string message)
    {
        IsInProgress = false;
        StatusText = string.IsNullOrWhiteSpace(message) ? "Hata" : message.Trim();
    }

    public static string FormatSize(long bytes) => UploadFileItem.FormatSize(bytes);

    public static TransferFileItem FromFileInfo(FileInfo fileInfo)
    {
        return new TransferFileItem
        {
            FileName = fileInfo.Name,
            FullPath = fileInfo.FullName,
            SizeBytes = fileInfo.Length,
            SizeText = UploadFileItem.FormatSize(fileInfo.Length),
            Extension = fileInfo.Extension.TrimStart('.').ToUpperInvariant()
        };
    }
}
