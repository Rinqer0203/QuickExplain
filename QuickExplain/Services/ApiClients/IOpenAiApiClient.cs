using QuickExplain.Models;

namespace QuickExplain.Services.ApiClients
{
    public interface IOpenAiApiClient
    {
        Task StreamGenerateContentAsync(
            string apiKey,
            OpenAiApiRequestModels.Request request,
            Action<string> onGetContent,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage);
    }
}
