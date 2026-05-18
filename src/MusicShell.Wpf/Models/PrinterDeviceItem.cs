using System.Windows;
using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class PrinterDeviceItem : ObservableObject
{
    private bool _isDefault;
    private bool _isSelected;
    private bool _isPrintSelected;
    private int _number;
    private Brush _coverBrush = Solid("#FF7A00");

    public string Name { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;

    public Brush CoverBrush
    {
        get => _coverBrush;
        set => SetProperty(ref _coverBrush, value);
    }

    public string Kicker => IsDefault ? "Varsayılan" : "Yazıcı";
    public string Subtitle => IpAddress;

    public int Number
    {
        get => _number;
        set
        {
            if (SetProperty(ref _number, value))
            {
                OnPropertyChanged(nameof(NumberText));
            }
        }
    }

    public string NumberText => Number.ToString("00");

    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (SetProperty(ref _isDefault, value))
            {
                OnPropertyChanged(nameof(DefaultLabel));
                OnPropertyChanged(nameof(Kicker));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(RowBorderBrush));
                OnPropertyChanged(nameof(RowBackgroundBrush));
            }
        }
    }

    public bool IsPrintSelected
    {
        get => _isPrintSelected;
        set
        {
            if (SetProperty(ref _isPrintSelected, value))
            {
                OnPropertyChanged(nameof(PrintCardBorderBrush));
                OnPropertyChanged(nameof(PrintCardBorderThickness));
            }
        }
    }

    public Brush PrintCardBorderBrush => IsPrintSelected ? Solid("#F59E0B") : Solid("#00000000");
    public Thickness PrintCardBorderThickness => IsPrintSelected ? new Thickness(2) : new Thickness(0);

    public string QueueValue => string.IsNullOrWhiteSpace(QueueName) ? Name : QueueName;

    public string QueueDisplay => $"Windows kuyruğu: {QueueValue}";

    public string DefaultLabel => IsDefault ? "Varsayılan" : string.Empty;

    public string ColorCategory
    {
        get
        {
            var value = $"{Name} {QueueName}".ToUpperInvariant();

            if (value.Contains("RENKSİZ") || value.Contains("RENKSIZ") || value.Contains("SİYAH") || value.Contains("SIYAH") || value.Contains("BEYAZ") || value.Contains("BLACK") || value.Contains("B/W"))
            {
                return "Siyah Beyaz";
            }

            if (value.Contains("RENKLİ") || value.Contains("RENKLI") || value.Contains("COLOR") || value.Contains("COLOUR"))
            {
                return "Renkli";
            }

            return "Genel";
        }
    }

    public Brush ColorCategoryBrush => ColorCategory switch
    {
        "Renkli" => Solid("#F59E0B"),
        "Siyah Beyaz" => Solid("#475569"),
        _ => Solid("#22C55E")
    };

    public Brush ColorCategoryBorderBrush => ColorCategory switch
    {
        "Renkli" => IsDarkTheme() ? Solid("#92400E") : Solid("#FCD34D"),
        "Siyah Beyaz" => IsDarkTheme() ? Solid("#475569") : Solid("#CBD5E1"),
        _ => IsDarkTheme() ? Solid("#166534") : Solid("#86EFAC")
    };

    public Brush ColorCategoryBackgroundBrush => ColorCategory switch
    {
        "Renkli" => IsDarkTheme() ? Solid("#261704") : Solid("#FFFBEB"),
        "Siyah Beyaz" => IsDarkTheme() ? Solid("#1E293B") : Solid("#F8FAFC"),
        _ => IsDarkTheme() ? Solid("#0B2415") : Solid("#F0FDF4")
    };

    public Brush RowBorderBrush => IsSelected ? ResourceBrush("LineBrush", "#DADAE3") : ResourceBrush("LineBrush", "#E9E9EF");
    public Brush RowBackgroundBrush => ResourceBrush("PanelBrush", "#FFFFFF");

    public void RefreshTheme()
    {
        OnPropertyChanged(nameof(RowBorderBrush));
        OnPropertyChanged(nameof(RowBackgroundBrush));
        OnPropertyChanged(nameof(ColorCategoryBackgroundBrush));
        OnPropertyChanged(nameof(ColorCategoryBorderBrush));
        OnPropertyChanged(nameof(ColorCategoryBrush));
    }

    private static Brush ResourceBrush(string key, string fallbackColor)
    {
        if (Application.Current?.Resources[key] is Brush brush)
        {
            return brush;
        }

        return Solid(fallbackColor);
    }

    private static bool IsDarkTheme()
    {
        if (Application.Current?.Resources["SurfaceBrush"] is SolidColorBrush brush)
        {
            var color = brush.Color;
            return color.R + color.G + color.B < 384;
        }

        return false;
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
