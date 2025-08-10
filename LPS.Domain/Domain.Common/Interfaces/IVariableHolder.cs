using LPS.Domain.Domain.Common.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{

    // Shoud we move the interface and the enum to the domain?
    public interface IVariableHolder
    {
        VariableType? Type { get; }
        bool IsGlobal { get; }

        ValueTask<string> GetRawValueAsync () ;
    }
}
