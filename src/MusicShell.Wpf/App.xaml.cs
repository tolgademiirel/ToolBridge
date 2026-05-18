using System;
using System.Threading.Tasks;
using System.Windows;
using MusicShell.Infrastructure;

namespace MusicShell;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLogger.CleanupOldLogs(keepDays: 30);
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

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Log("ToolBridge kapatıldı.");
        base.OnExit(e);
    }
}
