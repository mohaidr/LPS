using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IVariableMaintainer
    {
        public ValueTask<IVariableHolder> UpdateAsync(CancellationToken token);
    }
}
