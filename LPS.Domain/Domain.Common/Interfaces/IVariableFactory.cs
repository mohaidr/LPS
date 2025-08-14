// DOMAIN - Interface
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface IVariableFactory
    {
        Task<IStringVariableHolder> CreateStringAsync(
            string rawValue,
            VariableType type = VariableType.String,   // String, JsonString, XmlString, CsvString
            string? pattern = null,
            bool isGlobal = false,
            CancellationToken token = default);

        Task<IVariableHolder> CreateBooleanAsync(
            string rawValue,
            bool isGlobal = false,
            CancellationToken token = default);

        // NEW: Number variable (Int, Float, Double, Decimal)
        Task<IVariableHolder> CreateNumberAsync(
            string rawValue,
            VariableType type,                         // Int, Float, Double, Decimal
            bool isGlobal = false,
            CancellationToken token = default);

        // NEW: HttpResponse variable
        Task<IHttpResponseVariableHolder> CreateHttpResponseAsync(
            IStringVariableHolder body,                // interface (domain doesn’t know concretes)
            HttpStatusCode statusCode,
            IEnumerable<KeyValuePair<string, string>>? headers = null,
            bool isGlobal = false,
            CancellationToken token = default);
    }
}
