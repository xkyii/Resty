using System;
using System.Collections.Generic;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace Resty.Gui.Views;

/// <summary>
/// 实验面板 — 用于临时试验新功能/组件。
/// 左侧为试验列表（入口），右侧由 MainWindow 显示具体试验内容。
/// </summary>
public sealed class LabView
{
    private static readonly Color BgSidebar = Color.FromRgb(0x25, 0x25, 0x26);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);

    public UIElement RootElement { get; }
    public event Action<UIElement>? ExperimentRequested;

    public LabView()
    {
        var title = new TextBlock
        {
            Text       = "实验室",
            FontSize   = 11,
            FontWeight = FontWeight.Bold,
            Foreground = TextSec,
            Margin     = new Thickness(12, 10, 0, 6),
        };

        var placeholder = new TextBlock
        {
            Text                = "此区域用于试验新功能",
            FontSize            = 12,
            Foreground          = TextSec,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = new Thickness(0, 24, 0, 0),
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 8,
            Margin      = new Thickness(8),
        };
        panel.Add(title);

        // 实验列表（左侧） — 目前只放一个入口：TabControl 试验
        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };

        var tabCtrlBtn = new Button { Height = 32 };
        tabCtrlBtn.Content("TabControl 试验", false).FontSize(12).Background(Color.Transparent).Foreground(TextPri)
            .OnClick(() =>
            {
                var experiment = BuildTabControlExperiment();
                ExperimentRequested?.Invoke(experiment);
            });

        list.Add(tabCtrlBtn);
        panel.Add(list);

        panel.Add(placeholder);

        RootElement = new Border
        {
            Background = BgSidebar,
            Child      = panel,
        };
    }

    private UIElement BuildTabControlExperiment()
    {
        var tabControl = new TabControl();

        UIElement MakeTabContent(string text) => new Border { Background = BgSidebar, Child = new TextBlock { Text = text, Foreground = TextPri, Margin = new Thickness(8) } };

        var closeBtns = new List<Button>();

        // RebuildHeaders() 在每次 AddTab/RemoveTabAt 后都会创建全新的 TabHeaderButton 实例，
        // 因此每次结构变更后都需要重新绑定悬停事件。
        void BindTabHeaders()
        {
            var i = 0;
            VisualTree.Visit(tabControl, el =>
            {
                if (el.GetType().Name == "TabHeaderButton" && el is UIElement thb)
                {
                    if (i >= closeBtns.Count) return;
                    var btn = closeBtns[i++];
                    thb.MouseEnter += () => btn.Foreground(TextSec);
                    thb.MouseLeave += () => btn.Foreground(Color.Transparent);
                }
            });
        }

        void AddTab(string title, UIElement content)
        {
            TabItem item  = null!;
            Button closeBtn = null!;
            new TabItem().Ref(out item)
                .Header(
                    new StackPanel()
                        .Horizontal()
                        .CenterVertical()
                        .Spacing(6)
                        .Children(
                            new Label()
                                .Text(title)
                                .FontSize(12),
                            new Button()
                                .Ref(out closeBtn)
                                .Content(new GlyphElement { Kind = GlyphKind.Cross, GlyphSize = 3.5, IsHitTestVisible = false })
                                .MinHeight(0)
                                .Size(16, 16)
                                .Padding(new Thickness(0))
                                .CenterVertical()
                                .BorderThickness(0)
                                .Background(Color.Transparent)
                                .Foreground(Color.Transparent)   // 初始隐藏
                                .OnClick(() =>
                                {
                                    var idx = -1;
                                    for (int i = 0; i < tabControl.Tabs.Count; i++)
                                    {
                                        if (ReferenceEquals(tabControl.Tabs[i], item)) { idx = i; break; }
                                    }
                                    if (idx >= 0)
                                    {
                                        closeBtns.RemoveAt(idx);
                                        tabControl.RemoveTabAt(idx);
                                        // RebuildHeaders 生成了全新实例，重新绑定悬停事件
                                        BindTabHeaders();
                                    }
                                })
                        ))
                .Content(content as Element);

            closeBtns.Add(closeBtn);
            tabControl.AddTab(item);
        }

        AddTab("⚙ 工具 [3]", MakeTabContent("工具面板示例"));
        AddTab("📡 网络", MakeTabContent("网络试验内容"));
        AddTab("🧪 测试 [beta]", MakeTabContent("测试用例输出"));

        BindTabHeaders();

        return new Border { Background = Color.FromRgb(0x1E, 0x1E, 0x1E), Child = tabControl };
    }
}
