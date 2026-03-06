using Initio.Core.Infrastructure;

namespace Initio.Core.Models;

public sealed class SelectionProfile : ObservableObject
{
    private bool _isSelected;

    public SelectionProfile(string key, string displayName, HashSet<string> selectedWingetIds, bool isCustom = false)
    {
        Key = key;
        DisplayName = displayName;
        SelectedWingetIds = selectedWingetIds;
        IsCustom = isCustom;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public HashSet<string> SelectedWingetIds { get; }
    public bool IsCustom { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
