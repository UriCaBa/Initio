using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NewPCSetupWPF.Models;

public sealed class AppItem : INotifyPropertyChanged
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
        set => SetField(ref _isSelected, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetField(ref _isInstalled, value) && value)
            {
                IsSelected = false;
            }
        }
    }

    public string InstallStatus
    {
        get => _installStatus;
        set => SetField(ref _installStatus, value);
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
