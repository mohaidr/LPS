using HtmlAgilityPack;
using LPS.Domain;
using LPS.Domain.Common;
using LPS.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Client
{
    public class LPSHttpClientService : ILPSClientService<LPSHttpRequest>
    {
        private HttpClient httpClient;
        private ILPSLogger _logger;
        private static int _clientNumber;
        public string Id { get; private set; }
        public string GuidId { get; private set; }
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSHttpClientService(ILPSClientConfiguration<LPSHttpRequest> config, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            SocketsHttpHandler socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = ((ILPSHttpClientConfiguration<LPSHttpRequest>)config).PooledConnectionLifetime,
                PooledConnectionIdleTimeout = ((ILPSHttpClientConfiguration<LPSHttpRequest>)config).PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = ((ILPSHttpClientConfiguration<LPSHttpRequest>)config).MaxConnectionsPerServer,
                UseCookies = true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
            };
            httpClient = new HttpClient(socketsHandler);
            httpClient.Timeout = ((ILPSHttpClientConfiguration<LPSHttpRequest>)config).Timeout;
            Id = Interlocked.Increment(ref _clientNumber).ToString();
            GuidId = Guid.NewGuid().ToString();
        }
        public async Task SendAsync(LPSHttpRequest lpsHttpRequest, string requestNumber, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            var requestUri = new Uri(lpsHttpRequest.URL);
            try
            {
                LPSConnectionEventSource.Log.ConnectionEstablished(requestUri.Host);
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
                            SetRequestHeader(httpRequestMessage, header.Key.Trim(), header.Value.Trim());
                        }
                        else
                        {
                            SetUserHeader(httpRequestMessage, header.Key.Trim(), header.Value.Trim());
                        }
                    }
                }
                Stopwatch watch = Stopwatch.StartNew();
                TimeSpan timeToGetFullResponse;
                var responseMessageTask = httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationTokenWrapper.CancellationToken);
                var response = await responseMessageTask;
                string contentType = response?.Content?.Headers?.ContentType?.MediaType;

                MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);
                string fileExtension = mimeType.ToFileExtension();
                string directoryName = $"{_runtimeOperationIdProvider.OperationId}.Resources";
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                string fileName = $"{directoryName}/{Id}.{requestNumber}.{lpsHttpRequest.Id}{fileExtension}";

                using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                using (FileStream fileStream = File.Create(fileName))
                {
                    byte[] buffer = new byte[1048576]; // Adjust the buffer size as needed
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                    }
                    timeToGetFullResponse = watch.Elapsed;
                    watch.Stop();
                }

                LPSHttpResponse.SetupCommand lpsResponseCommand = new LPSHttpResponse.SetupCommand
                {
                    StatusCode = response.StatusCode,
                    LocationToResponse = fileName,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    ResponseContentHeaders = response.Content?.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ResponseHeaders = response.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    MIMEType = mimeType
                };
                lpsHttpRequest.LPSHttpResponses.Add(new LPSHttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider));

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {Id} - Request # {requestNumber} {lpsHttpRequest.HttpMethod} {lpsHttpRequest.URL} Http/{lpsHttpRequest.Httpversion}\n\tTotal Time: {timeToGetFullResponse.TotalMilliseconds} MS\n\tStatus Code: {(int)response.StatusCode} Reason: {response.StatusCode}\n\tResponse Body: {fileName}\n\tResponse Headers: {response.Headers}{response.Content.Headers}", LPSLoggingLevel.Verbos, cancellationTokenWrapper);

                if (contentType == "text/html" && response.IsSuccessStatusCode && lpsHttpRequest.DownloadHtmlEmbeddedResources)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Downloading Embedded Resources - Client: {Id} - Request # {requestNumber} {lpsHttpRequest.HttpMethod} {lpsHttpRequest.URL} Http/{lpsHttpRequest.Httpversion}", LPSLoggingLevel.Verbos, cancellationTokenWrapper);
                    string htmlContent = await File.ReadAllTextAsync(fileName, Encoding.UTF8);
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(htmlContent);
                    await DownloadHtmlEmbeddedResources(doc, lpsHttpRequest.URL, httpClient, cancellationTokenWrapper);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || (ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer"))))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {Id} - Request # {requestNumber} {lpsHttpRequest.HttpMethod} {lpsHttpRequest.URL} Http/{lpsHttpRequest.Httpversion} \n\t  The request # {requestNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, cancellationTokenWrapper);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {Id} - Request # {requestNumber} {lpsHttpRequest.HttpMethod} {lpsHttpRequest.URL} Http/{lpsHttpRequest.Httpversion} \n\t The request # {requestNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, cancellationTokenWrapper);
                throw new Exception(ex.Message, ex.InnerException);
            }
            finally {
                LPSConnectionEventSource.Log.ConnectionClosed(requestUri.Host);
            }
        }

        private void SetContentHeader(HttpRequestMessage message, string name, string value)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                return;

            switch (name.ToLower())
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
                case "content-disposition":
                    message.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue(value);
                    break;
                case "content-md5":
                    message.Content.Headers.ContentMD5 = Convert.FromBase64String(value);
                    break;
                default:
                    throw new NotSupportedException("Unsupported Content Header, the currently supported headers are (content-type, content-encoding, content-length, content-language, content-disposition, content-location, content-md5, content-range, expires, last-modified)");
            }
        }

        private void SetRequestHeader(HttpRequestMessage message, string name, string value)
        {

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                return;

            string[] encodings;
            switch (name.Trim().ToLower())
            {
                case "authorization":
                    AuthenticationHeaderValue authValue;
                    if (AuthenticationHeaderValue.TryParse(value, out authValue))
                    {
                        message.Headers.Authorization = authValue;
                    }
                    break;
                case "accept":
                    var types = value.Trim().Split(',');

                    foreach (var type in types)
                    {
                        MediaTypeWithQualityHeaderValue typeValue;

                        if (MediaTypeWithQualityHeaderValue.TryParse(type, out typeValue))
                        {
                            message.Headers.Accept.Add(typeValue);
                        }
                    }
                    break;
                case "accept-charset":
                    var charsets = value.Trim().Split(',');

                    foreach (var charset in charsets)
                    {
                        StringWithQualityHeaderValue charsetValue;
                        if (StringWithQualityHeaderValue.TryParse(charset, out charsetValue))
                        {
                            message.Headers.AcceptCharset.Add(charsetValue);
                        }
                    }
                    break;
                case "accept-encoding":
                    encodings = value.Trim().Split(',');
                    foreach (var encoding in encodings)
                    {
                        StringWithQualityHeaderValue encodingValue;
                        if (StringWithQualityHeaderValue.TryParse(encoding, out encodingValue))
                        {
                            message.Headers.AcceptEncoding.Add(encodingValue);
                        }

                    }
                    break;
                case "accept-language":
                    var languages = value.Trim().Split(',');

                    foreach (var language in languages)
                    {
                        StringWithQualityHeaderValue languageValue;
                        if (StringWithQualityHeaderValue.TryParse(language, out languageValue))
                        {
                            message.Headers.AcceptLanguage.Add(languageValue);
                        }
                    }
                    break;
                case "connection":
                    var connectionValues = value.Trim().Split(',');

                    foreach (var connectionValue in connectionValues)
                    {
                        message.Headers.Connection.Add(connectionValue);
                        if (connectionValue.ToLower() == "close")
                        {
                            message.Headers.ConnectionClose = true;
                        }
                    }
                    break;
                case "host":
                    message.Headers.Host = value;
                    break;
                case "transfer-encoding":
                    encodings = value.Trim().Split(',');
                    foreach (var encoding in encodings)
                    {
                        TransferCodingHeaderValue encodingValue;
                        if (TransferCodingHeaderValue.TryParse(encoding, out encodingValue))
                        {
                            message.Headers.TransferEncoding.Add(encodingValue);
                            if (encoding.ToLower() == "chuncked")
                            {
                                message.Headers.TransferEncodingChunked = true;
                            }
                        }
                    }
                    break;
                case "user-agent":
                    var agents = value.Trim().Split(',');
                    foreach (var agent in agents)
                    {
                        ProductInfoHeaderValue agentValue;
                        if (ProductInfoHeaderValue.TryParse(agent, out agentValue))
                        {
                            message.Headers.UserAgent.Add(agentValue);
                        }
                    }
                    break;
                case "upgrade":
                    ProductHeaderValue upgradeValue;
                    if (ProductHeaderValue.TryParse(value, out upgradeValue))
                    {
                        message.Headers.Upgrade.Add(upgradeValue);
                    }
                    break;
                case "pragma":
                    message.Headers.Pragma.Add(new NameValueHeaderValue(value));
                    break;
                case "cache-control":
                    CacheControlHeaderValue cacheControlValue;
                    if (CacheControlHeaderValue.TryParse(value, out cacheControlValue))
                    {
                        message.Headers.CacheControl = cacheControlValue;
                    }
                    break;
                // Additional headers to apply
                case "expect":
                    message.Headers.ExpectContinue = value.Trim() == "100-continue";
                    break;
                case "date":
                    DateTimeOffset date;
                    if (DateTimeOffset.TryParse(value, out date))
                    {
                        message.Headers.Date = date;
                    }
                    break;
                case "from":
                    message.Headers.From = value;
                    break;
                case "if-match":
                    var matches = value.Trim().Split(',');
                    foreach (var match in matches)
                    {
                        EntityTagHeaderValue matchValue;
                        if (EntityTagHeaderValue.TryParse(match, out matchValue))
                        {
                            message.Headers.IfMatch.Add(matchValue);
                        }
                    }
                    break;
                case "if-none-match":
                    var noneMatches = value.Trim().Split(',');
                    foreach (var noneMatch in noneMatches)
                    {
                        EntityTagHeaderValue noneMatchValue;
                        if (EntityTagHeaderValue.TryParse(noneMatch, out noneMatchValue))
                        {
                            message.Headers.IfNoneMatch.Add(noneMatchValue);
                        }
                    }
                    break;
                case "if-unmodified-since":
                    DateTimeOffset ifUnmodifiedSince;
                    if (DateTimeOffset.TryParse(value, out ifUnmodifiedSince))
                    {
                        message.Headers.IfUnmodifiedSince = ifUnmodifiedSince;
                    }
                    break;
                case "if-modified-since":
                    DateTimeOffset ifModifiedSince;
                    if (DateTimeOffset.TryParse(value, out ifModifiedSince))
                    {
                        message.Headers.IfModifiedSince = ifModifiedSince;
                    }
                    break;
                case "max-forwards":
                    int maxForwards;
                    if (int.TryParse(value, out maxForwards))
                    {
                        message.Headers.MaxForwards = maxForwards;
                    }
                    break;
                case "proxy-authorization":
                    AuthenticationHeaderValue authHeaderValue;
                    if (AuthenticationHeaderValue.TryParse(value, out authHeaderValue))
                    {
                        message.Headers.ProxyAuthorization = authHeaderValue;
                    }
                    break;
                case "range":
                    RangeHeaderValue rangeValue;
                    if (RangeHeaderValue.TryParse(value, out rangeValue))
                    {
                        message.Headers.Range = rangeValue;
                    }
                    break;
                case "if-range":
                    RangeConditionHeaderValue ifRangeValue;
                    if (RangeConditionHeaderValue.TryParse(value, out ifRangeValue))
                    {
                        message.Headers.IfRange = ifRangeValue;
                    }

                    break;
                case "referrer":
                    Uri referrerValue;
                    if (Uri.TryCreate(value, UriKind.Absolute, out referrerValue))
                    {
                        message.Headers.Referrer = referrerValue;
                    }
                    break;
                case "te":
                    var tes = value.Trim().Split(',');
                    foreach (var te in tes)
                    {
                        TransferCodingWithQualityHeaderValue teValue;
                        if (TransferCodingWithQualityHeaderValue.TryParse(te, out teValue))
                        {
                            message.Headers.TE.Add(teValue);
                        }
                    }
                    break;
                case "trailer":
                    var trailers = value.Trim().Split(',');
                    foreach (var trailer in trailers)
                    {
                        message.Headers.Trailer.Add(trailer);
                    }
                    break;
                case "via":
                    var vias = value.Trim().Split(',');
                    foreach (var via in vias)
                    {
                        ViaHeaderValue viaValue;
                        if (ViaHeaderValue.TryParse(via, out viaValue))
                        {
                            message.Headers.Via.Add(viaValue);
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException($"header {name} is an unsupported request header.");
            }
        }

        private void SetUserHeader(HttpRequestMessage message, string name, string value)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                return;

            message.Headers.Add(name, value);
        }

        private async Task DownloadHtmlEmbeddedResources(HtmlDocument doc, string baseUrl, HttpClient client, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            // XPath expressions to select different types of resources
            string[] resourceXPaths = { "//img", "//link[@rel='stylesheet']", "//script" };

            foreach (string resourceXPath in resourceXPaths)
            {
                var resourceNodes = doc.DocumentNode.SelectNodes(resourceXPath);
                if (resourceNodes != null)
                {
                    foreach (var resourceNode in resourceNodes)
                    {
                        string resourceUrl = resourceNode.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(resourceUrl) && !resourceUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] resourceData = await client.GetByteArrayAsync(new Uri(new Uri(baseUrl), resourceUrl), cancellationTokenWrapper.CancellationToken);
                            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Downloaded: {resourceUrl}", LPSLoggingLevel.Verbos, cancellationTokenWrapper);
                        }
                    }
                }
            }
        }
    }
}



