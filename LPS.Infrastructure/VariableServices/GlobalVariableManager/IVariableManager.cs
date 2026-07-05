#nullable enable
using LPS.Domain.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.GlobalVariableManager
{
    public interface IVariableManager
    {
        Task PutAsync(string variableName, IVariableHolder variableHolder, CancellationToken token);
        /// <summary>Upsert: sets the variable, silently overwriting any existing value. Use when overwrite is intended.</summary>
        Task SetAsync(string variableName, IVariableHolder variableHolder, CancellationToken token = default);
        Task<IVariableHolder?> GetAsync(string variableName, CancellationToken token);
        Task RemoveVariableAsync(string variableName, CancellationToken token = default);


    }

}
