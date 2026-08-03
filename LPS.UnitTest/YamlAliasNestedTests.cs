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
