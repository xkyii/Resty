using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels
{
    public partial class KeyValueRow : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = true;
        [ObservableProperty] private string _key = string.Empty;
        [ObservableProperty] private string _value = string.Empty;

        public Action<KeyValueRow>? OnRemove { get; set; }

        [RelayCommand]
        private void Remove() => OnRemove?.Invoke(this);
    }

    public partial class KeyValueTableViewModel : ObservableObject
    {
        public ObservableCollection<KeyValueRow> Items { get; } = [];

        [RelayCommand]
        public void AddRow()
        {
            var row = new KeyValueRow { IsEnabled = true };
            row.OnRemove = r => Items.Remove(r);
            Items.Add(row);
        }
    }
}
