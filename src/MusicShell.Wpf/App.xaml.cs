using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MusicShell.Infrastructure;
using MusicShell.Services;

namespace MusicShell;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyHiddenScrollerChrome();

        AppLogger.CleanupOldLogs(keepDays: 30);
        TransferStagingCleanupService.CleanupOldIncomingStagingFolders(TimeSpan.FromHours(24));
        AppLogger.Log("ToolBridge başlatıldı.");

        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.LogException("UI iş parçacığında beklenmeyen hata", args.Exception);
            MessageBox.Show(
                "Beklenmeyen bir hata oluştu. Teknik detaylar log dosyasına yazıldı. Uygulama çalışmaya devam edecek.",
                "ToolBridge",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                AppLogger.LogException("Uygulama genelinde beklenmeyen hata", exception);
            }
            else
            {
                AppLogger.Log("Uygulama genelinde beklenmeyen hata: bilinmeyen hata nesnesi.");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.LogException("Arka plan görevinde yakalanmayan hata", args.Exception);
            args.SetObserved();
        };
    }

    private void ApplyHiddenScrollerChrome()
    {
        try
        {
            Resources[typeof(ScrollBar)] = CreateHiddenScrollBarStyle();
            AppLogger.Log("ScrollBar görsel bileşenleri gizlendi. Mouse wheel / touchpad scroll aktif kalacak.");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("ScrollBar gizleme stili uygulanamadı", ex);
        }
    }

    private static Style CreateHiddenScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 0d));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 0d));
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 0d));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0d));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(UIElement.IsHitTestVisibleProperty, false));
        style.Setters.Add(new Setter(Control.FocusableProperty, false));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));

        var hiddenTemplate = new ControlTemplate(typeof(ScrollBar));
        var hiddenRoot = new FrameworkElementFactory(typeof(Border));
        hiddenRoot.SetValue(FrameworkElement.WidthProperty, 0d);
        hiddenRoot.SetValue(FrameworkElement.HeightProperty, 0d);
        hiddenRoot.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        hiddenTemplate.VisualTree = hiddenRoot;
        style.Setters.Add(new Setter(Control.TemplateProperty, hiddenTemplate));

        return style;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Log("ToolBridge kapatıldı.");
        base.OnExit(e);
    }
}