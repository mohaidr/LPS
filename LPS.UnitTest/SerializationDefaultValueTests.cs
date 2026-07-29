using System.Collections.Generic;
using LPS.Infrastructure.Common;
using LPS.UI.Common.DTOs;
using Xunit;

namespace LPS.UnitTest
{
    public class SerializationDefaultValueTests
    {
        private static PlanDto BuildPlan(string? httpVersion = null, string? supportH2C = null)
        {
            var request = new HttpRequestDto
            {
                URL = "https://example.com",
                HttpMethod = "GET"
            };

            if (httpVersion != null) request.HttpVersion = httpVersion;
            if (supportH2C != null) request.SupportH2C = supportH2C;

            return new PlanDto
            {
                Name = "T",
                Rounds = new List<RoundDto>
                {
                    new RoundDto
                    {
                        Name = "R",
                        Iterations = new List<HttpIterationDto>
                        {
                            new HttpIterationDto { Name = "I", HttpRequest = request }
                        }
                    }
                }
            };
        }

        [Fact]
        public void SerializeToYaml_OmitsDefaultValuedProperties()
        {
            // HttpVersion defaults to "2.0" and SupportH2C to "false" via the DTO constructor.
            var yaml = SerializationHelper.SerializeToYaml(BuildPlan());

            Assert.DoesNotContain("httpVersion", yaml);
            Assert.DoesNotContain("supportH2C", yaml);
            Assert.Contains("example.com", yaml);
        }

        [Fact]
        public void SerializeToYaml_KeepsNonDefaultValues()
        {
            var yaml = SerializationHelper.SerializeToYaml(BuildPlan(httpVersion: "1.1", supportH2C: "true"));

            Assert.Contains("1.1", yaml);
            Assert.Contains("supportH2C", yaml);
        }
    }
}
