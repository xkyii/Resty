using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using Kx.Resty.Models;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Commands;

/// <summary>
/// Scans a workspace directory for .http files and env files,
/// populates a <see cref="CollectionPanel"/>, and watches for changes.
/// </summary>
public sealed class WorkspaceScanner : IDisposable
{
    private readonly string          _rootPath;
    private readonly CollectionPanel _panel;
    private FileSystemWatcher?       _watcher;
    private Timer?                   _debounce;

    public WorkspaceScanner(string rootPath, CollectionPanel panel)
    {
        _rootPath = rootPath;
        _panel    = panel;
    }

    public void Start()
    {
        ScanNow();
        SetupWatcher();
    }

    // ─── Scanning ─────────────────────────────────────────────────────────────

    private void ScanNow()
    {
        var nodes = new List<CollectionTreeNode>();
        ScanDirectory(_rootPath, nodes);

        var envSets = LoadEnvironments();

        Dispatcher.UIThread.Post(() =>
        {
            _panel.RootNodes.Clear();
            foreach (var n in nodes) _panel.RootNodes.Add(n);

            _panel.Environments.Clear();
            foreach (var e in envSets) _panel.Environments.Add(e);

            if (_panel.ActiveEnvironment == null && _panel.Environments.Count > 0)
                _panel.SelectEnvironment(_panel.Environments[0]);
        });
    }

    private void ScanDirectory(string path, List<CollectionTreeNode> target)
    {
        if (!Directory.Exists(path)) return;

        // .http files at this level.
        foreach (var file in Directory.GetFiles(path, "*.http").OrderBy(f => f))
        {
            var col = HttpFileParser.Parse(file);
            target.Add(new CollectionTreeNode
            {
                Name       = col.Name,
                IsDirectory = false,
                Collection  = col
            });
        }

        // Subdirectories (skip hidden / dot-dirs).
        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.StartsWith('.')) continue;

            var children = new List<CollectionTreeNode>();
            ScanDirectory(dir, children);
            if (children.Count == 0) continue;

            var folder = new CollectionTreeNode { Name = dirName, IsDirectory = true };
            foreach (var c in children) folder.Children.Add(c);
            target.Add(folder);
        }
    }

    // ─── Environment loading ──────────────────────────────────────────────────

    private List<EnvironmentSet> LoadEnvironments()
    {
        var pub     = LoadEnvFile(Path.Combine(_rootPath, "http-client.env.json"));
        var priv    = LoadEnvFile(Path.Combine(_rootPath, "http-client.private.env.json"));
        var allKeys = pub.Keys.Union(priv.Keys).OrderBy(k => k);
        var result  = new List<EnvironmentSet>();

        foreach (var envName in allKeys)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (pub.TryGetValue(envName,  out var pubVars))  foreach (var kv in pubVars)  merged[kv.Key] = kv.Value;
            if (priv.TryGetValue(envName, out var privVars)) foreach (var kv in privVars) merged[kv.Key] = kv.Value;

            var set = new EnvironmentSet { Name = envName };
            foreach (var kv in merged.OrderBy(kv => kv.Key))
                set.Variables.Add(new EnvironmentVariable { Name = kv.Key, Value = kv.Value });
            result.Add(set);
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadEnvFile(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var result    = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var env in doc.RootElement.EnumerateObject())
            {
                var vars = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var v in env.Value.EnumerateObject())
                    vars[v.Name] = v.Value.ToString();
                result[env.Name] = vars;
            }
            return result;
        }
        catch { return []; }
    }

    // ─── File watching ────────────────────────────────────────────────────────

    private void SetupWatcher()
    {
        if (!Directory.Exists(_rootPath)) return;
        _watcher = new FileSystemWatcher(_rootPath)
        {
            IncludeSubdirectories = true,
            Filter                = "*",
            NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            EnableRaisingEvents   = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += (_, _) => ScheduleRescan();
    }

    private void OnChanged(object _, FileSystemEventArgs e)
    {
        var name = e.Name ?? string.Empty;
        if (name.EndsWith(".http", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("env.json", StringComparison.OrdinalIgnoreCase))
            ScheduleRescan();
    }

    private void ScheduleRescan()
    {
        _debounce?.Dispose();
        _debounce = new Timer(_ => ScanNow(), null, 400, Timeout.Infinite);
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
