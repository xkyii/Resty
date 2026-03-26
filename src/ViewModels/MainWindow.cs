using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels
{
    public class RequestTabItem : ObservableObject
    {
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public RequestTab? Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private string _title = "New Request";
        private RequestTab? _content;
    }

    public partial class MainWindow : ObservableObject
    {
        public string Title => "Kx.Resty";

        public ObservableCollection<RequestTabItem> Tabs { get; } = [];

        public RequestTabItem? ActiveTab
        {
            get => _activeTab;
            set => SetProperty(ref _activeTab, value);
        }

        public CollectionPanel SidePanel { get; } = new CollectionPanel();

        public bool HasTabs => Tabs.Count > 0;

        public MainWindow()
        {
            Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));
        }

        [RelayCommand]
        public void NewRequest()
        {
            var tab = new RequestTabItem
            {
                Title = "New Request",
                Content = new RequestTab()
            };
            Tabs.Add(tab);
            ActiveTab = tab;
        }

        [RelayCommand]
        public void CloseTab(RequestTabItem tab)
        {
            var idx = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            if (Tabs.Count == 0)
            {
                ActiveTab = null;
                return;
            }

            ActiveTab = Tabs[Math.Clamp(idx, 0, Tabs.Count - 1)];
        }

        [RelayCommand]
        public void OpenPreferences()
        {
            App.ShowDialog(new Dialogs.Preferences());
        }

        private RequestTabItem? _activeTab;
    }
}
