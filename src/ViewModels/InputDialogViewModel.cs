using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.ViewModels;

public partial class InputDialogViewModel : Popup
{
    [ObservableProperty]
    private string _inputValue = string.Empty;

    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    private string _watermark = string.Empty;

    private readonly Func<string, bool> _submitAction;

    /// <param name="defaultValue">The default value shown in the input box.</param>
    /// <param name="prompt">Label text shown above the input field.</param>
    /// <param name="submitAction">Called with the new trimmed value; return false to show an error.</param>
    public InputDialogViewModel(string defaultValue, string prompt, Func<string, bool> submitAction)
    {
        _submitAction = submitAction;
        InputValue = defaultValue;
        Prompt = prompt;
        Watermark = prompt;
    }

    public override bool Check()
    {
        if (!string.IsNullOrWhiteSpace(InputValue))
        {
            ProgressDescription = string.Empty;
            return true;
        }

        ProgressDescription = "Input cannot be empty";
        return false;
    }

    public override async Task<bool> Sure()
    {
        if (!Check())
            return false;

        InProgress = true;
        ProgressDescription = "";

        try
        {
            await Task.Run(() =>
            {
                if (!_submitAction(InputValue.Trim()))
                    throw new Exception("Operation failed");
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
