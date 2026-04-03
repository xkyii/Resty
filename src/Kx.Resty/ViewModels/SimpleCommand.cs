using System;
using System.Windows.Input;
using Avalonia.Threading;

namespace Kx.Resty.ViewModels;

public sealed class SimpleCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public SimpleCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged()
    {
        void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        if (Dispatcher.UIThread.CheckAccess())
            Raise();
        else
            Dispatcher.UIThread.Post(Raise);
    }
}
