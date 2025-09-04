
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class DateTimeAliasMethod : IPlaceholderMethod
    {
        private readonly TimestampMethod _inner;
        public DateTimeAliasMethod(TimestampMethod inner) { _inner = inner; }
        public string Name => "datetime";
        public Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token) 
            => _inner.ExecuteAsync(parameters, sessionId, token);
    }
}
