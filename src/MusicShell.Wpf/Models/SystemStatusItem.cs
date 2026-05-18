using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class SystemStatusItem : ObservableObject
{
    private string _statusText = string.Empty;
    private string _detailText = string.Empty;
    private string _versionText = string.Empty;
    private bool _isAvailable;

    public SystemStatusItem(string name, string purpose, bool isAvailable, string detailText, string versionText = "")
    {
        Name = name;
        Purpose = purpose;
        _isAvailable = isAvailable;
        _statusText = isAvailable ? "Hazır" : "Eksik";
        _detailText = detailText;
        _versionText = versionText?.Trim() ?? string.Empty;
    }

    public string Name { get; }
    public string Purpose { get; }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                StatusText = value ? "Hazır" : "Eksik";
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value?.Trim() ?? string.Empty);
    }

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value?.Trim() ?? string.Empty);
    }

    public string VersionText
    {
        get => _versionText;
        set
        {
            if (SetProperty(ref _versionText, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(VersionVisibility));
            }
        }
    }

    public System.Windows.Visibility VersionVisibility => string.IsNullOrWhiteSpace(VersionText) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public Brush StatusBrush => IsAvailable ? Solid("#16A34A") : Solid("#DC2626");

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
