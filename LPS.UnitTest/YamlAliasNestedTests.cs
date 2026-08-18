using LPS.Infrastructure.Common;
using LPS.UI.Common.DTOs;
using Xunit;

namespace LPS.UnitTest
{
    // Guards the fix where a parent type carrying a [YamlAlias] (e.g. iteration's delay)
    // must not strip alias support from its nested objects (e.g. httpRequest's url/method).
    public class YamlAliasNestedTests
    {
        [Fact]
        public void Iteration_DelayAlias_And_NestedRequestAliases_Resolve()
        {
            var yaml =
                "name: step1\n" +
                "delay: \"10000\"\n" +
                "httpRequest:\n" +
                "  url: https://x/\n" +
                "  method: GET\n" +
                "  headers:\n" +
                "    accept: application/json\n";

            var dto = SerializationHelper.DeserializeFromYaml<HttpIterationDto>(yaml);

            Assert.Equal("10000", dto.StartupDelay);
            Assert.Equal("https://x/", dto.HttpRequest.URL);
            Assert.Equal("GET", dto.HttpRequest.HttpMethod);
            Assert.Equal("application/json", dto.HttpRequest.HttpHeaders["accept"]);
        }

        [Fact]
        public void Iteration_CamelCaseNames_AlsoResolve()
        {
            var yaml =
                "name: step2\n" +
                "startupDelay: \"5\"\n" +
                "httpRequest:\n" +
                "  uRL: https://y/\n" +
                "  httpMethod: POST\n";

            var dto = SerializationHelper.DeserializeFromYaml<HttpIterationDto>(yaml);

            Assert.Equal("5", dto.StartupDelay);
            Assert.Equal("https://y/", dto.HttpRequest.URL);
            Assert.Equal("POST", dto.HttpRequest.HttpMethod);
        }

        [Fact]
        public void Iteration_RuleCollections_Resolve()
        {
            var yaml =
                "name: step3\n" +
                "failureRules:\n" +
                "  - metric: ErrorRate > 0.05\n" +
                "terminationRules:\n" +
                "  - metric: TotalTime.P90 > 2000\n" +
                "    gracePeriod: 00:00:05\n";

            var dto = SerializationHelper.DeserializeFromYaml<HttpIterationDto>(yaml);

            Assert.Equal("ErrorRate > 0.05", dto.FailureRules[0].Metric);
            Assert.Equal("TotalTime.P90 > 2000", dto.TerminationRules[0].Metric);
            Assert.Equal("00:00:05", dto.TerminationRules[0].GracePeriod);
        }

        [Fact]
        public void Request_NullHeaderValue_ResolvesAsEmptyString()
        {
            var yaml =
                "url: https://example.com\n" +
                "method: GET\n" +
                "headers:\n" +
                "  x-optional:\n";

            var dto = SerializationHelper.DeserializeFromYaml<HttpRequestDto>(yaml);

            Assert.Equal(string.Empty, dto.HttpHeaders["x-optional"]);
        }

        [Fact]
        public void Request_SerializesWithCanonicalAliases()
        {
            var dto = new HttpRequestDto
            {
                URL = "https://example.com",
                HttpMethod = "GET",
                HttpVersion = "1.1",
                HttpHeaders = new Dictionary<string, string> { ["accept"] = "application/json" }
            };

            var yaml = SerializationHelper.SerializeToYaml(dto);

            Assert.Contains("url:", yaml);
            Assert.Contains("method:", yaml);
            Assert.Contains("version:", yaml);
            Assert.Contains("headers:", yaml);
            Assert.DoesNotContain("uRL:", yaml);
            Assert.DoesNotContain("httpMethod:", yaml);
            Assert.DoesNotContain("httpVersion:", yaml);
            Assert.DoesNotContain("httpHeaders:", yaml);
        }

        [Fact]
        public void Serialization_DoesNotMutateInputCollections()
        {
            var dto = new HttpIterationDto
            {
                Name = "step4",
                HttpRequest = new HttpRequestDto
                {
                    URL = "https://example.com",
                    HttpMethod = "GET"
                }
            };

            SerializationHelper.SerializeToYaml(dto);

            Assert.NotNull(dto.FailureRules);
            Assert.NotNull(dto.TerminationRules);
            Assert.NotNull(dto.Before);
            Assert.NotNull(dto.After);
            Assert.NotNull(dto.HttpRequest.HttpHeaders);
        }

        [Fact]
        public void Iteration_StartupDelay_SerializesAsDelay_AndRoundTrips()
        {
            var dto = new HttpIterationDto { Name = "s", StartupDelay = "7942" };

            var yaml = SerializationHelper.SerializeToYaml(dto);

            Assert.Contains("delay:", yaml);
            Assert.DoesNotContain("startupDelay:", yaml);

            var back = SerializationHelper.DeserializeFromYaml<HttpIterationDto>(yaml);
            Assert.Equal("7942", back.StartupDelay);
        }
    }
}
