using System.Text.Json;
using Resty.Core.Models;
using Resty.Core.Parsing;

namespace Resty.Gui.Services;

/// <summary>表示一个已解析的工作区请求节点。</summary>
public sealed record RequestNode(string Name, string Method);

/// <summary>表示一个 .http 文件节点（含其子请求列表）。</summary>
public sealed record HttpFileNode(string FileName, string FilePath, IReadOnlyList<RequestNode> Requests);

/// <summary>扫描工作区目录，构建集合文件树。</summary>
public sealed class WorkspaceService : IDisposable
{
    public string WorkspaceName { get; private set; } = string.Empty;
    public string WorkspacePath { get; private set; } = string.Empty;
    public IReadOnlyList<HttpFileNode> Files { get; private set; } = [];

    // 缓存已解析的文件定义，用于 GetRequestDefinition
    private readonly Dictionary<string, HttpFileDefinition> _fileDefs = new();

    // 文件变化通知
    public event Action? FilesChanged;
    private FileSystemWatcher? _watcher;

    public void Dispose() => _watcher?.Dispose();

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

        // 启动文件监听
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(folderPath, "*.http")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        var debounceTimer = new System.Timers.Timer(600) { AutoReset = false };
        debounceTimer.Elapsed += (_, _) => FilesChanged?.Invoke();
        void OnChanged(object _, FileSystemEventArgs __) { debounceTimer.Stop(); debounceTimer.Start(); }
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += (s, e) => OnChanged(s, e);
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

    /// <summary>
    /// 将 <paramref name="def"/> 写回 <paramref name="filePath"/> 中对应的请求块。
    /// 若请求名称为空或 [未命名]，则替换文件中第一个请求块。
    /// </summary>
    public bool SaveRequest(string filePath, HttpRequestDefinition def)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            // 找到请求块起始行（### 行）
            int startIdx = -1;
            int endIdx   = lines.Count; // exclusive
            var targetName = string.IsNullOrWhiteSpace(def.Name) ? null : def.Name.Trim();

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].TrimStart();
                if (!line.StartsWith("###")) continue;
                if (startIdx < 0)
                {
                    // 若没有目标名称，取第一个 ###
                    if (targetName is null)
                    {
                        startIdx = i;
                        continue;
                    }
                    // 否则找名称匹配的 ###
                    var header = lines[i].TrimStart('#').Trim();
                    if (string.Equals(header, targetName, StringComparison.OrdinalIgnoreCase))
                        startIdx = i;
                }
                else
                {
                    // 找到下一个 ### 作为结束
                    endIdx = i;
                    break;
                }
            }

            if (startIdx < 0) return false;

            // 生成新的请求块内容
            var newBlock = new List<string>();
            var origLine = lines[startIdx]; // 保留原 ### 行
            newBlock.Add(origLine);
            // 方法 + URL
            newBlock.Add($"{def.Method.ToUpperInvariant()} {def.Url}");
            // Headers
            foreach (var kv in def.Headers)
                newBlock.Add($"{kv.Key}: {kv.Value}");
            // Body
            if (!string.IsNullOrWhiteSpace(def.Body))
            {
                newBlock.Add(string.Empty);
                newBlock.Add(def.Body);
            }
            // Assertions
            if (def.Assertions.Count > 0)
            {
                newBlock.Add(string.Empty);
                newBlock.Add("> {%");
                foreach (var a in def.Assertions)
                    newBlock.Add(a.RawText);
                newBlock.Add("%}");
            }
            newBlock.Add(string.Empty); // trailing blank

            // 替换原始行
            lines.RemoveRange(startIdx, endIdx - startIdx);
            lines.InsertRange(startIdx, newBlock);

            File.WriteAllLines(filePath, lines);

            // 刷新缓存
            var content = string.Join(Environment.NewLine, lines);
            _fileDefs[filePath] = HttpFileParser.ParseContent(content);
            return true;
        }
        catch { return false; }
    }

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
