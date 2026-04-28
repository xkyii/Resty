using System.IO;
using Resty.Core.Environment;

namespace Resty.Core.Tests.Environment;

public class EnvironmentResolverTests
{
    private static string CreateTempDir(
        string? envJson = null,
        string? privateJson = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        if (envJson is not null)
            File.WriteAllText(Path.Combine(dir, "http-client.env.json"), envJson);

        if (privateJson is not null)
            File.WriteAllText(Path.Combine(dir, "http-client.private.env.json"), privateJson);

        return dir;
    }

    // ---- Basic variable resolution ----

    [Fact]
    public void Resolve_PlaceholderReplaced()
    {
        var dir = CreateTempDir("""{"dev": {"host": "localhost:8080"}}""");
        var dummyFile = Path.Combine(dir, "test.http");

        var resolver = EnvironmentResolver.Load(dummyFile, "dev");
        Assert.Equal("https://localhost:8080/api", resolver.Resolve("https://{{host}}/api"));
    }

    [Fact]
    public void Resolve_NoPlaceholder_Unchanged()
    {
        var dir = CreateTempDir("""{"dev": {}}""");
        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev");
        Assert.Equal("https://example.com", resolver.Resolve("https://example.com"));
    }

    // ---- env.json loading ----

    [Fact]
    public void Load_WrongEnvName_NoVariables()
    {
        var dir = CreateTempDir("""{"prod": {"host": "prod.example.com"}}""");
        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev");
        Assert.Equal("{{host}}", resolver.Resolve("{{host}}"));
    }

    // ---- Private env overlays public env ----

    [Fact]
    public void Load_PrivateEnvOverridesPublic()
    {
        var dir = CreateTempDir(
            envJson: """{"dev": {"token": "public-token"}}""",
            privateJson: """{"dev": {"token": "private-token"}}""");

        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev");
        Assert.Equal("private-token", resolver.Resolve("{{token}}"));
    }

    // ---- Missing env files are silently ignored ----

    [Fact]
    public void Load_NoEnvFiles_DoesNotThrow()
    {
        var dir = CreateTempDir();  // no env files
        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev");
        Assert.Equal("{{host}}", resolver.Resolve("{{host}}"));
    }

    // ---- File-level variables ----

    [Fact]
    public void Load_FileVariables_UsedAsFallback()
    {
        var dir = CreateTempDir();  // no env files
        var fileVars = new Dictionary<string, string> { ["host"] = "localhost" };
        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev", fileVars);
        Assert.Equal("localhost", resolver.Resolve("{{host}}"));
    }

    [Fact]
    public void Load_EnvOverridesFileVariables()
    {
        var dir = CreateTempDir("""{"dev": {"host": "env-host"}}""");
        var fileVars = new Dictionary<string, string> { ["host"] = "file-host" };
        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev", fileVars);
        Assert.Equal("env-host", resolver.Resolve("{{host}}"));
    }

    // ---- ApplyTo resolves all request fields ----

    [Fact]
    public void ApplyTo_ResolvesUrlAndHeaders()
    {
        var dir = CreateTempDir("""{"dev": {"host": "api.example.com", "token": "abc"}}""");
        var resolver = EnvironmentResolver.Load(Path.Combine(dir, "test.http"), "dev");

        var request = new Resty.Core.Models.HttpRequestDefinition
        {
            Name = "Test",
            Method = "GET",
            Url = "https://{{host}}/api",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer {{token}}",
            },
        };

        var resolved = resolver.ApplyTo(request);

        Assert.Equal("https://api.example.com/api", resolved.Url);
        Assert.Equal("Bearer abc", resolved.Headers["Authorization"]);
    }
}
