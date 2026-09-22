using Spectre.Console;
using System.IO;
using System.Text;

namespace LPS.UI.Core.Host
{
    internal static class FinalizationFrameRenderer
    {
        private static readonly string[] LargeFinalizationLogo = CreateFigletLogo("L -- P {} S ^", 2);
        private static readonly string[] FinalizationLogo = CreateFigletLogo("L -- P {} S ^");
        private static readonly string[] CompactFinalizationLogo = CreateFigletLogo("LPS");

        internal static Rows Render(double completion, int frame, int terminalWidth)
        {
            var width = Math.Clamp(terminalWidth, 1, 180);
            var caption = completion >= 1 ? "RUN COMPLETE" : "FINALIZING";
            var logo = width >= LargeFinalizationLogo[0].Length + 4 ? LargeFinalizationLogo
                : width >= FinalizationLogo[0].Length + 4 ? FinalizationLogo : CompactFinalizationLogo;
            if (width < Math.Max(24, logo[0].Length))
            {
                return new Rows(
                    new Text(string.Empty),
                    new Text(string.Empty),
                    Align.Center(new Text("LPS"[..Math.Min(3, width)])),
                    Align.Center(new Text(caption[..Math.Min(caption.Length, width)])));
            }

            var height = logo.Length + 2;
            var logoWidth = logo[0].Length;
            var left = (width - logoWidth) / 2;
            var characters = new char[height, width];
            var shades = new int[height, width];
            var occupiedColumns = Enumerable.Range(0, logoWidth)
                .Where(column => logo.Any(line => line[column] != ' ')).ToArray();
            var sweep = occupiedColumns[frame / 2 % occupiedColumns.Length];

            for (var logoRow = 0; logoRow < logo.Length; logoRow++)
            {
                for (var logoColumn = 0; logoColumn < logo[logoRow].Length; logoColumn++)
                {
                    if (logo[logoRow][logoColumn] == ' ')
                    {
                        continue;
                    }

                    var seed = logoRow * logoWidth + logoColumn;
                    var targetColumn = left + logoColumn;
                    var targetRow = logoRow + 1;
                    var startColumn = (seed * 17 + 11) % width;
                    var startRow = (seed * 7 + 3) % height;
                    var departure = 0.08 + (seed * 13 % 100) / 100d * 0.12;
                    var arrival = 0.48 + (seed * 37 % 100) / 100d * 0.24;
                    var travel = Math.Clamp((completion - departure) / (arrival - departure), 0, 1);
                    var eased = 1 - Math.Pow(1 - travel, 3);
                    var drift = Math.Sin(frame * 0.12 + seed) * (1 - eased);
                    var column = Math.Clamp((int)Math.Round(startColumn + (targetColumn - startColumn) * eased + drift * 3), 0, width - 1);
                    var row = Math.Clamp((int)Math.Round(startRow + (targetRow - startRow) * eased + drift), 0, height - 1);
                    var shade = travel >= 1 ? 3 : travel > 0.25 ? 2 : 1;

                    if (completion >= 0.74 && completion < 1 && Math.Abs(targetColumn - left - sweep) < 2)
                    {
                        shade = 4;
                    }

                    if (shade >= shades[row, column])
                    {
                        characters[row, column] = logo[logoRow][logoColumn];
                        shades[row, column] = shade;
                    }
                }
            }

            string[] styles = { "", "grey", "cyan", "green", "bold white" };
            var artwork = new StringBuilder();
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    var shade = shades[row, column];
                    if (shade == 0)
                    {
                        artwork.Append(' ');
                    }
                    else
                    {
                        artwork.Append('[').Append(styles[shade]).Append(']')
                            .Append(characters[row, column]).Append("[/]");
                    }
                }

                if (row < height - 1)
                {
                    artwork.AppendLine();
                }
            }

            const string signature = "LOAD / PERFORM / STRESS";
            return new Rows(
                new Text(string.Empty),
                new Text(string.Empty),
                Align.Center(new Rows(
                    new Markup(artwork.ToString()),
                    new Markup($"[{(completion >= 1 ? "green" : "bold grey")}]{caption}[/]"),
                    new Markup($"[grey]{signature}[/]"),
                    new Text(string.Empty))));
        }

        private static string[] CreateFigletLogo(string text, int scale = 1)
        {
            using var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer)
            });
            console.Profile.Width = 200;
            console.Write(new FigletText(text));
            var lines = writer.ToString().Replace("\r\n", "\n").Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            var left = lines.Min(line => line.Length - line.TrimStart().Length);
            var right = lines.Max(line => line.TrimEnd().Length);
            return lines
                .Select(line => string.Concat(line.PadRight(right)[left..right]
                    .Select(character => new string(character, scale))))
                .SelectMany(line => Enumerable.Range(0, scale)
                    .Select(row => row == scale - 1 ? line : line.Replace('_', ' '))).ToArray();
        }
    }
}