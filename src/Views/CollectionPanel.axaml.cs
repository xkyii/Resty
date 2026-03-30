using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Views;

public partial class CollectionPanel : UserControl
{
    public CollectionPanel()
    {
        InitializeComponent();
    }

    private void OnCollectionsContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        var panelVM = DataContext as ViewModels.CollectionPanel;
        var canManage = panelVM?.CanManageCollections == true;

        var createItem = this.FindControl<MenuItem>("CreateCollectionMenuItem");
        var importItem = this.FindControl<MenuItem>("ImportCollectionMenuItem");

        if (createItem != null)
            createItem.IsEnabled = canManage;
        if (importItem != null)
            importItem.IsEnabled = canManage;
    }

    private async void OnCreateCollectionClicked(object? sender, RoutedEventArgs e)
    {
        var panelVM = DataContext as ViewModels.CollectionPanel;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (panelVM is null || owner is null || !panelVM.CanManageCollections) return;

        await InputDialog.ShowAsync(
            owner,
            App.Text("Panel.NewCollection"),
            "new-collection",
            collectionName =>
            {
                if (!panelVM.CreateCollection(collectionName))
                    throw new ArgumentException(App.Text("Panel.CreateCollectionFailed"));
                return true;
            });
    }
}