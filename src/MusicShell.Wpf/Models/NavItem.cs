using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class NavItem : ObservableObject
{
    private bool _isActive;

    public string Icon { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Brush IconBrush { get; init; } = Solid("#2563EB");
    public Brush GlowBrush { get; init; } = Brushes.Transparent;
    public Brush AccentBrush { get; init; } = Solid("#2563EB");
    public Brush AccentSoftBrush { get; init; } = Solid("#EFF4FE");
    public Brush AccentBorderBrush { get; init; } = Solid("#B6CDF8");
    public Brush AccentRingBrush { get; init; } = Solid("#B6CDF8");

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
