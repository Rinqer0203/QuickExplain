using QuickExplain.Models;

namespace QuickExplain.Services.ApiClients
{
    public interface IGoogleApiClient
    {
        Task StreamGenerateContentAsync(
            string apiKey,
            GoogleApiRequestModels.Request request,
            string modelName,
            Action<string> onGetContent,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage,
            CancellationToken cancellationToken);
    }
}
