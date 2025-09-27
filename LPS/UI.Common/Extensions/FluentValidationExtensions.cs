using FluentValidation.Results;
using LPS.Domain.Common.Interfaces;

public static class FluentValidationExtensions
{
    public static void PrintValidationErrors(this ValidationResult validationResult, ILogger? logger = null)
    {
        var groupedErrors = validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToList()
            );

        foreach (var kv in groupedErrors)
        {
            // Console output
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{kv.Key}:");
            Console.ResetColor();

            // Logger output
            logger?.Log($"{kv.Key}: validation failed", LPSLoggingLevel.Error);

            foreach (var error in kv.Value)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"- {error}");
                Console.ResetColor();

                logger?.Log( error, LPSLoggingLevel.Error);
            }
        }
    }
}
