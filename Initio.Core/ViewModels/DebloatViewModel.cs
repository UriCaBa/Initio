using System.Collections.ObjectModel;
using System.ComponentModel;
using Initio.Core.Infrastructure;
using Initio.Core.Models;

namespace Initio.Core.ViewModels;

public sealed class DebloatViewModel : ObservableObject
{
    private readonly List<BloatwareItem> _allItems = [];
    private readonly ObservableCollection<CategoryOption> _categories = [];
    private CategoryOption? _selectedCategory;

    public DebloatViewModel()
    {
        Categories = new ReadOnlyObservableCollection<CategoryOption>(_categories);
    }

    public ObservableCollection<BloatwareItem> VisibleItems { get; } = [];
    public ReadOnlyObservableCollection<CategoryOption> Categories { get; }
    public IReadOnlyList<BloatwareItem> AllItems => _allItems;

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

    public string Summary
    {
        get
        {
            var detected = _allItems.Count(item => item.IsInstalled);
            var selected = _allItems.Count(item => item.IsSelected && item.IsInstalled);
            var removed = _allItems.Count(item => !item.IsInstalled && item.RemovalStatus == "Removed");
            return $"{selected} selected | {detected} detected | {removed} removed";
        }
    }

    public void SetItems(IReadOnlyList<BloatwareDefinition> items)
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
        OnPropertyChanged(nameof(Summary));
    }

    public void ApplyDetectedPackages(IReadOnlySet<string> installedPackages)
    {
        foreach (var item in _allItems)
        {
            var isInstalled = installedPackages.Contains(item.PackageName);
            item.IsInstalled = isInstalled;
            item.RemovalStatus = isInstalled ? "Detected" : "Not Found";
        }

        RefreshVisibleItems();
        OnPropertyChanged(nameof(Summary));
    }

    public IReadOnlyList<BloatwareItem> GetSelectedInstalledItems()
    {
        return _allItems.Where(item => item.IsSelected && item.IsInstalled).ToList();
    }

    public void SelectAllVisibleInstalled()
    {
        foreach (var item in VisibleItems)
        {
            if (item.IsInstalled)
            {
                item.IsSelected = true;
            }
        }
    }

    public void SelectNone()
    {
        foreach (var item in _allItems)
        {
            item.IsSelected = false;
        }
    }

    public void RefreshVisibleItems()
    {
        var categoryKey = SelectedCategory?.Key ?? "all";
        var filtered = _allItems.Where(item =>
            string.Equals(categoryKey, "all", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Category, categoryKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        VisibleItems.Clear();
        foreach (var item in filtered)
        {
            VisibleItems.Add(item);
        }

        OnPropertyChanged(nameof(Summary));
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

    private static BloatwareItem CreateBindableItem(BloatwareDefinition item)
    {
        return new BloatwareItem
        {
            Name = item.Name,
            Category = item.Category,
            PackageName = item.PackageName,
            Description = item.Description
        };
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BloatwareItem.IsSelected) or nameof(BloatwareItem.IsInstalled) or nameof(BloatwareItem.RemovalStatus))
        {
            OnPropertyChanged(nameof(Summary));
        }
    }
}
