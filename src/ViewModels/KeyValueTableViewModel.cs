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

    /// <summary>Creates a table backed by a fresh empty collection.</summary>
    public KeyValueTableViewModel()
    {
        Items = [];
        Items.CollectionChanged += OnItemsChanged;
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
        Items.Add(row);
    }

    public void ReplaceWith(IEnumerable<NamedValue> source)
    {
        Items.Clear();
        foreach (var nv in source)
            AddRow(nv.Enabled, nv.Key, nv.Value);
        Changed?.Invoke();
    }

    public List<NamedValue> ToNamedValues() =>
        Items.Select(r => new NamedValue { Enabled = r.IsEnabled, Key = r.Key, Value = r.Value })
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

        Changed?.Invoke();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => Changed?.Invoke();
}