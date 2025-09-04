using System.Threading.Tasks;
using System.Threading;

namespace LPS.Infrastructure.PlaceHolderService
{
    public interface IPlaceholderProcessor
    {
        public Task<string> ProcessPlaceholderAsync(string placeholder, string sessionId, CancellationToken token);
    }
}