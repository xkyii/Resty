using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.ViewModels;

public partial class RenameDialogViewModel : Popup
{
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [ObservableProperty]
    private string _name = string.Empty;

    private readonly Func<string, bool> _renameAction;

    /// <param name="currentName">The existing name shown in the input box.</param>
    /// <param name="renameAction">Called with the new trimmed name; return false to show an error.</param>
    public RenameDialogViewModel(string currentName, Func<string, bool> renameAction)
    {
        _renameAction = renameAction;
        Name = currentName;
    }

    public override async Task<bool> Sure()
    {
        if (!Check())
            return false;

        InProgress = true;
        ProgressDescription = App.Text("Panel.Renaming");

        try
        {
            await Task.Run(() =>
            {
                if (!_renameAction(Name.Trim()))
                    throw new Exception(App.Text("Panel.RenameFailedDuplicate"));
            });

            return true;
        }
        catch (Exception ex)
        {
            ProgressDescription = ex.Message;
            return false;
        }
        finally
        {
            InProgress = false;
        }
    }
}
