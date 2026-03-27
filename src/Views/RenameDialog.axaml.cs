using Avalonia;
using Avalonia.Controls;
using Kx.Resty.Models;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Views;

public partial class RenameDialog : UserControl
{
    public RenameDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the rename dialog for a collection.
    /// </summary>
    /// <param name="owner">Parent window</param>
    /// <param name="collection">Collection to rename</param>
    /// <param name="renameAction">Action to perform the rename (returns true on success)</param>
    /// <returns>true if rename was successful, false if cancelled or failed</returns>
    public static async Task<bool> ShowAsync(
        Window owner,
        HttpCollection collection,
        Func<HttpCollection, string, bool> renameAction)
    {
        var vm = new RenameDialogViewModel(collection, renameAction);
        var dialog = new Window
        {
            Width = 380,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = App.Text("Panel.RenameCollection"),
            Content = new RenameDialog { DataContext = vm }
        };

        // Get button references and wire up event handlers
        var renameView = dialog.Content as RenameDialog;
        var okButton = renameView?.FindControl<Button>("OkButton");
        var cancelButton = renameView?.FindControl<Button>("CancelButton");
        var nameInput = renameView?.FindControl<TextBox>("NameInput");

        if (okButton != null)
        {
            okButton.Click += async (_, _) =>
            {
                if (!vm.Check())
                    return;

                okButton.IsEnabled = false;
                var success = await vm.Sure();
                if (success)
                {
                    dialog.Close(true);
                }
                else
                {
                    okButton.IsEnabled = true;
                }
            };
        }

        if (cancelButton != null)
        {
            cancelButton.Click += (_, _) => dialog.Close(false);
        }

        // Auto-focus name input and select all text
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            nameInput?.Focus();
            nameInput?.SelectAll();
        });

        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}
