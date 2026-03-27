using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public partial class RenameDialogViewModel : Popup
{
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [ObservableProperty]
    private string _name = string.Empty;

    private readonly HttpCollection _collection;
    private readonly Func<HttpCollection, string, bool> _renameAction;

    public RenameDialogViewModel(HttpCollection collection, Func<HttpCollection, string, bool> renameAction)
    {
        _collection = collection;
        _renameAction = renameAction;
        Name = collection.Name;
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
                var trimmedName = Name.Trim();
                if (!_renameAction(_collection, trimmedName))
                {
                    throw new Exception(App.Text("Panel.RenameFailedDuplicate"));
                }
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
