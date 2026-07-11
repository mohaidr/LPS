using System.Collections.Generic;
using LPS.Domain;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using LPS.UI.Common.DTOs;
using Xunit;

namespace LPS.UnitTest
{
    /// <summary>
    /// Tests for the before:/after: lifecycle-hook plumbing — DTO defaults, DTO deep-copy, and
    /// SetupCommand copy — for both the request and iteration scopes.
    /// </summary>
    public class BeforeAfterHooksTests
    {
        // ---------------- DTO defaults ----------------

        [Fact]
        public void HttpRequestDto_Constructor_InitializesBeforeAndAfterAsEmpty()
        {
            var dto = new HttpRequestDto();

            Assert.NotNull(dto.Before);
            Assert.Empty(dto.Before);
            Assert.NotNull(dto.After);
            Assert.Empty(dto.After);
        }

        [Fact]
        public void HttpIterationDto_Constructor_InitializesBeforeAndAfterAsEmpty()
        {
            var dto = new HttpIterationDto();

            Assert.NotNull(dto.Before);
            Assert.Empty(dto.Before);
            Assert.NotNull(dto.After);
            Assert.Empty(dto.After);
        }

        // ---------------- DTO deep copy ----------------

        [Fact]
        public void HttpRequestDto_DeepCopy_CopiesBeforeAndAfter()
        {
            var source = new HttpRequestDto
            {
                URL = "https://example.com",
                HttpMethod = "GET",
                Before = new List<string> { "$find(source=$a, variable=x)" },
                After = new List<string> { "$setvariable(variable=y, value=1)" }
            };

            source.DeepCopy(out HttpRequestDto target);

            Assert.Equal(source.Before, target.Before);
            Assert.Equal(source.After, target.After);
            // Ensure it is a copy, not the same reference.
            Assert.NotSame(source.Before, target.Before);
            Assert.NotSame(source.After, target.After);
        }

        [Fact]
        public void HttpIterationDto_DeepCopy_CopiesBeforeAndAfter()
        {
            var source = new HttpIterationDto
            {
                Name = "iter",
                Before = new List<string> { "$find(source=$a, variable=x)" },
                After = new List<string> { "$sum(source=[1,2], variable=total)" }
            };

            source.DeepCopy(out HttpIterationDto target);

            Assert.Equal(source.Before, target.Before);
            Assert.Equal(source.After, target.After);
            Assert.NotSame(source.Before, target.Before);
            Assert.NotSame(source.After, target.After);
        }

        // ---------------- SetupCommand copy ----------------

        [Fact]
        public void HttpRequestSetupCommand_Copy_CopiesBeforeAndAfter()
        {
            var source = new HttpRequest.SetupCommand
            {
                Url = new URL("https://example.com"),
                HttpMethod = "GET",
                HttpVersion = "2.0",
                Before = new List<string> { "$find(source=$a, variable=x)" },
                After = new List<string> { "$setvariable(variable=y, value=1)" }
            };
            var target = new HttpRequest.SetupCommand();

            source.Copy(target);

            Assert.Equal(source.Before, target.Before);
            Assert.Equal(source.After, target.After);
        }

        [Fact]
        public void HttpIterationSetupCommand_Copy_CopiesBeforeAndAfter()
        {
            var source = new HttpIteration.SetupCommand
            {
                Name = "iter",
                Before = new List<string> { "$find(source=$a, variable=x)" },
                After = new List<string> { "$sum(source=[1,2], variable=total)" },
                TerminationRules = new List<TerminationRule>(),
                FailureRules = new List<FailureRule>(),
                ValidationErrors = new Dictionary<string, List<string>>()
            };
            var target = new HttpIteration.SetupCommand();

            source.Copy(target);

            Assert.Equal(source.Before, target.Before);
            Assert.Equal(source.After, target.After);
        }
    }
}
