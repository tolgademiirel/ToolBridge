using System.Windows.Media;

namespace MusicShell.Models;

public sealed class TransferHistoryItem
{
    public string DateText { get; init; } = string.Empty;
    public string DirectionText { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Personel { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public Brush StatusBrush { get; init; } = Brushes.Gray;

    public static TransferHistoryItem Outgoing(string fileName, string personel, string fullPath)
    {
        return Create("Giden", fileName, personel, fullPath);
    }

    public static TransferHistoryItem Incoming(string fileName, string personel, string fullPath)
    {
        return Create("Gelen", fileName, personel, fullPath);
    }

    private static TransferHistoryItem Create(string direction, string fileName, string personel, string fullPath)
    {
        return new TransferHistoryItem
        {
            DateText = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            DirectionText = direction,
            FileName = fileName,
            Personel = personel,
            StatusText = "Tamamlandı",
            FullPath = fullPath,
            StatusBrush = Solid("#16A34A")
        };
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
