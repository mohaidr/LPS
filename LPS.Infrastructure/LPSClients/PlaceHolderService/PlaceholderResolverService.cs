using LPS.Domain.Common;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Caching;
using System;
using System.Text.RegularExpressions;
using System.Linq;

namespace LPS.Infrastructure.LPSClients.PlaceHolderService
{
    public partial class PlaceholderResolverService : IPlaceholderResolverService
    {
        private readonly ISessionManager _sessionManager;
        private readonly ICacheService<string> _memoryCacheService;

        public PlaceholderResolverService(ISessionManager sessionManager, ICacheService<string> memoryCacheService)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _memoryCacheService = memoryCacheService ?? throw new ArgumentNullException(nameof(memoryCacheService));
        }

        public string ResolvePlaceholders(string input, string idntifier)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Use a cache key unique to the input and clientId
            string cacheKey = $"Placeholder_{idntifier}_{input.GetHashCode()}";

            // Check if the result is already cached
            if (_memoryCacheService.TryGetItem(cacheKey, out string cachedResult))
            {
                return cachedResult;
            }

            // Perform placeholder resolution
            string resolvedInput = PerformResolution(input, idntifier);

            // Cache the resolved result
            _memoryCacheService.SetItemAsync(cacheKey, resolvedInput).Wait();

            return resolvedInput;
        }

        private string PerformResolution(string input, string identifier)
        {
            var matches = PathRegex().Matches(input);
            foreach (Match match in matches.Cast<Match>())
            {
                var variableName = match.Groups[1].Value; // Variable, e.g., "VariableName"
                var propertyPath = match.Groups[2].Value; // Path or regex, e.g., "item.item.subitem" or "/item/item/subitem"

                var response = _sessionManager.GetResponse(identifier, variableName);
                if (response == null)
                {
                    throw new InvalidOperationException($"Variable '{variableName}' not found in session.");
                }

                string resolvedValue = response.Format switch
                {
                    var format when format == MimeType.ApplicationJson.ToString() => response.ExtractJsonValue(propertyPath),
                    var format when format == MimeType.TextXml.ToString() ||
                                   format == MimeType.ApplicationXml.ToString() ||
                                   format == MimeType.RawXml.ToString() => response.ExtractXmlValue(propertyPath),
                    _ => response.ExtractRegexMatch(propertyPath),
                };

                input = input.Replace(match.Value, resolvedValue);
            }

            // Handle escaped placeholders (e.g., $$VariableName)
            input = input.Replace("$$", "$");

            return input;
        }

        [GeneratedRegex(@"(?<!\$)\$(\w+)\.([^\s]+)")]
        private static partial Regex PathRegex();
    }
}
