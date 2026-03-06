using Initio.Core.Infrastructure;

namespace Initio.Core.Models;

public sealed class StoreTrendItem : ObservableObject
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

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string CatalogStatus
    {
        get => _catalogStatus;
        set => SetProperty(ref _catalogStatus, value);
    }
}
