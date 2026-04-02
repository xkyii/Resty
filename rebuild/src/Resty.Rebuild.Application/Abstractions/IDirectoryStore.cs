using System.Threading.Tasks;
using Resty.Rebuild.Domain.Directories;

namespace Resty.Rebuild.Application.Abstractions;

public interface IDirectoryStore
{
    Task<DirectoriesData> LoadAsync();
    Task SaveAsync(DirectoriesData data);
}
