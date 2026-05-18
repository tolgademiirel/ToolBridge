using MusicShell.Services;

namespace MusicShell.ViewModels;

public sealed partial class MainViewModel
{
    private void RefreshConvertEngineStatuses()
    {
        ConvertEngineStatuses.Clear();

        foreach (var status in SystemStatusService.GetConvertEngineStatuses())
        {
            ConvertEngineStatuses.Add(status);
        }

        OnPropertyChanged(nameof(ConvertEngineStatusSummary));
    }
}
