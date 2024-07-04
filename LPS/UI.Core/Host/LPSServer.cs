using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LPS.Infrastructure.Common;
using LPS.Domain.Common.Interfaces;
using Spectre.Console;
using LPS.Infrastructure.Monitoring.Metrics;
using System.Linq;
using LPS.UI.Common;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using LPS.Infrastructure.Common.Interfaces;
using System.Threading;
using LPS.Domain;
using LPS.Domain.Domain.Common.Interfaces;
using System.Security.Cryptography;

//TODO: This Code Is writting by the help of chatGPT so please consider refactoring for it
namespace LPS.UI.Core.Host
{
    //TODO: If the server logic goes bigger, consider moving to a different solution
    internal static class LPSServer
    {
        private static TcpListener _listener;
        private static bool _isServerRunning = false;
        private static readonly object _lockObject = new object();
        private static ILPSLogger _logger;
        private static ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private static Dictionary<string, Func<string>> _routeHandlers;
        private static ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunCommandStatusMonitor;
        static CancellationTokenSource _cts;
        static CancellationToken _testCancellationToken;
        public static bool IsRunning { get; private set; }
        public static int Port { get; private set; }
        public static void Initialize(ILPSLogger logger, ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunCommandStatusMonitor, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider, CancellationToken testCancellationToken)
        {
            _logger = logger;
            _httpRunCommandStatusMonitor = httpRunCommandStatusMonitor;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _testCancellationToken = testCancellationToken;
            _cts = new CancellationTokenSource();
            InitializeRouteHandlers();
        }

        private static void InitializeRouteHandlers()
        {
            _routeHandlers = new Dictionary<string, Func<string>>
            {
                { "/dashboard", () => GetDashboardResponse() },
                { "/kpis", () => GetMetrics() }
            };
        }

        private static string GetDashboardResponse(string subPath = "")
        {
            if (string.IsNullOrEmpty(subPath))
            {
                subPath = "index.html";  // Default file to serve if no subpath is specified
            }

            // Compute the full path to the file within the dashboard directory
            string filePath = Path.Combine(LPSAppConstants.AppExecutableLocation, "dashboard", subPath);

            if (File.Exists(filePath))
            {
                try
                {
                    // Read the contents of the file
                    string content = File.ReadAllText(filePath);
                    string httpResponse = $"HTTP/1.1 200 OK\r\n" +
                                          "Content-Type: text/html\r\n" +
                                          $"Content-Length: {content.Length}\r\n" +
                                          "\r\n" +
                                          content;
                    return httpResponse;
                }
                catch (Exception ex)
                {
                    // Handle file reading errors
                    return $"HTTP/1.1 500 Internal Server Error\r\n\r\nError reading file: {ex.Message}";
                }
            }
            else
            {
                // Return 404 if the file doesn't exist
                return "HTTP/1.1 404 Not Found\r\n\r\nFile not found";
            }
        }

        private static string GetResponseForRequest(string urlPath)
        {
            // Check if the URL path starts with /dashboard and extract the subpath
            if (urlPath.StartsWith("/dashboard"))
            {
                // Remove the '/dashboard' part and pass the rest to the GetDashboardResponse
                string subPath = urlPath.Substring("/dashboard".Length).TrimStart('/');
                if (string.IsNullOrWhiteSpace(subPath))
                {
                    // If there is no subpath, default to serving the index.html file
                    subPath = "index.html";  // Adjust this line to dynamically select any index.* file if needed
                }
                return GetDashboardResponse(subPath);
            }

            foreach (var routeHandler in _routeHandlers)
            {
                if (urlPath.Contains(routeHandler.Key))
                {
                    return routeHandler.Value();
                }
            }

            return "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nNot Found";
        }
        private class MetricData
        {
            public string Endpoint { get; set; }
            public string ExecutionStatus { get; set; }
            public object ResponseBreakDownMetrics { get; set; }
            public object ResponseTimeMetrics { get; set; }
            public object ConnectionMetrics { get; set; }
        }

      

