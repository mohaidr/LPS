using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Client
{
    public class LPSHttpClientService: ILPSClientService<LPSHttpRequest>
    {
        private HttpClient httpClient;
        private ICustomLogger _logger;

        public LPSHttpClientService(ILPSClientConfiguration<LPSHttpRequest> config, ICustomLogger logger) 
        {
            _logger= logger;
            SocketsHttpHandler socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = ((LPSHttpClientConfiguration)config).PooledConnectionLifetime,
                PooledConnectionIdleTimeout = ((LPSHttpClientConfiguration)config).PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = ((LPSHttpClientConfiguration)config).MaxConnectionsPerServer,
            };
            httpClient = new HttpClient(socketsHandler);
            httpClient.Timeout = ((LPSHttpClientConfiguration)config).Timeout;
        }
        public async Task Send(LPSHttpRequest lpsHttpRequest, string requestId, CancellationToken cancellationToken)
        {
            try
            {
                var httpRequestMessage = new HttpRequestMessage();
                httpRequestMessage.RequestUri = new Uri(lpsHttpRequest.URL);
                httpRequestMessage.Method = new HttpMethod(lpsHttpRequest.HttpMethod);

                bool supportsContent = (lpsHttpRequest.HttpMethod.ToLower() == "post" || lpsHttpRequest.HttpMethod.ToLower() == "put" || lpsHttpRequest.HttpMethod.ToLower() == "patch");
                string major = lpsHttpRequest.Httpversion.Split('.')[0];
                string minor = lpsHttpRequest.Httpversion.Split('.')[1];
                httpRequestMessage.Version = new Version(int.Parse(major), int.Parse(minor));
                httpRequestMessage.Content = supportsContent ? new StringContent(lpsHttpRequest.Payload) : null;


                foreach (var header in lpsHttpRequest.HttpHeaders)
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

                var responseMessageTask = httpClient.SendAsync(httpRequestMessage, cancellationToken);
                var responseMessage = await responseMessageTask;
                await _logger.LogAsync(string.Empty, $"...Response for call # {requestId}...\n\tStatus Code: {(int)responseMessage.StatusCode} Reason: {responseMessage.StatusCode}\n\t Response Body: {responseMessage.Content.ReadAsStringAsync().Result}\n\t Response Headers: {responseMessage.Headers}", LoggingLevel.INF);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Only one usage of each socket address") || (ex.InnerException != null && ex.InnerException.Message.Contains("Only one usage of each socket address")))
                {
                    Console.WriteLine(ex);
                }

                await _logger.LogAsync(string.Empty, @$"...Response for call # {requestId} \n\t ...Call # {requestId} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LoggingLevel.ERR);
                throw new Exception(ex.Message, ex.InnerException);
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



