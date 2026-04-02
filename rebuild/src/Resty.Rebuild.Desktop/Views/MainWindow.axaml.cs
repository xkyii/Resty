using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Resty.Rebuild.Desktop.ViewModels;

namespace Resty.Rebuild.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ShouldIgnoreTitleBarGesture(e.Source as StyledElement))
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void TitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ShouldIgnoreTitleBarGesture(e.Source as StyledElement))
            return;

        ToggleMaximizeWindow(sender, new RoutedEventArgs());
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DirectoryMenuDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (!vm.IsDirectoryManagerMode)
            return;

        if (!vm.DirectoryManager.HasSelection)
            return;

        vm.DirectoryManager.OpenSelectedInWorkspace();
    }

    private async void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // 仅在目录管理模式下响应
        if (!vm.IsDirectoryManagerMode)
            return;

        if (!StorageProvider.CanPickFolder)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var path = folders[0].Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        // 将选中路径交给 DirectoryManager 处理（会校验、加入最近并触发打开）
        await vm.DirectoryManager.OpenPathAsync(path);
    }

    private async void OpenSettingsDialog(object? sender, RoutedEventArgs e)
    {
        var app = Avalonia.Application.Current;
        if (app is null)
            return;

        var currentTheme = app.RequestedThemeVariant;
        var currentThemeText = currentTheme == ThemeVariant.Light
            ? "浅色"
            : currentTheme == ThemeVariant.Dark
                ? "深色"
                : "跟随系统";

        var themeCombo = new ComboBox
        {
            ItemsSource = new[] { "浅色", "深色", "跟随系统" },
            SelectedItem = currentThemeText
        };

        var languageCombo = new ComboBox
        {
            ItemsSource = new[] { "英语", "简体中文" },
            SelectedItem = "简体中文"
        };

        var okButton = new Button { Content = "确定", Width = 88 };
        var cancelButton = new Button { Content = "取消", Width = 88 };

        var themeLabel = new TextBlock { Text = "主题", FontWeight = Avalonia.Media.FontWeight.SemiBold };
        var languageLabel = new TextBlock { Text = "语言", FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 20, 0, 0) };
        var actionRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, okButton }
        };

        var contentGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto")
        };
        contentGrid.Children.Add(themeLabel);
        contentGrid.Children.Add(themeCombo);
        contentGrid.Children.Add(languageLabel);
        contentGrid.Children.Add(languageCombo);
        contentGrid.Children.Add(actionRow);

        Grid.SetRow(themeLabel, 0);
        Grid.SetRow(themeCombo, 1);
        Grid.SetRow(languageLabel, 2);
        Grid.SetRow(languageCombo, 3);
        Grid.SetRow(actionRow, 5);

        var dialog = new Window
        {
            Title = "偏好设置",
            Width = 420,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = contentGrid
        };

        cancelButton.Click += (_, _) => dialog.Close();
        okButton.Click += (_, _) =>
        {
            try
            {
                var selectedTheme = themeCombo.SelectedItem as string ?? "跟随系统";
                app.RequestedThemeVariant = selectedTheme switch
                {
                    "浅色" => ThemeVariant.Light,
                    "深色" => ThemeVariant.Dark,
                    _ => ThemeVariant.Default
                };

                var selectedLang = languageCombo.SelectedItem as string ?? "简体中文";
                ApplyLocale(selectedLang == "英语" ? "en-US" : "zh-CN");
            }
            catch
            {
                // 设置应用失败时，避免对话框点击“确定”导致崩溃。
            }

            dialog.Close();
        };

        await dialog.ShowDialog(this);
    }

    private async void OpenAboutDialog(object? sender, RoutedEventArgs e)
    {
        var closeButton = new Button { Content = "关闭", Width = 88, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

        var title = new TextBlock { Text = "Resty.Rebuild", FontSize = 22, FontWeight = Avalonia.Media.FontWeight.Bold };
        var desc = new TextBlock { Text = "HTTP API 调试工具（重构版）", Margin = new Thickness(0, 10, 0, 0) };
        var tech = new TextBlock { Text = "基于 Avalonia + Semi + Ursa", Margin = new Thickness(0, 8, 0, 0) };
        var platform = new TextBlock { Text = $"运行平台: {Environment.OSVersion}", Margin = new Thickness(0, 8, 0, 0) };

        var contentGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto")
        };
        contentGrid.Children.Add(title);
        contentGrid.Children.Add(desc);
        contentGrid.Children.Add(tech);
        contentGrid.Children.Add(platform);
        contentGrid.Children.Add(closeButton);

        Grid.SetRow(title, 0);
        Grid.SetRow(desc, 1);
        Grid.SetRow(tech, 2);
        Grid.SetRow(platform, 3);
        Grid.SetRow(closeButton, 4);

        var dialog = new Window
        {
            Title = "关于",
            Width = 460,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = contentGrid
        };
        closeButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private static bool ShouldIgnoreTitleBarGesture(StyledElement? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Button
                || current is Menu
                || current is MenuItem
                || current is ToggleButton
                || current is TextBox
                || current is ComboBox)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static void ApplyLocale(string locale)
    {
        var app = Avalonia.Application.Current;
        if (app is null)
            return;

        // Semi/Ursa 主题对象都可能包含 Locale 属性，但属性类型不一定一致。
        // 仅在可安全赋值时写入，避免类型不匹配引发崩溃。
        foreach (var style in app.Styles)
        {
            try
            {
                var localeProp = style.GetType().GetProperty("Locale");
                if (localeProp?.CanWrite != true)
                    continue;

                var propType = localeProp.PropertyType;
                if (propType == typeof(string))
                {
                    localeProp.SetValue(style, locale);
                    continue;
                }

                if (propType.IsEnum)
                {
                    // 尝试按名称匹配枚举值。
                    var enumName = locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                        ? "zhCN"
                        : locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                            ? "enUS"
                            : null;

                    if (enumName is not null)
                    {
                        var names = Enum.GetNames(propType);
                        var matched = names.FirstOrDefault(n => string.Equals(n, enumName, StringComparison.OrdinalIgnoreCase));
                        if (matched is not null)
                        {
                            var enumValue = Enum.Parse(propType, matched, ignoreCase: true);
                            localeProp.SetValue(style, enumValue);
                        }
                    }
                }
            }
            catch
            {
                // 忽略单个样式的 locale 设置异常，确保整体不崩溃。
            }
        }
    }
}