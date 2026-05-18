using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class PendingTransferItem : ObservableObject
{
    private string _statusText = "Bekliyor";

    public PendingTransferItem(
        string senderName,
        string recipientName,
        IEnumerable<TransferFileItem> files,
        string destinationFolder)
    {
        Id = Guid.NewGuid().ToString("N");
        SenderName = senderName?.Trim() ?? string.Empty;
        RecipientName = recipientName?.Trim() ?? string.Empty;
        DestinationFolder = destinationFolder?.Trim() ?? string.Empty;
        CreatedAt = DateTime.Now;
        Files = new ObservableCollection<TransferFileItem>(files);
    }

    public string Id { get; }
    public string SenderName { get; }
    public string RecipientName { get; }
    public DateTime CreatedAt { get; }
    public string DestinationFolder { get; }
    public ObservableCollection<TransferFileItem> Files { get; }

    public string DateText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
    public int FileCount => Files.Count;
    public string FileCountText => FileCount <= 1 ? "1 dosya" : $"{FileCount} dosya";
    public string PrimaryFileName => Files.FirstOrDefault()?.FileName ?? "Transfer paketi";
    public string SummaryText => FileCount <= 1 ? PrimaryFileName : $"{PrimaryFileName} + {FileCount - 1} dosya";
    public string SenderSummaryText => string.IsNullOrWhiteSpace(SenderName) ? "Bilinmeyen gönderici" : SenderName;
    public string DestinationText => string.IsNullOrWhiteSpace(DestinationFolder) ? "Klasör seçilmedi" : DestinationFolder;
    public string TotalSizeText => TransferFileItem.FormatSize(Files.Sum(file => file.SizeBytes));

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value?.Trim() ?? string.Empty);
    }

    public bool SourceFilesExist => Files.All(file => File.Exists(file.FullPath));
}
