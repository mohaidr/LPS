
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// Contract for a placeholder method like $hash(...), $random(...), $read(...)
    /// </summary>
    public interface IPlaceholderMethod
    {
        /// <summary>Method name as used after the $ (case-insensitive).</summary>
        string Name { get; }

        /// <summary>Execute the method with its raw parameter string.</summary>
        Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token);
    }
}
