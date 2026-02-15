// StoreTrendItem.cs
// Represents an app from the Store catalog that can be browsed and selected for installation.
// Converted from record to class to support mutable IsSelected property with property change notification.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NewPCSetupWPF.Models;

public sealed class StoreTrendItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _catalogStatus = string.Empty;

    public StoreTrendItem(string category, int rank, string name, string wingetId, double rating, string popularitySignal)
    {
        Category = category;
        Rank = rank;
        Name = name;
        WingetId = wingetId;
        Rating = rating;
        PopularitySignal = popularitySignal;
    }

    public string Category { get; }
    public int Rank { get; }
    public string Name { get; }
    public string WingetId { get; }
    public double Rating { get; }
    public string PopularitySignal { get; }
    public int TrendScore => Math.Max(42, 100 - ((Rank - 1) * 2));

    /// <summary>Whether this store item is checked/selected by the user for adding to the install catalog.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Status text: "In My Setup", "Installed", or "" — set by code-behind.</summary>
    public string CatalogStatus
    {
        get => _catalogStatus;
        set
        {
            if (_catalogStatus == value) return;
            _catalogStatus = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
