using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.Views
{
    public partial class MainWindow : ChromelessWindow
    {
        public static readonly IRelayCommand QuitCommand = new RelayCommand(() =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(0);
        });

        public MainWindow()
        {
            InitializeComponent();
        }

        public void MinimizeWindow(object? sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        public void MaximizeWindow(object? sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        public void CloseWindow(object? sender, RoutedEventArgs e) =>
            Close();
    }
}
