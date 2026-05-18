using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class NavItem : ObservableObject
{
    private bool _isActive;

    public string Icon { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Brush IconBrush { get; init; } = Brushes.DarkOrange;
    public Brush GlowBrush { get; init; } = Brushes.Transparent;
    public Brush AccentBrush { get; init; } = Brushes.DarkOrange;
    public Brush AccentSoftBrush { get; init; } = Brushes.SeaShell;
    public Brush AccentBorderBrush { get; init; } = Brushes.Orange;
    public Brush AccentRingBrush { get; init; } = Brushes.Orange;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
