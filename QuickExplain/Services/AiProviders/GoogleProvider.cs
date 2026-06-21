using QuickExplain.Models;
using QuickExplain.Services.ApiClients;

namespace QuickExplain.Services.AiProviders
{
    internal sealed class GoogleProvider : IAiProvider
    {
        private readonly IGoogleApiClient _client;

        public GoogleProvider(IGoogleApiClient client)
        {
            _client = client;
        }

        public AiType Type => AiType.Google;

        public Task StreamGenerateContentAsync(
            AiProviderRequest request,
            Action<string> onGetContent,
            Action<string> onStatus,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage,
            CancellationToken cancellationToken)
        {
            var messages = request.Messages.ToArray();
            if (request.ImageBytes == null)
            {
                var body = GoogleApiRequestModels.CreateRequest(request.SystemInstruction, messages.AsSpan());
                return _client.StreamGenerateContentAsync(AppConfig.Instance.GoogleApiKey, body, request.ModelName, onGetContent, onError, onTokenUsage, cancellationToken);
            }

            var imageBase64 = Convert.ToBase64String(request.ImageBytes);
            var imageBody = GoogleApiRequestModels.CreateImageRequest(
                request.SystemInstruction,
                messages.AsSpan(),
                imageBase64,
                request.ImageMimeType);

            return _client.StreamGenerateContentAsync(AppConfig.Instance.GoogleApiKey, imageBody, request.ModelName, onGetContent, onError, onTokenUsage, cancellationToken);
        }
    }
}
