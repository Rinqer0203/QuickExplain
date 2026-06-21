using QuickExplain.Models;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickExplain.Services.AiProviders
{
    internal sealed class OllamaProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;

        public OllamaProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public AiType Type => AiType.Ollama;

        public async Task StreamGenerateContentAsync(
            AiProviderRequest request,
            Action<string> onGetContent,
            Action<string> onStatus,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage)
        {
            try
            {
                var endpoint = new Uri(new Uri(AppConfig.Instance.OllamaBaseUrl.TrimEnd('/') + "/"), "api/chat");
                var body = CreateRequest(request);
                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(body, jsonOptions), Encoding.UTF8, "application/json");

                using var response = await _httpClient
                    .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    await ApiClients.SseStreamProcessor.HandleErrorAsync(response, onError);
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var hasContent = false;
                var thinkingFrame = 0;
                var lastThinkingStatusUpdate = DateTimeOffset.MinValue;
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        if (document.RootElement.TryGetProperty("message", out var message)
                            && message.TryGetProperty("thinking", out var thinking)
                            && !string.IsNullOrEmpty(thinking.GetString())
                            && !hasContent)
                        {
                            var now = DateTimeOffset.UtcNow;
                            if (now - lastThinkingStatusUpdate >= TimeSpan.FromMilliseconds(500))
                            {
                                thinkingFrame = (thinkingFrame + 1) % 4;
                                lastThinkingStatusUpdate = now;
                                onStatus($"考え中です{new string('.', thinkingFrame + 1)}");
                            }
                        }

                        if (document.RootElement.TryGetProperty("message", out message)
                            && message.TryGetProperty("content", out var content))
                        {
                            var text = content.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                hasContent = true;
                                onGetContent(text);
                            }
                        }

                        if (document.RootElement.TryGetProperty("done", out var done) && done.GetBoolean())
                        {
                            var usage = ExtractTokenUsage(document.RootElement);
                            if (usage != null)
                                onTokenUsage(usage);
                            break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        onError($"(Ollama レスポンスのパースに失敗しました: {ex.Message})");
                    }
                }
            }
            catch (UriFormatException)
            {
                onError("Ollama Base URL が正しくありません。設定を確認してください。");
            }
            catch (HttpRequestException)
            {
                onError("Ollama に接続できません。Ollama が起動しているか確認してください。");
            }
            catch (IOException)
            {
                onError("Ollama との通信が中断されました。Ollama の状態を確認してください。");
            }
        }

        private static Request CreateRequest(AiProviderRequest request)
        {
            var messages = new List<Message>(request.Messages.Count + 2)
            {
                new("system", request.SystemInstruction)
            };

            foreach (var (role, text) in request.Messages)
            {
                messages.Add(new Message(ConvertRole(role), text));
            }

            if (request.ImageBytes != null)
            {
                messages.Add(new Message(
                    "user",
                    string.Empty,
                    [Convert.ToBase64String(request.ImageBytes)]));
            }

            return new Request(
                request.ModelName,
                messages.ToArray(),
                KeepAlive: NormalizeKeepAlive(AppConfig.Instance.OllamaKeepAlive));
        }

        private static string NormalizeKeepAlive(string keepAlive)
        {
            return keepAlive == "-1" ? "-1m" : keepAlive;
        }

        private static string ConvertRole(string role)
        {
            return role == "user" ? "user" : "assistant";
        }

        private static TokenUsage? ExtractTokenUsage(JsonElement root)
        {
            var inputTokens = TryGetInt(root, "prompt_eval_count");
            var outputTokens = TryGetInt(root, "eval_count");
            int? totalTokens = inputTokens.HasValue || outputTokens.HasValue
                ? (inputTokens ?? 0) + (outputTokens ?? 0)
                : null;

            if (!inputTokens.HasValue && !outputTokens.HasValue && !totalTokens.HasValue)
                return null;

            return new TokenUsage(inputTokens, outputTokens, totalTokens);
        }

        private static int? TryGetInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
                ? value
                : null;
        }

        private sealed record Request(
            string Model,
            Message[] Messages,
            bool Stream = true,
            bool Think = false,
            [property: JsonPropertyName("keep_alive")] string KeepAlive = "5m");

        private sealed record Message(
            string Role,
            string Content,
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Images = null);
    }
}
