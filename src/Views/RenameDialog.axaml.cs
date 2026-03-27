using Avalonia;
using Avalonia.Controls;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Views;

public partial class RenameDialog : UserControl
{
    public RenameDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows a rename input dialog.
    /// </summary>
    /// <param name="owner">Parent window.</param>
    /// <param name="title">Window title (e.g. "重命名").</param>
    /// <param name="currentName">Pre-filled current name.</param>
    /// <param name="renameAction">Called with the trimmed new name; return false to keep the dialog open with an error.</param>
    /// <returns>true if the action succeeded, false if cancelled or failed.</returns>
    public static async Task<bool> ShowAsync(
        Window owner,
        string title,
        string currentName,
        Func<string, bool> renameAction)
    {
        var vm = new RenameDialogViewModel(currentName, renameAction);
        var dialog = new Window
        {
            Width = 380,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = title,
            Content = new RenameDialog { DataContext = vm }
        };

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
                    dialog.Close(true);
                else
                    okButton.IsEnabled = true;
            };
        }

        if (cancelButton != null)
            cancelButton.Click += (_, _) => dialog.Close(false);

        // Auto-focus and select-all
        dialog.Opened += (_, _) =>
        {
            nameInput?.Focus();
            nameInput?.SelectAll();
        };

        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}
