using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public partial class KeyValueRow : ObservableObject
{
    [ObservableProperty] private bool   _isEnabled = true;
    [ObservableProperty] private string _key       = string.Empty;
    [ObservableProperty] private string _value     = string.Empty;

    public Action<KeyValueRow>? OnRemove { get; set; }

    [RelayCommand]
    private void Remove() => OnRemove?.Invoke(this);
}

public partial class KeyValueTableViewModel : ObservableObject
{
    public ObservableCollection<KeyValueRow> Items { get; }
    public event Action? Changed;
    private bool _isAutoAddingTrailingRow;
    private bool _isReplacingRows;

    /// <summary>Creates a table backed by a fresh empty collection.</summary>
    public KeyValueTableViewModel()
    {
        Items = [];
        Items.CollectionChanged += OnItemsChanged;
        EnsureTrailingEmptyRow();
    }

    /// <summary>Populates the table from an existing list of <see cref="NamedValue"/>s.
    /// The Items collection is independent; use <see cref="ToNamedValues"/> to sync back.</summary>
    public KeyValueTableViewModel(IEnumerable<NamedValue> source) : this()
    {
        foreach (var nv in source)
            AddRow(nv.Enabled, nv.Key, nv.Value);
    }

    [RelayCommand]
    public void AddRow() => AddRow(true, string.Empty, string.Empty);

    public void AddRow(bool enabled, string key, string value)
    {
        var row = new KeyValueRow { IsEnabled = enabled, Key = key, Value = value };
        row.OnRemove = r => Items.Remove(r);

        // Keep the trailing editable row at the bottom when adding real data programmatically.
        var hasValue = !string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(value);
        if (hasValue && Items.Count > 0 && IsRowEmpty(Items[^1]))
            Items.Insert(Items.Count - 1, row);
        else
            Items.Add(row);
    }

    public void ReplaceWith(IEnumerable<NamedValue> source)
    {
        _isReplacingRows = true;
        Items.Clear();
        foreach (var nv in source)
            AddRow(nv.Enabled, nv.Key, nv.Value);
        _isReplacingRows = false;

        EnsureTrailingEmptyRow();
        Changed?.Invoke();
    }

    public List<NamedValue> ToNamedValues() =>
        Items.Select(r => new NamedValue { Enabled = r.IsEnabled, Key = r.Key, Value = r.Value })
             .Where(nv => !string.IsNullOrWhiteSpace(nv.Key) || !string.IsNullOrWhiteSpace(nv.Value))
             .ToList();

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (var item in e.NewItems)
                if (item is KeyValueRow row)
                    row.PropertyChanged += OnRowPropertyChanged;

        if (e.OldItems is not null)
            foreach (var item in e.OldItems)
                if (item is KeyValueRow row)
                    row.PropertyChanged -= OnRowPropertyChanged;

        if (_isReplacingRows || _isAutoAddingTrailingRow)
            return;

        EnsureTrailingEmptyRow();
        Changed?.Invoke();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        EnsureTrailingEmptyRow();
        Changed?.Invoke();
    }

    private void EnsureTrailingEmptyRow()
    {
        if (_isAutoAddingTrailingRow)
            return;

        var needsNewRow = Items.Count == 0 || !IsRowEmpty(Items[^1]);
        if (!needsNewRow)
            return;

        _isAutoAddingTrailingRow = true;
        try
        {
            AddRow(true, string.Empty, string.Empty);
        }
        finally
        {
            _isAutoAddingTrailingRow = false;
        }
    }

    private static bool IsRowEmpty(KeyValueRow row)
        => string.IsNullOrWhiteSpace(row.Key) && string.IsNullOrWhiteSpace(row.Value);
}