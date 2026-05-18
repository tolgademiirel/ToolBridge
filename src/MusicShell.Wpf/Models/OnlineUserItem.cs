using System;
using System.Windows;
using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class OnlineUserItem : ObservableObject
{
    private string _displayName;
    private string _presenceId = string.Empty;
    private string _machineName = string.Empty;
    private string _ipAddress = string.Empty;
    private DateTime _lastSeen = DateTime.Now;
    private bool _isOnline;
    private bool _isTransferReceiveEnabled;
    private bool _hasTransferNotification;
    private int _pendingTransferCount;
    private string _lastTransferFolder = string.Empty;
    private string _transferDownloadFolder = string.Empty;
    private bool _isSelectedForTransfer;

    public OnlineUserItem(string displayName, bool isOnline = true, bool isTransferReceiveEnabled = true)
    {
        _displayName = displayName;
        _isOnline = isOnline;
        _isTransferReceiveEnabled = isTransferReceiveEnabled;
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value?.Trim() ?? string.Empty);
    }

    public string PresenceId
    {
        get => _presenceId;
        set => SetProperty(ref _presenceId, value?.Trim() ?? string.Empty);
    }

    public string MachineName
    {
        get => _machineName;
        set
        {
            if (SetProperty(ref _machineName, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(NetworkInfoText));
            }
        }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (SetProperty(ref _ipAddress, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(NetworkInfoText));
            }
        }
    }

    public DateTime LastSeen
    {
        get => _lastSeen;
        set
        {
            if (SetProperty(ref _lastSeen, value))
            {
                OnPropertyChanged(nameof(NetworkInfoText));
            }
        }
    }

    public string NetworkInfoText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MachineName) && !string.IsNullOrWhiteSpace(IpAddress))
            {
                return $"{MachineName} • {IpAddress}";
            }

            if (!string.IsNullOrWhiteSpace(MachineName))
            {
                return MachineName;
            }

            return string.IsNullOrWhiteSpace(IpAddress) ? "Ağ bilgisi bekleniyor" : IpAddress;
        }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (SetProperty(ref _isOnline, value))
            {
                OnPropertyChanged(nameof(CanReceiveTransfers));
                OnPropertyChanged(nameof(TransferStatusText));
            }
        }
    }

    public bool IsTransferReceiveEnabled
    {
        get => _isTransferReceiveEnabled;
        set
        {
            if (SetProperty(ref _isTransferReceiveEnabled, value))
            {
                OnPropertyChanged(nameof(CanReceiveTransfers));
                OnPropertyChanged(nameof(TransferStatusText));
            }
        }
    }

    public string TransferDownloadFolder
    {
        get => _transferDownloadFolder;
        set => SetProperty(ref _transferDownloadFolder, value?.Trim() ?? string.Empty);
    }

    public bool IsSelectedForTransfer
    {
        get => _isSelectedForTransfer;
        set
        {
            if (SetProperty(ref _isSelectedForTransfer, value))
            {
                OnPropertyChanged(nameof(SelectionBorderBrush));
                OnPropertyChanged(nameof(SelectionBorderThickness));
                OnPropertyChanged(nameof(SelectionBackgroundBrush));
            }
        }
    }

    public Brush SelectionBorderBrush => IsSelectedForTransfer ? Solid("#3B82F6") : ResourceBrush("LineBrush", "#E5E7EB");
    public Thickness SelectionBorderThickness => IsSelectedForTransfer ? new Thickness(2) : new Thickness(1);
    public Brush SelectionBackgroundBrush => IsSelectedForTransfer ? ResourceBrush("PanelMutedBrush", "#EFF6FF") : ResourceBrush("PanelBrush", "#FFFFFF");

    public bool HasTransferNotification
    {
        get => _hasTransferNotification;
        private set
        {
            if (SetProperty(ref _hasTransferNotification, value))
            {
                OnPropertyChanged(nameof(TransferNotificationVisibility));
                OnPropertyChanged(nameof(TransferNotificationText));
            }
        }
    }

    public int PendingTransferCount
    {
        get => _pendingTransferCount;
        private set
        {
            if (SetProperty(ref _pendingTransferCount, value))
            {
                OnPropertyChanged(nameof(TransferNotificationText));
            }
        }
    }

    public string LastTransferFolder
    {
        get => _lastTransferFolder;
        private set
        {
            if (SetProperty(ref _lastTransferFolder, value))
            {
                OnPropertyChanged(nameof(TransferNotificationText));
            }
        }
    }

    public bool CanReceiveTransfers => IsOnline && IsTransferReceiveEnabled;

    public string TransferStatusText => !IsOnline
        ? "Offline"
        : IsTransferReceiveEnabled ? "Transfer alımı açık" : "Transfer alımı kapalı";

    public Visibility TransferNotificationVisibility => HasTransferNotification ? Visibility.Visible : Visibility.Collapsed;

    public string TransferNotificationText
    {
        get
        {
            if (!HasTransferNotification)
            {
                return "Yeni transfer yok";
            }

            var fileText = PendingTransferCount <= 1 ? "1 yeni transfer" : $"{PendingTransferCount} yeni transfer";
            return string.IsNullOrWhiteSpace(LastTransferFolder)
                ? fileText
                : $"{fileText} indirildi: {LastTransferFolder}";
        }
    }

    public void RegisterIncomingTransfer(int fileCount, string downloadFolder)
    {
        PendingTransferCount += Math.Max(fileCount, 1);
        LastTransferFolder = downloadFolder?.Trim() ?? string.Empty;
        HasTransferNotification = true;
    }

    public void ClearTransferNotification()
    {
        PendingTransferCount = 0;
        LastTransferFolder = string.Empty;
        HasTransferNotification = false;
    }

    public override string ToString() => DisplayName;

    private static Brush ResourceBrush(string key, string fallbackColor)
    {
        if (Application.Current?.Resources[key] is Brush brush)
        {
            return brush;
        }

        return Solid(fallbackColor);
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
