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

    public void Load(string folderPath)
    {
        WorkspacePath = folderPath;
        WorkspaceName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var nodes = new List<HttpFileNode>();
        foreach (var file in Directory.EnumerateFiles(folderPath, "*.http", SearchOption.AllDirectories)
                                      .OrderBy(f => f))
        {
            try
            {
                var content  = File.ReadAllText(file);
                var def      = HttpFileParser.ParseContent(content);
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
    }
}
