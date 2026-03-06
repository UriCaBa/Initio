using System.Collections.ObjectModel;
using System.ComponentModel;
using Initio.Core.Infrastructure;
using Initio.Core.Models;

namespace Initio.Core.ViewModels;

public sealed class CatalogViewModel : ObservableObject
{
    private readonly List<AppItem> _allItems = [];
    private AppItem? _selectedItem;

    public ObservableCollection<AppItem> VisibleItems { get; } = [];

    public IReadOnlyList<AppItem> AllItems => _allItems;

    public AppItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string SelectionSummary
    {
        get
        {
            var installed = _allItems.Count(item => item.IsInstalled);
            var selectable = _allItems.Count - installed;
            var selected = _allItems.Count(item => item.IsSelected && !item.IsInstalled);
            return $"{selected} selected | {selectable} available | {installed} installed";
        }
    }

    public void LoadDefaults(IEnumerable<AppDefinition> defaults, IReadOnlySet<string> knownInstalledIds)
    {
        Clear();
        foreach (var definition in defaults)
        {
            AddApp(definition, knownInstalledIds);
        }

        foreach (var item in _allItems)
        {
            item.IsSelected = !item.IsInstalled;
        }

        RefreshVisibleItems();
    }

    public bool AddApp(AppDefinition definition, IReadOnlySet<string> knownInstalledIds)
    {
        if (_allItems.Any(item => string.Equals(item.WingetId, definition.WingetId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var app = new AppItem
        {
            Name = definition.Name,
            Category = definition.Category,
            WingetId = definition.WingetId
        };
        app.IsSelected = true;
        if (knownInstalledIds.Contains(definition.WingetId))
        {
            app.IsInstalled = true;
            app.InstallStatus = "Installed";
        }

        AttachItem(app);
        _allItems.Add(app);
        RefreshVisibleItems();
        return true;
    }

    public AppItem? RemoveSelected()
    {
        if (SelectedItem is null)
        {
            return null;
        }

        var removed = SelectedItem;
        DetachItem(removed);
        _allItems.Remove(removed);
        SelectedItem = null;
        RefreshVisibleItems();
        return removed;
    }

    public void SetSelectionByProfile(IReadOnlySet<string> selectedWingetIds)
    {
        foreach (var item in _allItems)
        {
            item.IsSelected = selectedWingetIds.Contains(item.WingetId);
        }

        RefreshVisibleItems();
    }

    public IReadOnlyList<AppItem> GetPendingSelectedItems()
    {
        return _allItems.Where(item => item.IsSelected && !item.IsInstalled).ToList();
    }

    public void RefreshInstalledStates(string output, ISet<string> knownInstalledIds)
    {
        knownInstalledIds.Clear();
        foreach (var item in _allItems)
        {
            var isInstalled = output.Contains(item.WingetId, StringComparison.OrdinalIgnoreCase);
            if (!isInstalled && !string.IsNullOrWhiteSpace(item.Name))
            {
                isInstalled = output.Contains(item.Name, StringComparison.OrdinalIgnoreCase);
            }

            if (isInstalled)
            {
                knownInstalledIds.Add(item.WingetId);
            }

            item.IsInstalled = isInstalled;
            item.InstallStatus = isInstalled ? "Installed" : "Pending";
        }

        RefreshVisibleItems();
    }

    public void RefreshVisibleItems()
    {
        var ordered = _allItems
            .OrderBy(item => item.IsInstalled)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        VisibleItems.Clear();
        foreach (var item in ordered)
        {
            VisibleItems.Add(item);
        }

        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void Clear()
    {
        foreach (var item in _allItems)
        {
            DetachItem(item);
        }

        _allItems.Clear();
        VisibleItems.Clear();
        SelectedItem = null;
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void AttachItem(AppItem item)
    {
        item.PropertyChanged += Item_PropertyChanged;
    }

    private void DetachItem(AppItem item)
    {
        item.PropertyChanged -= Item_PropertyChanged;
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppItem.IsSelected) or nameof(AppItem.IsInstalled) or nameof(AppItem.InstallStatus))
        {
            RefreshVisibleItems();
        }
    }
}
