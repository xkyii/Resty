using System.Collections.Generic;

namespace Kx.Resty.Features.Workspace.Models;

public sealed class ParsedHttpCollection
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required string RelativePath { get; init; }
    public required List<ParsedHttpRequest> Requests { get; init; }
    public required Dictionary<string, string> FileVariables { get; init; }
}

public sealed class ParsedHttpRequest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required string HeadersText { get; init; }
    public required string BodyText { get; init; }
    public required int SegmentIndex { get; init; }
    public required bool NoLog { get; init; }
    public int? TimeoutMs { get; init; }
}
