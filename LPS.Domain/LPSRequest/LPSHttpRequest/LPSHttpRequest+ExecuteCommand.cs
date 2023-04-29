using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSHttpRequest
    {
        private class ProtectedAccessLPSTestCaseExecuteCommand : LPSHttpTestCase.ExecuteCommand
        {
            public new int SafelyIncrementNumberofSentRequests(LPSHttpTestCase.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberofSentRequests(command);
            }
            public new int SafelyIncrementNumberOfFailedRequests(LPSHttpTestCase.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfFailedRequests(command);
            }
            public new int SafelyIncrementNumberOfSuccessfulRequests(LPSHttpTestCase.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfSuccessfulRequests(command);
            }
        }

        new public class ExecuteCommand : IAsyncCommand<LPSHttpRequest> 
        {
            internal ILPSClientService<LPSHttpRequest> HttpClientService { get; set; }

            public ExecuteCommand()
            {
                LPSTestCaseExecuteCommand = new LPSHttpTestCase.ExecuteCommand();
            }

            public LPSHttpTestCase.ExecuteCommand LPSTestCaseExecuteCommand { get; set; }

            async public Task ExecuteAsync(LPSHttpRequest entity, CancellationToken cancellationToken)
            {
                await entity.ExecuteAsync(this, cancellationToken);
            }
        }

        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            if (this.IsValid)
            {
                int requestNumber = 0;
                ProtectedAccessLPSTestCaseExecuteCommand protectedCommand = new ProtectedAccessLPSTestCaseExecuteCommand();
                try
                {
                    if (command.HttpClientService == null)
                    {
                        throw new InvalidOperationException("Http Client Is Not Defined");
                    }

                    requestNumber = protectedCommand.SafelyIncrementNumberofSentRequests(command.LPSTestCaseExecuteCommand);
                    var clientServiceTask = command.HttpClientService.Send(this, requestNumber.ToString(), cancellationToken);
                    await clientServiceTask;
                    this.HasFailed = false;
                    protectedCommand.SafelyIncrementNumberOfSuccessfulRequests(command.LPSTestCaseExecuteCommand);
                }
                catch (Exception ex)
                {
                    protectedCommand.SafelyIncrementNumberOfFailedRequests(command.LPSTestCaseExecuteCommand);
                    this.HasFailed = true;
                }
            }
        }

        private void SetContentHeader(HttpRequestMessage message, string name, string value)
        {

            switch (name)
            {
                case "content-type":
                    message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(value);
                    break;
                case "content-encoding":
                    var contentEncoding = value.Trim().Split(',');
                    foreach (var encoding in contentEncoding)
                    {
                        message.Content.Headers.ContentEncoding.Add(encoding);
                    }
                    break;
                case "content-language":
                    var languages = value.Trim().Split(',');
                    foreach (var language in languages)
                    {
                        message.Content.Headers.ContentLanguage.Add(language);
                    }
                    break;
                case "content-length":
                    message.Content.Headers.ContentLength = long.Parse(value);
                    break;
                default:
                    {
                        //TODO: Support the below content headers
                        /*_ = message.Content.Headers.ContentMD5;
                        _ = message.Content.Headers.ContentRange;
                        _ = message.Content.Headers.ContentLocation;
                        _ = message.Content.Headers.LastModified;*/
                        throw new NotSupportedException("Unsupported Content Header, the current supported headers are (content-type, content-encoding, content-length, content-language)");
                    }
            }
        }

        private void SetRequestHeader(HttpRequestMessage message, string name, string value)
        {
            string[] encodings;
            switch (name.Trim().ToLower())
            {
                case "authorization":
                    message.Headers.Authorization = new AuthenticationHeaderValue(value);
                    break;
                case "accept":
                    var types = value.Trim().Split(',');

                    foreach (var type in types)
                    {
                        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(type));
                    }
                    break;
                case "accept-charset":
                    var charsets = value.Trim().Split(',');

                    foreach (var charset in charsets)
                    {
                        message.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue(charset));
                    }
                    break;
                case "accept-encoding":
                    encodings = value.Trim().Split(',');
                    foreach (var encoding in encodings)
                    {
                        message.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
                    }
                    break;
                case "accept-language":
                    var languages = value.Trim().Split(',');

                    foreach (var language in languages)
                    {
                        message.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
                    }
                    break;
                case "connection":
                    var connectionValues = value.Trim().Split(',');

                    foreach (var connectionValue in connectionValues)
                    {
                        message.Headers.Connection.Add(connectionValue);
                    }
                    break;
                case "host":
                    message.Headers.Host = value;
                    break;
                case "transfer-encoding":
                    encodings = value.Trim().Split(',');
                    foreach (var encoding in encodings)
                    {
                        message.Headers.TransferEncoding.Add(new TransferCodingHeaderValue(encoding));
                    }
                    break;
                case "user-agent":
                    var agents = value.Trim().Split(',');

                    foreach (var agent in agents)
                    {
                        message.Headers.UserAgent.Add(new ProductInfoHeaderValue(agent));
                    }
                    break;
                case "upgrade":
                    message.Headers.Upgrade.Add(new ProductHeaderValue(value));
                    break;
                case "pragma":
                    message.Headers.Pragma.Add(new NameValueHeaderValue(value));
                    break;
                case "cache-control":
                    message.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true };
                    break;
                default:
                    throw new NotSupportedException($"header {name} is unsupported requesat header, the current supported headers are (authorization, accept, accept-charset, accept-encoding, accept-language, connection, host, transfer-encoding, user-agent, )");


            }

            //TODO: Support the below content headers
            /*
            _ = message.Headers.ExpectContinue;
            _ = message.Headers.ConnectionClose = ;
            _ = message.Headers.TransferEncodingChunked;
            _ = message.Headers.CacheControl;
            _ = message.Headers.Date;
            _ = message.Headers.From;
            _ = message.Headers.IfMatch;
            _ = message.Headers.IfNoneMatch;
            _ = message.Headers.IfRange;
            _ = message.Headers.IfUnmodifiedSince;
            _ = message.Headers.IfModifiedSince;
            _ = message.Headers.MaxForwards;
            _ = message.Headers.Pragma;
            _ = message.Headers.ProxyAuthorization;
            _ = message.Headers.Range;
            _ = message.Headers.Referrer;
            _ = message.Headers.TE;
            _ = message.Headers.Trailer;
            _ = message.Headers.Upgrade;
            _ = message.Headers.Via;
            _ = message.Headers.Warning;*/
        }

        private void SetUserHeader(HttpRequestMessage message, string name, string value)
        {
            message.Headers.Add(name, value);
        }

    }
}
