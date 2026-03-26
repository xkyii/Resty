using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.ViewModels;

public class Popup : ObservableValidator
{
    public bool InProgress
    {
        get => _inProgress;
        set => SetProperty(ref _inProgress, value);
    }

    public string ProgressDescription
    {
        get => _progressDescription;
        set => SetProperty(ref _progressDescription, value);
    }

    public bool Check()
    {
        if (HasErrors)
            return false;
        ValidateAllProperties();
        return !HasErrors;
    }

    public virtual bool CanStartDirectly() => true;

    public virtual Task<bool> Sure() => Task.FromResult(false);

    private bool _inProgress = false;
    private string _progressDescription = string.Empty;
}