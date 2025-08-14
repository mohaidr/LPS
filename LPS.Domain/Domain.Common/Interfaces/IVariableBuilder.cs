using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IVariableBuilder
    {
        public ValueTask<IVariableHolder> BuildAsync(CancellationToken token);
    }
}
