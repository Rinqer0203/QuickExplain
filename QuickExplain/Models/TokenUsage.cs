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
            var parts = new List<string>();
            if (InputTokens.HasValue)
                parts.Add($"入力 {InputTokens.Value:N0}");
            if (OutputTokens.HasValue)
                parts.Add($"出力 {OutputTokens.Value:N0}");
            if (ReasoningTokens.HasValue && ReasoningTokens.Value > 0)
                parts.Add($"推論 {ReasoningTokens.Value:N0}");
            if (TotalTokens.HasValue)
                parts.Add($"合計 {TotalTokens.Value:N0}");

            return parts.Count == 0
                ? string.Empty
                : $"Tokens: {string.Join(" / ", parts)}";
        }
    }
}
