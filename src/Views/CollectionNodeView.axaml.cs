using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Kx.Resty.Models;

namespace Kx.Resty.Views;

public partial class CollectionNodeView : UserControl
{
    public CollectionNodeView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles a tap on a request entry row.
    /// Walks up the visual tree to find the <see cref="CollectionPanel"/> and
    /// calls <see cref="ViewModels.CollectionPanel.OpenRequest"/> on its ViewModel.
    /// </summary>
    private void OnRequestEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode { Collection: { } collection }) return;
        if ((sender as Border)?.DataContext is not HttpRequestEntry entry) return;

        var panelView = this.FindAncestorOfType<CollectionPanel>();
        (panelView?.DataContext as ViewModels.CollectionPanel)?.OpenRequest(entry, collection);
    }

    private void OnCollectionNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode node) return;
        // Allow selecting both directories and collection (.http) nodes.
        var panelView = this.FindAncestorOfType<CollectionPanel>();
        var panelVm = panelView?.DataContext as ViewModels.CollectionPanel;
        if (panelVm is null) return;

        panelVm.SelectCollectionNode(node);
    }

    private void OnCollectionNodeDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode node) return;
        node.ToggleExpanded();
    }

    private void OnToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode node) return;
        node.ToggleExpanded();
    }

    private async void OnRenameCollectionClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode { Collection: { } collection }) return;

        var panelView = this.FindAncestorOfType<CollectionPanel>();
        var panelVM = panelView?.DataContext as ViewModels.CollectionPanel;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (panelVM is null || owner is null) return;

        await RenameDialog.ShowAsync(
            owner,
            App.Text("Panel.RenameCollection"),
            collection.Name,
            newName => panelVM.RenameCollection(collection, newName));
    }

    private async void OnRenameRequestClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode { Collection: { } collection }) return;
        if ((sender as MenuItem)?.DataContext is not HttpRequestEntry entry) return;

        var panelView = this.FindAncestorOfType<CollectionPanel>();
        var panelVM = panelView?.DataContext as ViewModels.CollectionPanel;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (panelVM is null || owner is null) return;

        await RenameDialog.ShowAsync(
            owner,
            App.Text("Panel.RenameRequest"),
            entry.Name ?? string.Empty,
            newName => panelVM.RenameRequest(collection, entry, newName));
    }
}
