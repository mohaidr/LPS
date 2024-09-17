using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.LPSClient.EmbeddedResourcesServices
{
    using HtmlAgilityPack;
    using LPS.Domain.Common.Interfaces;
    using LPS.Infrastructure.Logger;
    using LPS.Infrastructure.LPSClients.EmbeddedResources;
    using LPS.Infrastructure.LPSClients.URLServices;
    using LPS.Infrastructure.Monitoring.Metrics;
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class HtmlResourceDownloaderService : IHtmlResourceDownloaderService
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _operationIdProvider;
        private readonly IUrlSanitizationService _urlSanitizationService;
        private readonly HttpClient _httpClient;

        public HtmlResourceDownloaderService(
            ILogger logger,
            IRuntimeOperationIdProvider operationIdProvider,
            IUrlSanitizationService urlSanitizationService,
            HttpClient httpClient)
        {
            _logger = logger;
            _operationIdProvider = operationIdProvider;
            _urlSanitizationService = urlSanitizationService;
            _httpClient = httpClient;
        }

        public async Task DownloadResourcesAsync(
            string baseUrl,
            string htmlFilePath,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.Log(_operationIdProvider.OperationId, $"Starting resource download for {htmlFilePath}", LPSLoggingLevel.Verbose, cancellationToken);

                // Read the HTML content from the saved file
                string htmlContent = await File.ReadAllTextAsync(htmlFilePath, cancellationToken);

                // Load the HTML into HtmlAgilityPack for parsing
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // Define XPath expressions for different resource types
                var resourceSelectors = new[]
                {
                "//img[@src]",
                "//link[@rel='stylesheet' and @href]",
                "//script[@src]"
            };

                var resourceUrls = resourceSelectors
                    .SelectMany(xpath => doc.DocumentNode.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>())
                    .Select(node =>
                    {
                        if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) ||
                            node.Name.Equals("script", StringComparison.OrdinalIgnoreCase))
                        {
                            return node.GetAttributeValue("src", null);
                        }
                        else if (node.Name.Equals("link", StringComparison.OrdinalIgnoreCase))
                        {
                            return node.GetAttributeValue("href", null);
                        }
                        return null;
                    })
                    .Where(url => !string.IsNullOrEmpty(url) && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                _logger.Log(_operationIdProvider.OperationId, $"Found {resourceUrls.Count} resources to download.", LPSLoggingLevel.Verbose, cancellationToken);

                // Directory to save downloaded resources
                string baseDirectory = Path.GetDirectoryName(htmlFilePath);
                string resourcesDirectory = Path.Combine(baseDirectory, "Resources");
                Directory.CreateDirectory(resourcesDirectory);

                // Download each resource
                foreach (var resourceUrl in resourceUrls)
                {
                    try
                    {
                        // Resolve relative URLs
                        Uri resourceUri = new Uri(new Uri(baseUrl), resourceUrl);

                        // Sanitize the URL to create a valid file name
                        string sanitizedUrl = _urlSanitizationService.Sanitize(resourceUri.AbsoluteUri);
                        string fileExtension = Path.GetExtension(resourceUri.AbsolutePath);
                        string fileName = $"{sanitizedUrl}{fileExtension}";
                        string filePath = Path.Combine(resourcesDirectory, fileName);

                        // Download the resource
                        byte[] resourceData = await _httpClient.GetByteArrayAsync(resourceUri, cancellationToken);

                        // Save to file
                        await File.WriteAllBytesAsync(filePath, resourceData, cancellationToken);

                        _logger.Log(_operationIdProvider.OperationId, $"Downloaded resource: {resourceUri}", LPSLoggingLevel.Verbose, cancellationToken);

                        // Optionally, update metrics
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(_operationIdProvider.OperationId, $"Failed to download resource {resourceUrl}: {ex.Message}", LPSLoggingLevel.Error, cancellationToken);
                        // Continue with other resources
                    }
                }

                _logger.Log(_operationIdProvider.OperationId, $"Completed resource download for {htmlFilePath}", LPSLoggingLevel.Verbose, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Log(_operationIdProvider.OperationId, $"Error in DownloadResourcesAsync: {ex.Message}", LPSLoggingLevel.Error, cancellationToken);
                // Depending on requirements, decide whether to rethrow or handle the exception
                throw;
            }
        }
    }

}
