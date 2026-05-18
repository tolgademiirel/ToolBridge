using System.Windows.Media;

namespace MusicShell.Models;

public sealed class ToolCard
{
    public string Kicker { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public Brush CoverBrush { get; init; } = Brushes.LightGray;
    public bool IsLarge { get; init; }
}
