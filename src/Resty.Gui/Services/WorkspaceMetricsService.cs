using Resty.Core.Parsing;

namespace Resty.Gui.Services;

public sealed record WorkspaceMetrics(bool Exists, int HttpFileCount, int RequestCount, string? Error = null);

/// <summary>统计任意目录的工作区指标（*.http 文件数、请求总数）。</summary>
public static class WorkspaceMetricsService
{
    public static WorkspaceMetrics Collect(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return new WorkspaceMetrics(false, 0, 0);

        try
        {
            var files = Directory.EnumerateFiles(path, "*.http", SearchOption.AllDirectories).ToList();
            var fileCount = files.Count;
            var requestCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var def = HttpFileParser.ParseContent(content);
                    requestCount += def.Requests.Count;
                }
                catch
                {
                    // 单文件解析失败不影响整体统计
                }
            }

            return new WorkspaceMetrics(true, fileCount, requestCount);
        }
        catch (Exception ex)
        {
            return new WorkspaceMetrics(true, 0, 0, ex.Message);
        }
    }
}
