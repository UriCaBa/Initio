using Initio.Core.Infrastructure;

namespace Initio.Core.Models;

public sealed class BloatwareItem : ObservableObject
{
    private bool _isSelected;
    private bool _isInstalled;
    private string _removalStatus = "Not Scanned";

    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string PackageName { get; init; }
    public required string Description { get; init; }

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
            if (SetProperty(ref _isInstalled, value) && !value)
            {
                IsSelected = false;
            }
        }
    }

    public string RemovalStatus
    {
        get => _removalStatus;
        set => SetProperty(ref _removalStatus, value);
    }
}

