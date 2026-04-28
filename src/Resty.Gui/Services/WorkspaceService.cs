using System.Text.Json;
using Resty.Core.Models;
using Resty.Core.Parsing;

namespace Resty.Gui.Services;

/// <summary>表示一个已解析的工作区请求节点。</summary>
public sealed record RequestNode(string Name, string Method);

/// <summary>表示一个 .http 文件节点（含其子请求列表）。</summary>
public sealed record HttpFileNode(string FileName, string FilePath, IReadOnlyList<RequestNode> Requests);

/// <summary>扫描工作区目录，构建集合文件树。</summary>
public sealed class WorkspaceService
{
    public string WorkspaceName { get; private set; } = string.Empty;
    public string WorkspacePath { get; private set; } = string.Empty;
    public IReadOnlyList<HttpFileNode> Files { get; private set; } = [];

    // 缓存已解析的文件定义，用于 GetRequestDefinition
    private readonly Dictionary<string, HttpFileDefinition> _fileDefs = new();

    public void Load(string folderPath)
    {
        WorkspacePath = folderPath;
        WorkspaceName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var nodes = new List<HttpFileNode>();
        _fileDefs.Clear();

        foreach (var file in Directory.EnumerateFiles(folderPath, "*.http", SearchOption.AllDirectories)
                                      .OrderBy(f => f))
        {
            try
            {
                var content  = File.ReadAllText(file);
                var def      = HttpFileParser.ParseContent(content);
                _fileDefs[file] = def;

                var requests = def.Requests
                    .Select(r => new RequestNode(
                        string.IsNullOrWhiteSpace(r.Name) ? "[未命名]" : r.Name,
                        r.Method.ToUpperInvariant()))
                    .ToList();

                nodes.Add(new HttpFileNode(Path.GetFileName(file), file, requests));
            }
            catch
            {
                nodes.Add(new HttpFileNode(Path.GetFileName(file), file, []));
            }
        }

        Files = nodes;
        ScanEnvironments();
    }

    /// <summary>
    /// 根据文件路径和请求名称返回完整的 <see cref="HttpRequestDefinition"/>。
    /// 若未找到则返回 null。
    /// </summary>
    public HttpRequestDefinition? GetRequestDefinition(string filePath, string requestName)
    {
        if (!_fileDefs.TryGetValue(filePath, out var def)) return null;
        return def.Requests.FirstOrDefault(r =>
            (string.IsNullOrWhiteSpace(r.Name) ? "[未命名]" : r.Name) == requestName);
    }

    public IReadOnlyList<string> AvailableEnvironments { get; private set; } = [];

    private void ScanEnvironments()
    {
        if (string.IsNullOrEmpty(WorkspacePath)) { AvailableEnvironments = []; return; }
        var envFile = Path.Combine(WorkspacePath, "http-client.env.json");
        if (!File.Exists(envFile)) { AvailableEnvironments = []; return; }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(envFile));
            var names = new List<string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                names.Add(prop.Name);
            AvailableEnvironments = names;
        }
        catch { AvailableEnvironments = []; }
    }
}
