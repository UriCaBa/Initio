using Initio.Core.Infrastructure;

namespace Initio.Core.Models;

public sealed class CategoryOption : ObservableObject
{
    private bool _isSelected;

    public CategoryOption(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Key { get; }
    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
