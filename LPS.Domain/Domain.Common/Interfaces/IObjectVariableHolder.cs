using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{

    // Shoud we move the interface and the enum to the domain?
    public interface IObjectVariableHolder : IVariableHolder
    {
        // Path routing: ".Body....", ".StatusCode", ".Headers.Name" (and optional indexing if you add it)
        ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token);
    }
}
