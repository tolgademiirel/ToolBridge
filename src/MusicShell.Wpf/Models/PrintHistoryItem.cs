using System.Windows.Media;

namespace MusicShell.Models;

public sealed class PrintHistoryItem
{
    public string DateText { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string PrinterName { get; init; } = string.Empty;
    public string PageText { get; init; } = string.Empty;
    public string PaperText { get; init; } = string.Empty;
    public string ColorText { get; init; } = string.Empty;
    public Brush ColorBrush { get; init; } = Brushes.Gray;
    public Brush ColorBorderBrush { get; init; } = Brushes.LightGray;
    public Brush ColorBackgroundBrush { get; init; } = Brushes.White;
    public string StatusText { get; init; } = string.Empty;
    public Brush StatusBrush { get; init; } = Brushes.Gray;
    public Brush StatusBorderBrush { get; init; } = Brushes.LightGray;
    public Brush StatusBackgroundBrush { get; init; } = Brushes.White;
}
