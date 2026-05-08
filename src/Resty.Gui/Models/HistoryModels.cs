namespace Resty.Gui.Models;

/// <summary>历史记录摘要，存于 index.json，用于左侧列表快速加载。</summary>
public sealed record HistorySummary(
    string   Id,            // "2026-05-07/103045-123_Get-User"（文件相对路径，不含扩展名）
    string   RequestName,
    string   Method,
    string   Url,
    int      StatusCode,
    long     ElapsedMs,
    DateTime Timestamp,
    string   FilePath,      // 源 .http 文件绝对路径
    string?  Error = null);

/// <summary>
/// 完整历史记录，对应一个 .hlog 文件。
/// 各区块内容为原始文本，与 .hlog 格式中 @@ 分隔符后的内容一一对应。
/// </summary>
public sealed record HistoryRecord(
    HistorySummary Summary,
    string         RequestSection,    // @@request 区块：原始 HTTP 请求文本
    string         ResponseSection,   // @@response 区块：原始 HTTP 响应文本
    string?        AssertionsSection  // @@assertions 区块：断言结果文本，无断言时为 null
);
