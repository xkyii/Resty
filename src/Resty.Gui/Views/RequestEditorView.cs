using System.Text;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Core.Models;
using Resty.Core.Parsing;

namespace Resty.Gui.Views;

/// <summary>
/// G2 请求编辑区：URL 工具栏 + 原始 HTTP 文本编辑器。
/// 发送时从文本编辑器重新解析请求定义。
/// </summary>
public sealed class RequestEditorView
{
    // ── 颜色 ────────────────────────────────────────────────────
    private static readonly Color BgBase    = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgPanel   = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

    // ── 控件 ────────────────────────────────────────────────────
    private readonly DockPanel         _root;
    private readonly TextBlock         _methodLabel;
    private readonly TextBox           _urlDisplay;
    private readonly Button            _sendBtn;
    private readonly MultiLineTextBox  _textEditor;
    private readonly Border            _emptyOverlay;

    // ── 公共接口 ─────────────────────────────────────────────────
    /// <summary>根元素，放入父布局。</summary>
    public UIElement RootElement => _root;

    /// <summary>用户点击「发送」时触发，参数为从文本编辑器解析出的请求定义。</summary>
    public Action<HttpRequestDefinition>? SendRequested;

    public RequestEditorView()
    {
        // ── 方法徽章 ─────────────────────────────────────────────
        _methodLabel = new TextBlock
        {
            Text      = "GET",
            FontSize  = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = MethodColor("GET"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(12, 0, 8, 0),
        };

        // ── URL 显示（只读） ─────────────────────────────────────
        _urlDisplay = new TextBox
        {
            FontSize  = 13,
            Foreground = TextPri,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _urlDisplay.Placeholder("输入 URL 后将显示在此…");
        _urlDisplay.IsReadOnly(true);

        // ── 发送按钮 ─────────────────────────────────────────────
        _sendBtn = new Button { Width = 80, Height = 36 };
        _sendBtn.Content("▶ 发送", false)
            .FontSize(13)
            .Background(Accent)
            .Foreground(Color.White)
            .OnClick(OnSendClicked);

        // ── URL 工具栏 ───────────────────────────────────────────
        var toolbar = new DockPanel();
        toolbar.Add(_methodLabel.DockLeft());
        toolbar.Add(new Border { Width = 8 }.DockRight());      // 右侧留白
        toolbar.Add(_sendBtn.DockRight());
        toolbar.Add(new Border { Width = 8 }.DockLeft());       // 方法与URL 间距占位
        toolbar.Add(_urlDisplay);

        var toolbarBorder = new Border
        {
            Height      = 44,
            Background  = BgPanel,
            BorderBrush = BorderCol,
            Child       = toolbar,
        };

        // ── 文本编辑器 ───────────────────────────────────────────
        _textEditor = new MultiLineTextBox
        {
            FontSize   = 13,
            Foreground = TextPri,
            Background = BgBase,
            Padding    = new Thickness(12, 8, 12, 8),
        };
        _textEditor.Wrap(false);

        // ── 空状态覆盖层 ─────────────────────────────────────────
        _emptyOverlay = new Border
        {
            Background = BgBase,
            Child = new TextBlock
            {
                Text      = "← 从侧边栏点击一个请求开始编辑",
                FontSize  = 13,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        // ── 根布局 ───────────────────────────────────────────────
        _root = new DockPanel();
        _root.Add(toolbarBorder.DockTop());
        _root.Add(_emptyOverlay);  // 初始显示空状态
    }

    /// <summary>加载请求到编辑器。</summary>
    public void Load(HttpRequestDefinition req)
    {
        _urlDisplay.Text      = req.Url;
        _methodLabel.Text     = req.Method;
        _methodLabel.Foreground = MethodColor(req.Method);
        _textEditor.Text      = BuildRawText(req);

        // 切换到文本编辑器（移除空状态）
        _root.Remove(_emptyOverlay);
        // 确保文本编辑器已加入（首次加载时添加）
        _root.Remove(_textEditor);
        _root.Add(_textEditor);
    }

    // ── 私有方法 ─────────────────────────────────────────────────

    private void OnSendClicked()
    {
        var raw = _textEditor.Text;
        if (string.IsNullOrWhiteSpace(raw)) return;

        try
        {
            var fileDef = HttpFileParser.ParseContent(raw);
            var req = fileDef.Requests.Count > 0 ? fileDef.Requests[0] : null;
            if (req is null) return;
            SendRequested?.Invoke(req);
        }
        catch
        {
            // G2 阶段不处理解析错误
        }
    }

    private static string BuildRawText(HttpRequestDefinition req)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(req.Name))
            sb.Append("### ").AppendLine(req.Name);
        sb.Append(req.Method).Append(' ').AppendLine(req.Url);
        foreach (var (k, v) in req.Headers)
            sb.Append(k).Append(": ").AppendLine(v);
        if (req.Body is not null)
        {
            sb.AppendLine();
            sb.Append(req.Body);
        }
        return sb.ToString();
    }

    private static Color MethodColor(string method) => method.ToUpperInvariant() switch
    {
        "GET"    => Color.FromRgb(0x61, 0xAF, 0xEF),
        "POST"   => Color.FromRgb(0x98, 0xC3, 0x79),
        "PUT"    => Color.FromRgb(0xE5, 0xC0, 0x7B),
        "PATCH"  => Color.FromRgb(0xD1, 0x9A, 0x66),
        "DELETE" => Color.FromRgb(0xE0, 0x6C, 0x75),
        _        => Color.FromRgb(0xCC, 0xCC, 0xCC),
    };
}
