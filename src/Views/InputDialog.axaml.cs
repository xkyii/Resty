using Avalonia;
using Avalonia.Controls;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Views;

public partial class InputDialog : UserControl
{
    public InputDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows an input dialog for collecting user input.
    /// </summary>
    /// <param name="owner">Parent window.</param>
    /// <param name="title">Window title.</param>
    /// <param name="defaultValue">Default pre-filled value.</param>
    /// <param name="watermark">Placeholder text for the input field.</param>
    /// <param name="submitAction">Called with the trimmed input value; return false to keep dialog open with an error.</param>
    /// <returns>true if the action succeeded, false if cancelled or failed.</returns>
    public static async Task<bool> ShowAsync(
        Window owner,
        string title,
        string defaultValue,
        Func<string, bool> submitAction,
        string? watermark = null)
    {
        var vm = new InputDialogViewModel(defaultValue, watermark ?? title, submitAction);
        var dialog = new Window
        {
            Width = 380,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = title,
            Content = new InputDialog { DataContext = vm }
        };

        var inputView = dialog.Content as InputDialog;
        var okButton = inputView?.FindControl<Button>("OkButton");
        var cancelButton = inputView?.FindControl<Button>("CancelButton");
        var inputField = inputView?.FindControl<TextBox>("InputField");

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
            inputField?.Focus();
            inputField?.SelectAll();
        };

        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}
