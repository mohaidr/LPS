using LPS.Domain.Common.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

public interface IHttpResponseVariableHolder : IObjectVariableHolder
{
    IStringVariableHolder Body { get; }
    HttpStatusCode? StatusCode { get; }
    public string StatusReason { get; }
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
}
