namespace QuickExplain.Models
{
    public sealed record TokenUsage(
        int? InputTokens,
        int? OutputTokens,
        int? TotalTokens,
        int? ReasoningTokens = null)
    {
        public string ToDisplayText()
        {
            if (!InputTokens.HasValue && !OutputTokens.HasValue)
                return string.Empty;

            return $"tokens input {FormatTokenCount(InputTokens)} / output {FormatTokenCount(OutputTokens)}";
        }

        private static string FormatTokenCount(int? value)
        {
            return value.HasValue ? value.Value.ToString("N0") : "-";
        }
    }
}
