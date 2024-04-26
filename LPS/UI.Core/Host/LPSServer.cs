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
        static CancellationTokenSource _localCancellationTokenSource; //= CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
        public static int Port { get; private set; }

        public static void Initialize(ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            InitializeRouteHandlers();
        }

        private static void InitializeRouteHandlers()
        {
            _routeHandlers = new Dictionary<string, Func<string>>
            {
                { "/dashboard", () => GetDashboardResponse() },
                { "/kpis", () => GetKPIsResponse() }
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
        private static string GetKPIsResponse()
        {
            try
            {
                var responseTimeMetrics = LPSSerializationHelper.Serialize(LPSMetricsDataSource.Get(metric => metric.MetricType == LPSMetricType.ResponseTime).Select(metric => (LPSDurationMetricDimensionSet)metric.GetDimensionSet()));
                var responseBreakDownMetrics = LPSSerializationHelper.Serialize(LPSMetricsDataSource.Get(metric => metric.MetricType == LPSMetricType.ResponseCode).Select(metric => (ResponseCodeDimensionSet)metric.GetDimensionSet()));
                var connectionsMetric = LPSSerializationHelper.Serialize(LPSMetricsDataSource.Get(metric => metric.MetricType == LPSMetricType.ConnectionsCount).Select(metric => (ConnectionDimensionSet)metric.GetDimensionSet()));

                string responseMessage = FormatJson($"\n{{\"responseBreakDownMetrics\":{responseBreakDownMetrics}, \n\"responseTimeMetrics\":\n{responseTimeMetrics},\n\"connectionMetrics\":\n{connectionsMetric}\n}}");
                string response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\n\r\n{responseMessage}";

                return response;
            }
            catch (Exception ex)
            {
                // Handle file reading errors
                return $"HTTP/1.1 500 Internal Server Error\r\n\r\nError reading file: {ex.Message} {ex?.InnerException}";
            }
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
        public static async Task StartServerAsync(int port, CancellationTokenWrapper cancellationTokenWrapper)
        {
            _localCancellationTokenSource = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenWrapper.CancellationToken, _localCancellationTokenSource.Token);
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
                while (!linkedCts.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    await HandleClientAsync(client, cancellationTokenWrapper);
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
            if (_logger == null || _runtimeOperationIdProvider == null)
            {
                throw new InvalidOperationException("Server is not properly initialized.");
            }
            Random random = new Random();
            int dynamicPortNumber = random.Next(8000, 9001);
            Port = dynamicPortNumber;
            await StartServerAsync(dynamicPortNumber, cancellationTokenWrapper);
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationTokenWrapper cancellationTokenWrapper)
        {
            using (NetworkStream stream = client.GetStream())
            {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Read the HTTP request
                StringBuilder requestBuilder = new StringBuilder();
                string line;
                while ((line = await reader.ReadLineAsync(cancellationTokenWrapper.CancellationToken)) != string.Empty)
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
            _localCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(6));
            await Task.Delay(TimeSpan.FromSeconds(6));
        }
    }
}