        private static string GetMetrics()
        {
            try
            {
                // Create a list to store metric data
                var metricsList = new List<MetricData>();

                // Define a local function to safely obtain endpoint details from a metric dimension set
                string GetEndPointDetails(IDimensionSet dimensionSet)
                {
                    if (dimensionSet is LPSDurationMetricDimensionSet durationSet)
                        return $"{durationSet.RunName} {durationSet.HttpMethod} {durationSet.URL} HTTP/{durationSet.HttpVersion}";
                    else if (dimensionSet is ResponseCodeDimensionSet responseSet)
                        return $"{responseSet.RunName} {responseSet.HttpMethod} {responseSet.URL} HTTP/{responseSet.HttpVersion}";
                    else if (dimensionSet is ConnectionDimensionSet connectionSet)
                        return $"{connectionSet.RunName} {connectionSet.HttpMethod} {connectionSet.URL} HTTP/{connectionSet.HttpVersion}";

                    return null; // or a default string if suitable
                }

                // Helper action to add metrics to the list
                Action<IEnumerable<dynamic>, string> addToList = (metrics, type) =>
                {
                    foreach (var metric in metrics)
                    {
                        var endPointDetails = GetEndPointDetails(metric.GetDimensionSet());
                        if (endPointDetails == null)
                            continue; // Skip metrics where endpoint details are not applicable or available

                        var statusList = _httpRunCommandStatusMonitor.GetAllStatuses(((ILPSMetricMonitor)metric).LPSHttpRun);
                        string status = DetermineOverallStatus(statusList);
                        bool isCancelled = status != "Completed" && status != "Failed" && _testCancellationToken.IsCancellationRequested;
                        if (isCancelled)
                        {
                            status = "Cancelled";
                        }

                        var metricData = metricsList.FirstOrDefault(m => m.Endpoint == endPointDetails);
                        if (metricData == null)
                        {
                            metricData = new MetricData
                            {
                                ExecutionStatus = status,
                                Endpoint = endPointDetails
                            };
                            metricsList.Add(metricData);
                        }
                        else 
                        if (metricData.ExecutionStatus != status)
                        {
                            metricData.ExecutionStatus = status;
                        }

                        switch (type)
                        {
                            case "ResponseTime":
                                metricData.ResponseTimeMetrics = metric.GetDimensionSet();
                                break;
                            case "ResponseCode":
                                metricData.ResponseBreakDownMetrics = metric.GetDimensionSet();
                                break;
                            case "ConnectionsCount":
                                metricData.ConnectionMetrics = metric.GetDimensionSet();
                                break;
                        }
                    }
                };
                // Fetch metrics by type
                var responseTimeMetrics = LPSMetricsDataMonitor.Get(metric => metric.MetricType == LPSMetricType.ResponseTime);
                var responseBreakDownMetrics = LPSMetricsDataMonitor.Get(metric => metric.MetricType == LPSMetricType.ResponseCode);
                var connectionsMetrics = LPSMetricsDataMonitor.Get(metric => metric.MetricType == LPSMetricType.ConnectionsCount);

                // Populate the dictionary
                addToList(responseTimeMetrics, "ResponseTime");
                addToList(responseBreakDownMetrics, "ResponseCode");
                addToList(connectionsMetrics, "ConnectionsCount");

                // Convert metrics list to JSON or any other required format
                string jsonResponse= JsonConvert.SerializeObject(metricsList, Formatting.Indented);

                // Format the HTTP response
                string httpResponse = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\n\r\n{jsonResponse}";
                return httpResponse;
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                return $"Error: {ex.Message}";
            }
        }


        private static string DetermineOverallStatus(List<AsyncCommandStatus> statuses)
        {
            if (statuses.Count == 0)
                return "NotRunning";
            if (statuses.All(status => status == AsyncCommandStatus.NotStarted))
                return "NotStarted";
            if (statuses.All(status => status == AsyncCommandStatus.Completed))
                return "Completed";
            if (statuses.All(status => status == AsyncCommandStatus.Completed || status == AsyncCommandStatus.Paused) && statuses.Any(status => status == AsyncCommandStatus.Paused))
                return "Paused";
            if (statuses.All(status => status == AsyncCommandStatus.Completed || status == AsyncCommandStatus.Failed) && statuses.Any(status => status == AsyncCommandStatus.Failed))
                return "Failed";
            if (statuses.Any(status => status == AsyncCommandStatus.Ongoing))
                return "Ongoing";

            return "Undefined"; // Default case, should ideally never be reached
        }

        private static string FormatJson(string json)
        {
            try
            {
                dynamic parsedJson = JToken.Parse(json);
                return parsedJson.ToString(Formatting.Indented);
            }
            catch (JsonReaderException)
            {
                return json;
            }
        }
        public static async Task StartServerAsync(int port)
        {
            lock (_lockObject)
            {
                if (_isServerRunning)
                {
                    AnsiConsole.MarkupLine("[red]Server is already running![/]");
                    return;
                }

                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _isServerRunning = true;
                AnsiConsole.MarkupLine($"[CornflowerBlue][bold][italic]Stats Server started and listening on port {port}[/][/][/]");
            }

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    await HandleClientAsync(client, _cts.Token);
                }
            }
            finally
            {
                _listener.Stop();
            }
        }

        private static string GetUrlPathFromRequest(string request)
        {
            // Assuming the request format is:
            // GET /path/to/resource HTTP/1.1
            // or
            // GET /path/to/resource HTTP/1.0
            // This method extracts the URL path from the request

            string[] lines = request.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length > 0)
            {
                string[] parts = lines[0].Split(' ');
                if (parts.Length >= 2 && parts[0] == "GET")
                {
                    return parts[1]; // The second part should be the URL path
                }
            }
            return null;
        }

        

        public static async Task RunAsync(CancellationTokenWrapper cancellationTokenWrapper)
        {
            IsRunning = true;
            if (_logger == null || _runtimeOperationIdProvider == null)
            {
                throw new InvalidOperationException("Server is not properly initialized.");
            }
            Random random = new Random();
            int dynamicPortNumber = random.Next(8000, 9001);
            Port = dynamicPortNumber;
            await StartServerAsync(dynamicPortNumber);
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (NetworkStream stream = client.GetStream())
            {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Read the HTTP request
                StringBuilder requestBuilder = new StringBuilder();
                string line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != string.Empty)
                {
                    requestBuilder.AppendLine(line);
                }
                string request = requestBuilder.ToString();

                // Extract URL path from the request
                string urlPath = GetUrlPathFromRequest(request);

                string response = GetResponseForRequest(urlPath);

                await writer.WriteAsync(response).ConfigureAwait(false);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"\n{request}\n{response}", LPSLoggingLevel.Information);
            }

            client.Close();
        }
        public static async Task ShutdownServerAsync()
        {
            IsRunning = false;
            _cts.CancelAfter(TimeSpan.FromSeconds(6));
            await Task.Delay(TimeSpan.FromSeconds(6));
        }
    }
}

