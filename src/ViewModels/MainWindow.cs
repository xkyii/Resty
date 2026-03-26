using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels;

public class RequestTabItem : ObservableObject
{
    public string Title
    {
        get;
        set => SetProperty(ref field, value);
    } = "New Request";

    public RequestTab? Content
    {
        get;
        set => SetProperty(ref field, value);
    }
}

public partial class MainWindow : ObservableObject
{
    public string Title => "Kx.Resty";

    public ObservableCollection<RequestTabItem> Tabs { get; } = [];

    public RequestTabItem? ActiveTab
    {
        get;
        set => SetProperty(ref field, value);
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
}