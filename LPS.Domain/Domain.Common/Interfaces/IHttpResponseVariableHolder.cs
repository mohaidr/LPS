using LPS.Domain.Common.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

public interface IHttpResponseVariableHolder : IVariableHolder
{
    IStringVariableHolder Body { get; }
    HttpStatusCode? StatusCode { get; }
    public string StatusReason { get; }

    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    // Path routing: ".Body....", ".StatusCode", ".Headers.Name" (and optional indexing if you add it)
    ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token);
}
