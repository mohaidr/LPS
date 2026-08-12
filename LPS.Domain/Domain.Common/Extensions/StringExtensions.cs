namespace LPS.Domain.Domain.Common.Extensions
{
    public static class StringExtensions
    {
        // A value is a placeholder when it references an unresolved variable (prefixed with '$').
        public static bool IsPlaceholder(this string? value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith("$");
    }
}
