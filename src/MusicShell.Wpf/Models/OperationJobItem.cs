using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using MusicShell.Infrastructure;

namespace MusicShell.Models;

public sealed class OperationJobItem : ObservableObject, IDisposable
{
    private int _progress;
    private string _statusText = "Kuyrukta";
    private string _detailText = string.Empty;
    private bool _isRunning;
    private bool _isCompleted;
    private bool _isCancelled;
    private bool _hasError;
    private bool _isDisposed;

    public OperationJobItem(string jobType, string title, string summary)
    {
        Id = Guid.NewGuid().ToString("N");
        JobType = string.IsNullOrWhiteSpace(jobType) ? "İşlem" : jobType.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? JobType : title.Trim();
        Summary = summary?.Trim() ?? string.Empty;
        CreatedAt = DateTime.Now;
        CancellationTokenSource = new CancellationTokenSource();
    }

    public string Id { get; }
    public string JobType { get; }
    public string Title { get; }
    public string Summary { get; }
    public DateTime CreatedAt { get; }
    public CancellationTokenSource CancellationTokenSource { get; }
    public CancellationToken CancellationToken => CancellationTokenSource.Token;
    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");

    public int Progress
    {
        get => _progress;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _progress, normalized))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, string.IsNullOrWhiteSpace(value) ? "Kuyrukta" : value.Trim());
    }

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value?.Trim() ?? string.Empty);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CancelButtonVisibility));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                OnPropertyChanged(nameof(CancelButtonVisibility));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public bool IsCancelled
    {
        get => _isCancelled;
        private set
        {
            if (SetProperty(ref _isCancelled, value))
            {
                OnPropertyChanged(nameof(CancelButtonVisibility));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string ProgressText => $"%{Progress}";
    public Visibility CancelButtonVisibility => !IsCompleted && !IsCancelled ? Visibility.Visible : Visibility.Collapsed;
    public Brush StatusBrush
    {
        get
        {
            if (HasError) return Solid("#DC2626");
            if (IsCancelled) return Solid("#F59E0B");
            if (IsCompleted) return Solid("#16A34A");
            if (IsRunning) return Solid("#3B82F6");
            return Solid("#777783");
        }
    }

    public void MarkQueued(string detail = "")
    {
        IsRunning = false;
        IsCompleted = false;
        IsCancelled = false;
        HasError = false;
        StatusText = "Kuyrukta";
        DetailText = detail;
        Progress = 0;
    }

    public void MarkRunning(string detail = "")
    {
        IsRunning = true;
        IsCompleted = false;
        IsCancelled = false;
        HasError = false;
        StatusText = "Çalışıyor";
        DetailText = detail;
    }

    public void UpdateProgress(int progress, string detail = "")
    {
        Progress = progress;
        if (!string.IsNullOrWhiteSpace(detail))
        {
            DetailText = detail;
        }
    }

    public void RequestCancel()
    {
        if (IsCompleted || IsCancelled)
        {
            return;
        }

        StatusText = "İptal ediliyor";
        DetailText = "Kullanıcı iptal istedi.";
        try { CancellationTokenSource.Cancel(); } catch { }
    }

    public void MarkCancelled(string detail = "İşlem iptal edildi.")
    {
        IsRunning = false;
        IsCancelled = true;
        IsCompleted = false;
        HasError = false;
        StatusText = "İptal edildi";
        DetailText = detail;
    }

    public void MarkCompleted(string detail = "Tamamlandı")
    {
        IsRunning = false;
        IsCancelled = false;
        IsCompleted = true;
        HasError = false;
        StatusText = "Tamamlandı";
        DetailText = detail;
        Progress = 100;
    }

    public void MarkError(string detail)
    {
        IsRunning = false;
        IsCompleted = true;
        IsCancelled = false;
        HasError = true;
        StatusText = "Hata";
        DetailText = string.IsNullOrWhiteSpace(detail) ? "İşlem tamamlanamadı." : detail.Trim();
    }


    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            CancellationTokenSource.Dispose();
        }
        catch
        {
            // Dispose sırasında oluşabilecek hatalar uygulama akışını etkilememeli.
        }
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
