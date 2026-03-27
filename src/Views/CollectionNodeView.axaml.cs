using Avalonia.Controls;
using Avalonia.Input;
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
    private void OnRequestEntryTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not CollectionTreeNode { Collection: { } collection }) return;
        if ((sender as Border)?.DataContext is not HttpRequestEntry entry) return;

        var panelView = this.FindAncestorOfType<CollectionPanel>();
        (panelView?.DataContext as ViewModels.CollectionPanel)?.OpenRequest(entry, collection);
    }
}
