using System.Collections.ObjectModel;
using System.ComponentModel;
using Initio.Core.Infrastructure;
using Initio.Core.Models;

namespace Initio.Core.ViewModels;

public sealed class SearchViewModel : ObservableObject
{
    private readonly ObservableCollection<StoreTrendItem> _results = [];
    private bool _isSearching;
    private string _query = string.Empty;

    public ReadOnlyObservableCollection<StoreTrendItem> Results { get; }

    public SearchViewModel()
    {
        Results = new ReadOnlyObservableCollection<StoreTrendItem>(_results);
    }

    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public int SelectedCount => _results.Count(item => item.IsSelected);

    public void SetResults(IEnumerable<WingetSearchResult> results)
    {
        foreach (var result in _results)
        {
            result.PropertyChanged -= Result_PropertyChanged;
        }

        _results.Clear();
        var rank = 0;
        foreach (var result in results)
        {
            rank++;
            var item = new StoreTrendItem("Search Result", rank, result.Name, result.WingetId, 0, "winget");
            item.PropertyChanged += Result_PropertyChanged;
            _results.Add(item);
        }

        OnPropertyChanged(nameof(SelectedCount));
    }

    public void UpdateCatalogStatus(IReadOnlyCollection<AppItem> catalogItems, string? installedInventoryOutput = null)
    {
        foreach (var item in _results)
        {
            item.CatalogStatus = CatalogStatusResolver.Resolve(item.WingetId, item.Name, catalogItems, installedInventoryOutput);
        }
    }

    public void SelectAll()
    {
        foreach (var item in _results)
        {
            item.IsSelected = true;
        }
    }

    public void SelectNone()
    {
        foreach (var item in _results)
        {
            item.IsSelected = false;
        }
    }

    private void Result_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StoreTrendItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
        }
    }
}
