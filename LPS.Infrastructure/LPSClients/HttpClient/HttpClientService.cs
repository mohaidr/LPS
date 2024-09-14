using HtmlAgilityPack;
using LPS.Domain;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientService : IClientService<HttpRequestProfile, HttpResponse>
    {
        private HttpClient httpClient;
        private ILogger _logger;
        private static int _clientNumber;
        public string Id { get; private set; }
        public string GuidId { get; private set; }
        private IClientConfiguration<HttpRequestProfile> _config;

        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private static readonly ConcurrentDictionary<string, IList<IMetricMonitor>> _metrics = new ConcurrentDictionary<string, IList<IMetricMonitor>>();
        CancellationTokenSource _cts;
        public HttpClientService(IClientConfiguration<HttpRequestProfile> config, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, CancellationTokenSource cts)
        {
            _config = config;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _cts = cts;
            SocketsHttpHandler socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).PooledConnectionLifetime,
                PooledConnectionIdleTimeout = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).MaxConnectionsPerServer,
                UseCookies = true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                EnableMultipleHttp2Connections = true,
            };
            httpClient = new HttpClient(socketsHandler)
            {
                DefaultRequestVersion = HttpVersion.Version20
            };
            httpClient.Timeout = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).Timeout;
            Id = Interlocked.Increment(ref _clientNumber).ToString();
            GuidId = Guid.NewGuid().ToString();
        }
        public async Task<HttpResponse> SendAsync(HttpRequestProfile lpsHttpRequestProfile)
        {

            _metrics.TryAdd(lpsHttpRequestProfile.Id.ToString(), MetricsDataMonitor.Get(metric => metric.LPSHttpRun.LPSHttpRequestProfile.Id == lpsHttpRequestProfile.Id));
            int sequenceNumber = lpsHttpRequestProfile.LastSequenceId;
            HttpResponse lpsHttpResponse;
            var requestUri = new Uri(lpsHttpRequestProfile.URL);
            Stopwatch stopWatch = Stopwatch.StartNew();
            try
            {
                var httpRequestMessage = ConstructRequestMessage(lpsHttpRequestProfile);

                await TryIncreaseConnectionsCount(lpsHttpRequestProfile);
                // restart in case StartNew adds unnecessary milliseconds becuase of JITing. See this https://stackoverflow.com/questions/14019510/calculate-the-execution-time-of-a-method
                stopWatch.Restart();
                var response = await ExecuteHttpRequestAsync(lpsHttpRequestProfile, httpRequestMessage);
                HttpResponse.SetupCommand responseCommand = await ProcessResponseAsync(response, lpsHttpRequestProfile, sequenceNumber);
                //This will only run if save response is set to true
                await ExtractAndDownloadHtmlResourcesAsync(sequenceNumber, responseCommand, lpsHttpRequestProfile, responseCommand.LocationToResponse, httpClient);
                responseCommand.ResponseTime = stopWatch.Elapsed; // set the response time after the complete payload is read.
                stopWatch.Stop();

                lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.LPSHttpRequestProfile = lpsHttpRequestProfile; // this will change when we implement IQueryable Repository where the domain can fetch, validate and update the property. We then pass the profile Id to through the command

                await TryUpdateResponseMetrics(lpsHttpRequestProfile, lpsHttpResponse);

                await TryDecreaseConnectionsCount(lpsHttpRequestProfile, response.IsSuccessStatusCode);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion}\n\tTotal Time: {responseCommand.ResponseTime.TotalMilliseconds} MS\n\tStatus Code: {(int)response.StatusCode} Reason: {response.StatusCode}\n\tResponse Body: {responseCommand.LocationToResponse}\n\tResponse Headers: {response.Headers}{response.Content.Headers}", LPSLoggingLevel.Verbose, _cts);

            }
            catch (Exception ex)
            {
                await TryDecreaseConnectionsCount(lpsHttpRequestProfile, false);

                HttpResponse.SetupCommand lpsResponseCommand = new HttpResponse.SetupCommand
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    LPSHttpRequestProfileId = lpsHttpRequestProfile.Id,
                    ResponseTime = stopWatch.Elapsed,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.LPSHttpRequestProfile = lpsHttpRequestProfile; // this will change when we implement IQueryable Repository where the domain can fetch, validate and update the property. We then pass the profile Id to through the command
                await TryUpdateResponseMetrics(lpsHttpRequestProfile, lpsHttpResponse);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, _cts);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, _cts);
                throw;
            }

            return lpsHttpResponse;
        }

        private HttpRequestMessage ConstructRequestMessage(HttpRequestProfile lpsHttpRequestProfile)
        {
            var httpRequestMessage = new HttpRequestMessage();
            httpRequestMessage.RequestUri = new Uri(lpsHttpRequestProfile.URL);
            httpRequestMessage.Method = new HttpMethod(lpsHttpRequestProfile.HttpMethod);
            bool supportsContent = lpsHttpRequestProfile.HttpMethod.ToLower() == "post" || lpsHttpRequestProfile.HttpMethod.ToLower() == "put" || lpsHttpRequestProfile.HttpMethod.ToLower() == "patch";
            httpRequestMessage.Version = GetHttpVersion(lpsHttpRequestProfile.Httpversion);
            httpRequestMessage.Content = supportsContent ? new StringContent(lpsHttpRequestProfile.Payload ?? string.Empty) : null;

            foreach (var header in lpsHttpRequestProfile.HttpHeaders)
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

                if (!new StringContent("").Headers.GetType().GetProperties().Any(property => property.Name.ToLower() == header.Key.ToLower().Replace("-", "")))
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

            return httpRequestMessage;
        }

        private async Task<HttpResponseMessage> ExecuteHttpRequestAsync(HttpRequestProfile lpsHttpRequestProfile, HttpRequestMessage httpRequestMessage)
        {
            var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
            return response;
        }

        private async Task<HttpResponse.SetupCommand> ProcessResponseAsync(HttpResponseMessage response, HttpRequestProfile lpsHttpRequestProfile, int sequenceNumber)
        {
            string locationToResponse = string.Empty;
            string contentType = response?.Content?.Headers?.ContentType?.MediaType;
            MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);

            using (Stream contentStream = await response.Content.ReadAsStreamAsync(_cts.Token))
            {
                var timeoutCts = new CancellationTokenSource();
                timeoutCts.CancelAfter(((ILPSHttpClientConfiguration<HttpRequestProfile>)_config).Timeout);
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                byte[] buffer = new byte[64000]; // Adjust the buffer size as needed and modify this logic to have queue of buffers and reuse them
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token)) > 0)
                {
                    // Process the response as needed
                    // Example: memoryStream.Write(buffer, 0, bytesRead);
                    if (lpsHttpRequestProfile.SaveResponse)
                    {
                        string fileExtension = mimeType.ToFileExtension();
                        string sanitizedUrl = SanitizeUrl(lpsHttpRequestProfile.URL);

                        string directoryName = $"{_runtimeOperationIdProvider.OperationId}.{sanitizedUrl}.Resources";
                        // Ensure the directory exists without an explicit check
                        Directory.CreateDirectory(directoryName);
                        locationToResponse = $"{directoryName}/{Id}.{sequenceNumber}.{lpsHttpRequestProfile.Id}.{sanitizedUrl}.http {response.Version} {fileExtension}";
                        // If saving the response to a file is required
                        using (FileStream fileStream = File.Create(locationToResponse))
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, linkedCts.Token);
                        }
                    }
                }
            }

            return new HttpResponse.SetupCommand
            {
                StatusCode = response.StatusCode,
                StatusMessage = response.ReasonPhrase,
                LocationToResponse = locationToResponse,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                ResponseContentHeaders = response.Content?.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                ResponseHeaders = response.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                ContentType = mimeType,
                LPSHttpRequestProfileId = lpsHttpRequestProfile.Id,
            };
        }

        private string SanitizeUrl(string url)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            return string.Join("-", url.Replace("https://", "").Split(invalidChars.ToCharArray()));
        }

        private static Version GetHttpVersion(string version)
        {
            if (version == "1.0")
                return HttpVersion.Version10;
            else
                if (version == "1.1")
                return HttpVersion.Version11;
            else
                if (version == "2.0")
                return HttpVersion.Version20;

            return HttpVersion.Version20;
        }

        private void SetContentHeader(HttpRequestMessage message, string name, string value)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                return;

            switch (name.ToLower())
            {
                case "content-type":
                    message.Content.Headers.ContentType = new MediaTypeHeaderValue(value);
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
                    message.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(value);
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

        private async Task ExtractAndDownloadHtmlResourcesAsync(int sequenceNumber, HttpResponse.SetupCommand responseCommand, HttpRequestProfile lpsHttpRequestProfile, string locationToResponse, HttpClient client)
        {
            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && lpsHttpRequestProfile.Id == responseCommand.LPSHttpRequestProfileId && lpsHttpRequestProfile.SaveResponse && lpsHttpRequestProfile.DownloadHtmlEmbeddedResources)
                {
                    var timeoutCts = new CancellationTokenSource();
                    timeoutCts.CancelAfter(((ILPSHttpClientConfiguration<HttpRequestProfile>)_config).Timeout);
                    var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Downloading Embedded Resources - Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion}", LPSLoggingLevel.Verbose, _cts);
                    string htmlContent = await File.ReadAllTextAsync(locationToResponse, Encoding.UTF8);
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(htmlContent);
                    string baseUrl = lpsHttpRequestProfile.URL;
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
                                    byte[] resourceData = await client.GetByteArrayAsync(new Uri(new Uri(baseUrl), resourceUrl), linkedCts.Token);
                                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Downloaded: {resourceUrl}", LPSLoggingLevel.Verbose, _cts);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Faild to extract and download html resources\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, _cts);
            }
        }



        private async Task<bool> TryIncreaseConnectionsCount(HttpRequestProfile lpsHttpRequestProfile)
        {
            try
            {
                var connectionsMetrics = GetConnectionsMetrics(lpsHttpRequestProfile);

                foreach (var metric in connectionsMetrics)
                {
                    ((IThroughputMetricMonitor)metric).IncreaseConnectionsCount();
                }
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                                       $"Failed to increase connections metrics\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}",
                                       LPSLoggingLevel.Error, _cts);
                return false;
            }
        }

        private async Task<bool> TryDecreaseConnectionsCount(HttpRequestProfile lpsHttpRequestProfile, bool isSuccessful)
        {
            try
            {
                var connectionsMetrics = GetConnectionsMetrics(lpsHttpRequestProfile);
                foreach (var metric in connectionsMetrics)
                {
                    ((IThroughputMetricMonitor)metric).DecreseConnectionsCount(isSuccessful);
                }
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                                       $"Failed to decrease connections metrics\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}",
                                       LPSLoggingLevel.Error, _cts);
                return false;
            }
        }

        private IEnumerable<IMetricMonitor> GetConnectionsMetrics(HttpRequestProfile lpsHttpRequestProfile)
        {
            return _metrics[lpsHttpRequestProfile.Id.ToString()]
                    .Where(metric => metric.MetricType == LPSMetricType.ConnectionsCount);
        }

        private async Task<bool> TryUpdateResponseMetrics(HttpRequestProfile lpsHttpRequestProfile, HttpResponse lpsResponse)
        {
            try
            {
                var responsMetrics = _metrics[lpsHttpRequestProfile.Id.ToString()].Where(metric => metric.MetricType == LPSMetricType.ResponseTime || metric.MetricType == LPSMetricType.ResponseCode);
                await Task.WhenAll(responsMetrics.Select(metric => ((IResponseMetric)metric).UpdateAsync(lpsResponse)));
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to update connections metrics\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, _cts);
                return false;
            }

        }
    }
}



