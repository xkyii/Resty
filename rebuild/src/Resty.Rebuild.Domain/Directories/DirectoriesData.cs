using System;
using System.Collections.Generic;

namespace Resty.Rebuild.Domain.Directories;

public record RecentDirectoryRecord(string Path, DateTime LastOpenedAt);

public record ManagedDirectoryRecord(string Path, DateTime AddedAt);

public record DirectoriesData(
    List<RecentDirectoryRecord> Recent,
    List<ManagedDirectoryRecord> Managed
)
{
    public static DirectoriesData Empty() => new([], []);
}
