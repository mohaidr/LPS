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
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncRequest
    {
        private class ProtectedAccessContainerExecuteCommand : HttpAsyncRequestWrapper.ExecuteCommand
        {
            public new int SafelyIncrementNumberofContainerSentRequests(HttpAsyncRequestWrapper.ExecuteCommand dto)
            {
                return base.SafelyIncrementNumberofContainerSentRequests(dto);
            }
            public new int SafelyIncrementFailedCallsCounter(HttpAsyncRequestWrapper.ExecuteCommand dto)
            {
                return base.SafelyIncrementFailedCallsCounter(dto);
            }
            public new int SafelyIncrementSuccessfulCallsCounter(HttpAsyncRequestWrapper.ExecuteCommand dto)
            {
                return base.SafelyIncrementSuccessfulCallsCounter(dto);
            }
        }

        public class ExecuteCommand : IAsyncCommand<HttpAsyncRequest>
        {
            public ExecuteCommand()
            {
                HttpAsyncRequestContainerExecuteCommand = new HttpAsyncRequestWrapper.ExecuteCommand();
            }

            public HttpAsyncRequestWrapper.ExecuteCommand HttpAsyncRequestContainerExecuteCommand { get; set; }

            async public Task ExecuteAsync(HttpAsyncRequest entity)
            {
                await entity.ExecuteAsync(this);
            }
        }

        async private Task ExecuteAsync(ExecuteCommand dto)
        {
            if (this.IsValid)
            {
                int callNumber = int.MinValue;
                ProtectedAccessContainerExecuteCommand command = new ProtectedAccessContainerExecuteCommand();
                try
                {

                        var httpRequestMessage = new HttpRequestMessage();
                        httpRequestMessage.RequestUri = new Uri(this.URL);
                        httpRequestMessage.Method = new HttpMethod(this.HttpMethod);

                        bool supportsContent = (this.HttpMethod.ToLower() == "post" || this.HttpMethod.ToLower() == "put" || this.HttpMethod.ToLower() == "patch");
                        string major = this.Httpversion.Split('.')[0];
                        string minor = this.Httpversion.Split('.')[1];
                        httpRequestMessage.Version = new Version(int.Parse(major), int.Parse(minor));
                        httpRequestMessage.Content = supportsContent ? new StringContent(this.Payload) : null;


                        foreach (var header in this.HttpHeaders)
                        {

                            if (supportsContent)
                            {
                                var contentHeaders = httpRequestMessage.Content.Headers;

                                if (contentHeaders.GetType().GetProperties().Any(property => property.Name.ToLower() == header.Key.ToLower().Replace("-", "")))
                                {
                                    SetContentHeader(httpRequestMessage, header.Key, header.Value);
                                    continue;
                                }
                            }

                            if (!(new StringContent("").Headers.GetType().GetProperties().Any(property => property.Name.ToLower() == header.Key.ToLower().Replace("-", ""))))
                            {
                                var requestHeader = httpRequestMessage.Headers;
                                if (requestHeader.GetType().GetProperties().Any(property => property.Name.ToLower() == header.Key.ToLower().Replace("-", "")))
                                {
                                    SetRequestHeader(httpRequestMessage, header.Key, header.Value.Trim());
                                }
                                else
                                {
                                    SetUserHeader(httpRequestMessage, header.Key, header.Value.Trim());
                                }
                            }
                        
                    }

                    var responseMessageTask = httpClient.SendAsync(httpRequestMessage);
                    callNumber = command.SafelyIncrementNumberofContainerSentRequests(dto.HttpAsyncRequestContainerExecuteCommand);
                    var responseMessage = await responseMessageTask;
                    this.HasFailed = false;
                    command.SafelyIncrementSuccessfulCallsCounter(dto.HttpAsyncRequestContainerExecuteCommand);
                    await _logger.LogAsync(string.Empty, $"...Response for call # {callNumber}...\n\tStatus Code: {(int)responseMessage.StatusCode} Reason: {responseMessage.StatusCode}\n\t Response Body: {responseMessage.Content.ReadAsStringAsync().Result}\n\t Response Headers: {responseMessage.Headers}", LoggingLevel.INF);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Only one usage of each socket address") || (ex.InnerException != null && ex.InnerException.Message.Contains("Only one usage of each socket address")))
                    {
                        Console.WriteLine(ex);
                    }

                    command.SafelyIncrementFailedCallsCounter(dto.HttpAsyncRequestContainerExecuteCommand);
                    this.HasFailed = true;
                    await _logger.LogAsync(string.Empty, @$"...Response for call # {callNumber} \n\t ...Call # {callNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty) } \n\t  {ex.Message} \n  {ex.StackTrace}", LoggingLevel.ERR);

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
                    message.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true};
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
