using QuickExplain.Models;

namespace QuickExplain.Services
{
    public sealed record AiRequestResult(string Text, TokenUsage? TokenUsage, bool Success, bool IsCanceled);
}
