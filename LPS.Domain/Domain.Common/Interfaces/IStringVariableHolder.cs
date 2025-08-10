using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{

    // Shoud we move the interface and the enum to the domain?
    public interface IStringVariableHolder: IVariableHolder
    {
        string Pattern { get; } // This should change
        ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token);
    }
}
