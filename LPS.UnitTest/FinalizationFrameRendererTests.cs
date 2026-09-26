using LPS.UI.Core.Host;
using Spectre.Console;
using Xunit.Abstractions;

namespace LPS.UnitTest
{
    public class FinalizationFrameRendererTests(ITestOutputHelper output)
    {
        [Theory]
        [InlineData(1)]
        [InlineData(18)]
        [InlineData(24)]
        [InlineData(40)]
        [InlineData(42)]
        [InlineData(80)]
        [InlineData(120)]
        [InlineData(160)]
        [InlineData(200)]
        [InlineData(220)]
        public void Render_FitsTerminalWithoutChangingHeight(int width)
        {
            var initialLines = Render(0, 0, width).Split('\n');

            foreach (var completion in new[] { 0.1, 0.3, 0.5, 0.72, 0.85, 1.0 })
            {
                var rendered = Render(completion, (int)(completion * 200), width);
                var lines = rendered.Split('\n');

                Assert.Equal(initialLines.Length, lines.Length);
                Assert.All(lines, line => Assert.True(line.Length <= width, $"Line exceeds width {width}: '{line}'"));
                Assert.All(rendered, character => Assert.InRange((int)character, 0, 127));
                Assert.All(lines.Take(2), line => Assert.True(string.IsNullOrWhiteSpace(line)));
            }
        }

        [Theory]
        [InlineData(40, "LPS")]
        [InlineData(80, "L - P {} S ^")]
        public void Render_UsesBannerFigletFont(int width, string text)
        {
            var expected = RenderContent(new FigletText(text), width).Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToArray();
            var rendered = Render(1, 0, width);
            var actual = rendered.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

            Assert.Equal(expected, actual.Take(expected.Length).Select(line => line.Trim()).ToArray());
            Assert.Contains("RUN COMPLETE", rendered);
            Assert.Contains("LOAD / PERFORM / STRESS", rendered);
            output.WriteLine(rendered);
        }

        [Theory]
        [InlineData(160)]
        [InlineData(200)]
        [InlineData(220)]
        public void Render_EnlargesOutlineWithoutRepeatingHorizontalStrokes(int width)
        {
            var original = RenderContent(new FigletText("L - P {} S ^"), width).Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            var artwork = Render(1, 0, width).Split('\n').Skip(3).Take(original.Length * 2).ToArray();

            for (var row = 0; row < original.Length; row++)
            {
                var expected = string.Concat(original[row].Trim().Select(character => new string(character, 2)));
                Assert.Equal(expected, artwork[row * 2 + 1].Trim());
                Assert.Equal(expected.Replace('_', ' ').Trim(), artwork[row * 2].Trim());
                Assert.DoesNotContain("_", artwork[row * 2]);
            }

            var left = artwork.Where(line => !string.IsNullOrWhiteSpace(line))
                .Min(line => line.Length - line.TrimStart().Length);
            Assert.Equal(new[]
            {
                "",
                "  __",
                "||  ||",
                "||  ||",
                "||  ||",
                "||  ||",
                "||  ||",
                "||  ||______",
                "||          ||",
                "||__________||"
            }, artwork.Take(10).Select(line => line.Substring(left, 14).TrimEnd()).ToArray());
        }

        [Theory]
        [InlineData(40)]
        [InlineData(80)]
        [InlineData(120)]
        [InlineData(160)]
        [InlineData(200)]
        [InlineData(220)]
        public void Render_CentersLogoAndCaptionsInTerminal(int width)
        {
            var lines = Render(1, 0, width).Split('\n');
            var artwork = lines.TakeWhile(line => !line.Contains("RUN COMPLETE"))
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            var left = artwork.Min(line => line.Length - line.TrimStart().Length);
            var right = width - artwork.Max(line => line.TrimEnd().Length);

            Assert.InRange(Math.Abs(left - right), 0, 1);
            foreach (var caption in new[] { "RUN COMPLETE", "LOAD / PERFORM / STRESS" })
            {
                var line = lines.Single(line => line.Contains(caption));
                Assert.InRange(Math.Abs(line.IndexOf(caption) - (width - caption.Length) / 2), 0, 1);
            }
        }

        [Fact]
        public void Render_AnimatesThenSettles()
        {
            var opening = Render(0.05, 0);
            var drifting = Render(0.05, 12);
            var assembling = Render(0.4, 80);
            var settled = Render(0.74, 148);

            Assert.NotEqual(opening, drifting);
            Assert.NotEqual(drifting, assembling);
            Assert.NotEqual(assembling, settled);
            Assert.Equal(settled, Render(0.95, 190));
            Assert.Equal(Render(1, 200), Render(1, 240));
            Assert.DoesNotContain("RUN COMPLETE", settled);
            output.WriteLine($"OPENING\n{opening}\nASSEMBLING\n{assembling}\nFINISHED\n{Render(1, 200)}");
        }

        [Fact]
        public void Render_KeepsHighlightMovingWhileShutdownIsPending()
        {
            var previous = RenderContent(FinalizationFrameRenderer.Render(0.95, 160, 80), 80, true);
            for (var frame = 162; frame < 240; frame += 2)
            {
                var current = RenderContent(FinalizationFrameRenderer.Render(0.95, frame, 80), 80, true);
                Assert.NotEqual(previous, current);
                Assert.DoesNotContain("RUN COMPLETE", current);
                previous = current;
            }
            Assert.Equal(Render(1, 8), Render(1, 20));
        }

        private static string Render(double completion, int frame, int width = 80)
            => RenderContent(FinalizationFrameRenderer.Render(completion, frame, width), width);

        private static string RenderContent(Spectre.Console.Rendering.IRenderable content, int width, bool ansi = false)
        {
            using var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = ansi ? AnsiSupport.Yes : AnsiSupport.No,
                ColorSystem = ansi ? ColorSystemSupport.TrueColor : ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer)
            });
            console.Profile.Width = width;
            console.Write(content);
            return writer.ToString().Replace("\r\n", "\n");
        }
    }
}