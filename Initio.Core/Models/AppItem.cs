using Initio.Core.Infrastructure;

namespace Initio.Core.Models;

public sealed class AppItem : ObservableObject
{
    private bool _isSelected;
    private bool _isInstalled;
    private string _installStatus = "Ready";

    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string WingetId { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetProperty(ref _isInstalled, value) && value)
            {
                IsSelected = false;
            }
        }
    }

    public string InstallStatus
    {
        get => _installStatus;
        set => SetProperty(ref _installStatus, value);
    }
}
