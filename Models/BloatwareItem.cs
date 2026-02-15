using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NewPCSetupWPF.Models;

public sealed class BloatwareItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isInstalled;
    private string _removalStatus = "Detected";

    public required string Name { get; init; }

    public required string Category { get; init; }

    public required string PackageName { get; init; }

    public required string Description { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetField(ref _isInstalled, value) && !value)
            {
                IsSelected = false;
            }
        }
    }

    public string RemovalStatus
    {
        get => _removalStatus;
        set => SetField(ref _removalStatus, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
