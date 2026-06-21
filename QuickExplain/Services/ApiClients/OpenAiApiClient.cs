using QuickExplain.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickExplain.Services.ApiClients
{
    internal class OpenAiApiClient : IOpenAiApiClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.openai.com/v1/";

        internal OpenAiApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task StreamGenerateContentAsync(
            string apiKey,
            OpenAiApiRequestModels.Request request,
            Action<string> onGetContent,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage,
            CancellationToken cancellationToken)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(request, jsonOptions), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await SseStreamProcessor.HandleErrorAsync(response, onError);
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await Task.Run(() => SseStreamProcessor.ProcessStreamAsync(
                stream,
                json => ExtractContentFromJson(json, onTokenUsage),
                onGetContent,
                onError,
                cancellationToken));
        }

        private static string? ExtractContentFromJson(string jsonPart, Action<TokenUsage> onTokenUsage)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonPart);

                var usage = ExtractTokenUsage(doc.RootElement);
                if (usage != null)
                    onTokenUsage(usage);

                if (!doc.RootElement.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                    return null;

                var delta = choices[0].GetProperty("delta");

                if (delta.TryGetProperty("content", out var contentProperty))
                    return contentProperty.GetString();

                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"パースエラー: {ex.Message}", ex);
            }
        }

        private static TokenUsage? ExtractTokenUsage(JsonElement root)
        {
            if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            var inputTokens = TryGetInt(usage, "prompt_tokens");
            var outputTokens = TryGetInt(usage, "completion_tokens");
            var totalTokens = TryGetInt(usage, "total_tokens");
            int? reasoningTokens = null;
            if (usage.TryGetProperty("completion_tokens_details", out var details))
                reasoningTokens = TryGetInt(details, "reasoning_tokens");

            return new TokenUsage(inputTokens, outputTokens, totalTokens, reasoningTokens);
        }

        private static int? TryGetInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
                ? value
                : null;
        }
    }
}
