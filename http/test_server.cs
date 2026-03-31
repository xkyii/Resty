#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property PublishAot=false

using System.Net;
using System.Text.Json;

var options = ParseArgs(args);
if (options.ShowHelp)
{
    PrintHelp();
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = []
});

builder.WebHost.UseUrls($"http://{options.Host}:{options.Port}");

var app = builder.Build();

app.MapGet("/", () => Results.Json(new
{
    name = "resty-test-server",
    version = "1.0",
    endpoints = new[]
    {
        "GET /ping",
        "GET /json",
        "GET /headers",
        "GET /cookies",
        "GET /status/{code}",
        "GET /delay?ms=500",
        "GET /set-cookie",
        "POST /echo"
    }
}));

app.MapGet("/ping", () => Results.Text("pong", "text/plain"));

app.MapGet("/json", () => Results.Json(new
{
    ok = true,
    time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    message = "hello from .NET 10 file-based ASP.NET server"
}));

app.MapGet("/headers", (HttpRequest req) => Results.Json(new
{
    headers = req.Headers.ToDictionary(
        x => x.Key,
        x => string.Join(",", x.Value.ToArray()),
        StringComparer.OrdinalIgnoreCase)
}));

app.MapGet("/cookies", (HttpRequest req) => Results.Json(new
{
    cookies = req.Cookies.ToDictionary(
        x => x.Key,
        x => x.Value ?? string.Empty,
        StringComparer.OrdinalIgnoreCase)
}));

app.MapGet("/set-cookie", (HttpResponse res) =>
{
    res.Cookies.Append("session_id", "abc123", new CookieOptions { Path = "/", HttpOnly = true });
    res.Cookies.Append("theme", "light", new CookieOptions { Path = "/" });
    return Results.Json(new { ok = true, set = 2 });
});

app.MapGet("/status/{code:int}", (int code) =>
{
    if (code < 100 || code > 599)
        return Results.Json(new { error = "invalid status code" }, statusCode: 400);

    var reason = Enum.IsDefined(typeof(HttpStatusCode), code)
        ? ((HttpStatusCode)code).ToString()
        : "Custom";
    return Results.Json(new { status = code, reason }, statusCode: code);
});

app.MapGet("/delay", async (HttpRequest req) =>
{
    var ms = 500;
    if (int.TryParse(req.Query["ms"], out var parsed))
        ms = Math.Clamp(parsed, 0, 60000);

    await Task.Delay(ms);
    return Results.Json(new { delayed_ms = ms });
});

app.MapPost("/echo", async (HttpRequest req) =>
{
    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();

    object? parsedJson = null;
    if ((req.ContentType ?? string.Empty).Contains("application/json", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            parsedJson = JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch
        {
            parsedJson = new { _parse_error = "invalid json" };
        }
    }

    return Results.Json(new
    {
        method = req.Method,
        path = req.Path.ToString(),
        query = req.Query.ToDictionary(
            x => x.Key,
            x => string.Join(",", x.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase),
        headers = req.Headers.ToDictionary(
            x => x.Key,
            x => string.Join(",", x.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase),
        body,
        body_json = parsedJson,
        size = body.Length
    });
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"[resty-test-server] listening on http://{options.Host}:{options.Port}");
    Console.WriteLine("[resty-test-server] press Ctrl+C to stop");
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("[resty-test-server] stopped");
});

app.Run();

static ServerOptions ParseArgs(string[] args)
{
    var options = new ServerOptions();

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--help":
            case "-h":
                options.ShowHelp = true;
                break;
            case "--host":
                if (i + 1 < args.Length)
                    options.Host = args[++i];
                break;
            case "--port":
                if (i + 1 < args.Length && int.TryParse(args[++i], out var port))
                    options.Port = Math.Clamp(port, 1, 65535);
                break;
        }
    }

    return options;
}

static void PrintHelp()
{
    Console.WriteLine("Single-file .NET 10 ASP.NET test server (file-based app)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --file .\\http\\test_server.cs -- --host 127.0.0.1 --port 8080");
}

sealed class ServerOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool ShowHelp { get; set; }
}
