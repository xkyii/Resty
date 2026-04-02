using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Kx.Resty.Domain.Abstractions;
using Kx.Resty.Domain.Directories;

namespace Kx.Resty.Infrastructure.Persistence;

public sealed class JsonDirectoryStore : IDirectoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonDirectoryStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Resty");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "directories.json");
    }

    public async Task<DirectoriesData> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return DirectoriesData.Empty();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<DirectoriesDto>(json, JsonOptions);
            if (dto is null) return DirectoriesData.Empty();

            var recent = dto.Recent?.ConvertAll(r => new RecentDirectoryRecord(r.Path, r.LastOpenedAt)) ?? [];
            var managed = dto.Managed?.ConvertAll(m => new ManagedDirectoryRecord(m.Path, m.AddedAt)) ?? [];
            return new DirectoriesData(recent, managed);
        }
        catch
        {
            return DirectoriesData.Empty();
        }
    }

    public async Task SaveAsync(DirectoriesData data)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var dto = new DirectoriesDto
            {
                Recent = data.Recent.ConvertAll(r => new RecentDto { Path = r.Path, LastOpenedAt = r.LastOpenedAt }),
                Managed = data.Managed.ConvertAll(m => new ManagedDto { Path = m.Path, AddedAt = m.AddedAt })
            };
            var json = JsonSerializer.Serialize(dto, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class DirectoriesDto
    {
        [JsonPropertyName("recent")]
        public System.Collections.Generic.List<RecentDto>? Recent { get; set; }

        [JsonPropertyName("managed")]
        public System.Collections.Generic.List<ManagedDto>? Managed { get; set; }
    }

    private sealed class RecentDto
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("lastOpenedAt")]
        public DateTime LastOpenedAt { get; set; }
    }

    private sealed class ManagedDto
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("addedAt")]
        public DateTime AddedAt { get; set; }
    }
}
