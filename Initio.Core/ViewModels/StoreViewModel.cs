using System.Collections.ObjectModel;
using System.ComponentModel;
using Initio.Core.Infrastructure;
using Initio.Core.Models;

namespace Initio.Core.ViewModels;

public sealed class StoreViewModel : ObservableObject
{
    private readonly List<StoreTrendItem> _allItems = [];
    private readonly ObservableCollection<CategoryOption> _categories = [];
    private StoreTrendItem? _selectedItem;
    private string _query = string.Empty;
    private CategoryOption? _selectedCategory;

    public ObservableCollection<StoreTrendItem> VisibleItems { get; } = [];
    public ReadOnlyObservableCollection<CategoryOption> Categories { get; }

    public StoreViewModel()
    {
        Categories = new ReadOnlyObservableCollection<CategoryOption>(_categories);
    }

    public IReadOnlyList<StoreTrendItem> AllItems => _allItems;

    public StoreTrendItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                RefreshVisibleItems();
            }
        }
    }

    public CategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                foreach (var category in _categories)
                {
                    category.IsSelected = ReferenceEquals(category, value);
                }

                RefreshVisibleItems();
            }
        }
    }

    public int SelectedCount => _allItems.Count(item => item.IsSelected);

    public void SetItems(IReadOnlyList<StoreCatalogEntry> items)
    {
        foreach (var item in _allItems)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        _allItems.Clear();
        _allItems.AddRange(items.Select(CreateBindableItem));
        foreach (var item in _allItems)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }

        BuildCategories();
        OnPropertyChanged(nameof(SelectedCount));
    }

    public void UpdateCatalogStatus(IReadOnlyCollection<AppItem> catalogItems, string? installedInventoryOutput = null)
    {
        foreach (var item in _allItems)
        {
            item.CatalogStatus = CatalogStatusResolver.Resolve(item.WingetId, item.Name, catalogItems, installedInventoryOutput);
        }
    }

    public void SelectAllVisible()
    {
        foreach (var item in VisibleItems)
        {
            item.IsSelected = true;
        }
    }

    public void SelectNone()
    {
        foreach (var item in _allItems)
        {
            item.IsSelected = false;
        }
    }

    private void BuildCategories()
    {
        _categories.Clear();
        _categories.Add(new CategoryOption("all", "All"));
        foreach (var category in _allItems.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _categories.Add(new CategoryOption(category, category));
        }

        SelectedCategory = _categories.FirstOrDefault();
    }

    private void RefreshVisibleItems()
    {
        var filtered = _allItems.Where(FilterItem).ToList();
        VisibleItems.Clear();
        foreach (var item in filtered)
        {
            VisibleItems.Add(item);
        }
    }

    private bool FilterItem(StoreTrendItem item)
    {
        var categoryKey = SelectedCategory?.Key ?? "all";
        var matchesCategory = string.Equals(categoryKey, "all", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Category, categoryKey, StringComparison.OrdinalIgnoreCase);
        var matchesQuery = string.IsNullOrWhiteSpace(Query) ||
            item.Name.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
            item.WingetId.Contains(Query, StringComparison.OrdinalIgnoreCase);
        return matchesCategory && matchesQuery;
    }

    private static StoreTrendItem CreateBindableItem(StoreCatalogEntry item)
    {
        return new StoreTrendItem(item.Category, item.Rank, item.Name, item.WingetId, item.Rating, item.PopularitySignal);
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StoreTrendItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
        }
    }
}
