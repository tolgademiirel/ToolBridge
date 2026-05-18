using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MusicShell.Models;

namespace MusicShell.ViewModels;

public sealed partial class MainViewModel
{
    private OperationJobItem CreateOperationJob(string jobType, string title, string summary)
    {
        var job = new OperationJobItem(jobType, title, summary);
        OperationJobs.Insert(0, job);
        OnPropertyChanged(nameof(OperationQueueCountText));
        OnPropertyChanged(nameof(OperationQueueListVisibility));
        OnPropertyChanged(nameof(OperationQueueEmptyVisibility));
        CommandManager.InvalidateRequerySuggested();
        return job;
    }

    private async Task<bool> WaitForJobSlotAsync(OperationJobItem job)
    {
        job.MarkQueued("Diğer işlemlerin tamamlanması bekleniyor.");
        try
        {
            await _jobQueueGate.WaitAsync(job.CancellationToken);
            job.MarkRunning("İşlem başlatıldı.");
            return true;
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled("Kuyruktayken iptal edildi.");
            return false;
        }
    }

    private void ReleaseJobSlot()
    {
        try { _jobQueueGate.Release(); } catch { }
    }

    private void CancelOperationJob(object? parameter)
    {
        if (parameter is OperationJobItem job)
        {
            job.RequestCancel();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ClearCompletedJobs()
    {
        foreach (var job in OperationJobs.Where(item => item.IsCompleted || item.IsCancelled).ToList())
        {
            OperationJobs.Remove(job);
            SafeDisposeJob(job);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void DisposeOperationJobs()
    {
        foreach (var job in OperationJobs.ToList())
        {
            try
            {
                job.RequestCancel();
            }
            catch
            {
                // Kapanış sırasında iptal çağrısı başarısız olursa dispose yine denenir.
            }

            OperationJobs.Remove(job);
            SafeDisposeJob(job);
        }
    }

    private static void SafeDisposeJob(OperationJobItem job)
    {
        try
        {
            job.Dispose();
        }
        catch
        {
            // İş kuyruğu temizliği uygulama kapanışını engellememeli.
        }
    }

}
