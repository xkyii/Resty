using System.Text.Json;
using Resty.Gui.Models;

namespace Resty.Gui.Services;

/// <summary>
/// 历史记录存储服务。
///
/// 目录结构：
///   .resty/history/
///     index.json          — 摘要索引（HistorySummary 数组）
///     2026-05-07/
///       103045-123_Get-User.hlog
///       103102-456_Create-User.hlog
/// </summary>
public sealed class HistoryService
{
    private string _historyDir = string.Empty;
    private readonly List<HistorySummary> _summaries = [];
    private const int MaxEntries = 200;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IReadOnlyList<HistorySummary> Summaries => _summaries;

    // ── 初始化 ───────────────────────────────────────────────────
    public void SetWorkspacePath(string workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath)) return;
        _historyDir = Path.Combine(workspacePath, ".resty", "history");
        Directory.CreateDirectory(_historyDir);
        _summaries.Clear();
        LoadIndex();
    }

    // ── 写入 ─────────────────────────────────────────────────────
    public void AddRecord(HistoryRecord record)
    {
        var id   = record.Summary.Id;
        var file = HlogPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        try { File.WriteAllText(file, HlogSerializer.Serialize(record)); }
        catch { return; }

        _summaries.Insert(0, record.Summary);
        TrimOldEntries();
        SaveIndex();
    }

    // ── 按需加载完整记录 ─────────────────────────────────────────
    public HistoryRecord? LoadRecord(string id)
    {
        var file = HlogPath(id);
        if (!File.Exists(file)) return null;
        try
        {
            var content = File.ReadAllText(file);
            return HlogSerializer.Deserialize(id, content);
        }
        catch { return null; }
    }

    // ── 清除全部 ─────────────────────────────────────────────────
    public void Clear()
    {
        foreach (var s in _summaries)
        {
            try { File.Delete(HlogPath(s.Id)); } catch { }
        }
        // 删除空的日期子目录
        try
        {
            foreach (var dir in Directory.GetDirectories(_historyDir))
                if (Directory.GetFiles(dir).Length == 0)
                    Directory.Delete(dir);
        }
        catch { }

        _summaries.Clear();
        SaveIndex();
    }

    // ── ID 生成 ──────────────────────────────────────────────────
    /// <summary>生成唯一 ID，格式：yyyy-MM-dd/HHmmss-fff_SafeName</summary>
    public static string NewId(DateTime ts, string requestName)
    {
        var date     = ts.ToString("yyyy-MM-dd");
        var time     = ts.ToString("HHmmss-fff");
        var safeName = MakeSafe(requestName, 32);
        return $"{date}/{time}_{safeName}";
    }

    // ── 私有 ─────────────────────────────────────────────────────
    private string HlogPath(string id) =>
        Path.Combine(_historyDir, id + ".hlog");

    private string IndexPath() =>
        Path.Combine(_historyDir, "index.json");

    private void LoadIndex()
    {
        var path = IndexPath();
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<HistorySummary>>(json, JsonOpts);
            if (list is not null) _summaries.AddRange(list);
        }
        catch { }
    }

    private void SaveIndex()
    {
        if (string.IsNullOrEmpty(_historyDir)) return;
        try
        {
            var json = JsonSerializer.Serialize(_summaries, JsonOpts);
            File.WriteAllText(IndexPath(), json);
        }
        catch { }
    }

    private void TrimOldEntries()
    {
        while (_summaries.Count > MaxEntries)
        {
            var old = _summaries[^1];
            _summaries.RemoveAt(_summaries.Count - 1);
            try { File.Delete(HlogPath(old.Id)); } catch { }
        }
    }

    private static string MakeSafe(string name, int maxLen)
    {
        var safe = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_') safe.Append(c);
            else if (c == ' ')                                    safe.Append('-');
        }
        var s = safe.ToString();
        return s.Length > maxLen ? s[..maxLen] : (s.Length == 0 ? "request" : s);
    }
}
