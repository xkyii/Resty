using System.Threading.Tasks;
using Kx.Resty.Domain.Directories;

namespace Kx.Resty.Domain.Abstractions;

public interface IDirectoryStore
{
    Task<DirectoriesData> LoadAsync();
    Task SaveAsync(DirectoriesData data);
}
