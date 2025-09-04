
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class HashMethod : MethodBase
    {
        public HashMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r) { }

        public override string Name => "hash";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                string algorithm = (await _params.ExtractStringAsync(parameters, "algorithm", "SHA256", sessionId, token)).ToUpperInvariant();
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                using var hasher = algorithm switch
                {
                    "MD5" => System.Security.Cryptography.MD5.Create(),
                    "SHA1" => System.Security.Cryptography.SHA1.Create(),
                    "SHA256" => System.Security.Cryptography.SHA256.Create(),
                    "SHA384" => System.Security.Cryptography.SHA384.Create(),
                    "SHA512" => (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA512.Create(),
                    _ => null
                };


                if (hasher == null)
                {
                    await _logger.LogAsync(_op.OperationId, $"Unsupported hash algorithm '{algorithm}'.", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                byte[] hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                string result = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"hash failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
