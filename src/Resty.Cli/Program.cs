using Resty.Core.Assertions;
using Resty.Core.Environment;
using Resty.Core.Execution;
using Resty.Core.Models;
using Resty.Core.Parsing;
using Resty.Core.Reporting;

namespace Resty.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        if (command is not ("run" or "test"))
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'. Use 'run' or 'test'.");
            return 3;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Missing target. Usage: resty run|test <file|dir> [options]");
            return 3;
        }

        // ---- Parse options ----
        var opts = ParseOptions(args[2..]);
        var target = args[1];
        var isTest = command == "test";

        // ---- Resolve .http files ----
        List<string> httpFiles;
        try
        {
            httpFiles = ResolveHttpFiles(target, out var requestFragment);
            opts.RequestFilter ??= requestFragment;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error resolving target: {ex.Message}");
            return 3;
        }

        if (httpFiles.Count == 0)
        {
            Console.Error.WriteLine($"No .http files found at '{target}'.");
            return 3;
        }

        // ---- Execute ----
        var allResults = new List<RequestExecutionResult>();

        using var executor = new HttpRequestExecutor(opts.TimeoutMs);

        foreach (var filePath in httpFiles)
        {
            HttpFileDefinition fileDef;
            try
            {
                fileDef = HttpFileParser.Parse(filePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Parse error in '{filePath}': {ex.Message}");
                return 3;
            }

            var resolver = EnvironmentResolver.Load(filePath, opts.Env, fileDef.FileVariables);

            var requests = fileDef.Requests.AsEnumerable();
            if (!string.IsNullOrEmpty(opts.RequestFilter))
                requests = requests.Where(r =>
                    r.Name.Contains(opts.RequestFilter!, StringComparison.OrdinalIgnoreCase));

            foreach (var request in requests)
            {
                var resolved = resolver.ApplyTo(request);
                var response = await executor.ExecuteAsync(resolved);

                var assertions = isTest
                    ? AssertionEngine.Evaluate(resolved.Assertions, response)
                    : [];   // run mode: don't enforce assertions

                allResults.Add(new RequestExecutionResult
                {
                    Request = resolved,
                    Response = response,
                    AssertionResults = assertions,
                });
            }
        }

        // ---- Report ----
        TextWriter reportWriter = Console.Out;
        FileStream? fileStream = null;

        if (!string.IsNullOrEmpty(opts.OutputFile))
        {
            fileStream = File.Open(opts.OutputFile, FileMode.Create, FileAccess.Write);
            reportWriter = new StreamWriter(fileStream);
        }

        try
        {
            IReporter reporter = (opts.ReportFormat?.ToLowerInvariant()) switch
            {
                "junit" => new JUnitReporter(),
                "json" => new JsonReporter(),
                _ => new TextReporter(useColor: opts.UseColor, verbose: opts.Verbose),
            };

            await reporter.WriteAsync(allResults, reportWriter);

            if (reportWriter != Console.Out)
                await reportWriter.FlushAsync();
        }
        finally
        {
            if (fileStream is not null)
            {
                await fileStream.DisposeAsync();
            }
        }

        // ---- Exit code ----
        if (allResults.Any(r => r.HasTransportError)) return 2;
        if (isTest && allResults.Any(r => !r.AllAssertionsPassed)) return 1;
        return 0;
    }

    // -------------------------------------------------------------------------

    private static List<string> ResolveHttpFiles(string target, out string? requestFragment)
    {
        requestFragment = null;

        // Support file.http#RequestName fragment
        var hash = target.LastIndexOf('#');
        if (hash > 0)
        {
            requestFragment = target[(hash + 1)..];
            target = target[..hash];
        }

        if (File.Exists(target))
            return [Path.GetFullPath(target)];

        if (Directory.Exists(target))
            return Directory
                .EnumerateFiles(target, "*.http", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();

        throw new FileNotFoundException($"'{target}' is not a file or directory.");
    }

    private static CliOptions ParseOptions(string[] args)
    {
        var opts = new CliOptions();
        var i = 0;
        while (i < args.Length)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--env":
                    opts.Env = Next(args, ref i);
                    break;
                case "--request":
                    opts.RequestFilter = Next(args, ref i);
                    break;
                case "--report":
                    opts.ReportFormat = Next(args, ref i);
                    break;
                case "--output":
                    opts.OutputFile = Next(args, ref i);
                    break;
                case "--timeout":
                    if (int.TryParse(Next(args, ref i), out var t)) opts.TimeoutMs = t;
                    break;
                case "--no-color":
                    opts.UseColor = false;
                    break;
                case "--verbose":
                    opts.Verbose = true;
                    break;
            }
            i++;
        }
        return opts;
    }

    private static string Next(string[] args, ref int i)
    {
        if (++i < args.Length) return args[i];
        Console.Error.WriteLine($"Option '{args[i - 1]}' requires a value.");
        return string.Empty;
    }

    private static bool IsHelp(string arg) =>
        arg is "-h" or "--help" or "help";

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Resty — HTTP API client  (https://github.com/xkyii/Resty)

            Usage:
              resty run  <target> [options]   Send requests, display responses
              resty test <target> [options]   Run assertions, exit 1 on failure

            Target:
              file.http                       All requests in a file
              file.http#RequestName           Single named request
              ./directory                     All .http files recursively

            Options:
              --env <name>         Environment name (default: dev)
              --request <name>     Filter requests by name (substring match)
              --report <format>    Output format: text | junit | json  (default: text)
              --output <file>      Write report to file instead of stdout
              --timeout <ms>       Request timeout in milliseconds (default: 30000)
              --no-color           Disable ANSI colour output
              --verbose            Show request/response headers

            Exit codes:
              0   All requests succeeded (and assertions passed in test mode)
              1   One or more assertions failed  (test mode only)
              2   One or more requests failed (network/transport error)
              3   Configuration or file error
            """);
    }

    private sealed class CliOptions
    {
        public string Env { get; set; } = "dev";
        public string? RequestFilter { get; set; }
        public string? ReportFormat { get; set; }
        public string? OutputFile { get; set; }
        public int TimeoutMs { get; set; } = 30_000;
        public bool UseColor { get; set; } = true;
        public bool Verbose { get; set; }
    }
}
