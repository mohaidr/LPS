using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{

    // Shoud we move the interface and the enum to the domain?
    public interface IVariableHolder
    {
        VariableType? Type { get; }
        bool IsGlobal { get; }
        IVariableMaintainer Maintainer { get; }
        ValueTask<string> GetRawValueAsync (CancellationToken token) ;
    }
}
