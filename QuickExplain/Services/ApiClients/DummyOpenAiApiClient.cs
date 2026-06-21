using QuickExplain.Models;
using System.Text;

namespace QuickExplain.Services.ApiClients
{
    internal class DummyOpenAiApiClient : IOpenAiApiClient
    {
        Task IOpenAiApiClient.StreamGenerateContentAsync(
            string apiKey,
            OpenAiApiRequestModels.Request request,
            Action<string> onGetContent,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                await Task.Delay(500, cancellationToken); // 初期ディレイ

                var sb = new StringBuilder();
                sb.AppendLine("Dummy OpenAI API Response:");
                sb.AppendLine($"Model: {request.model}");
                sb.AppendLine("Messages:");
                sb.AppendLine("--------------------------");

                foreach (var msg in request.messages)
                {
                    sb.AppendLine($"{msg.role} : {msg.content}");
                }

                string fullText = sb.ToString();
                int chunkSize = 10;

                for (int i = 0; i < fullText.Length; i += chunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string chunk = fullText.Substring(i, Math.Min(chunkSize, fullText.Length - i));
                    onGetContent.Invoke(chunk);
                    await Task.Delay(50, cancellationToken); // 擬似ストリーム間隔
                }

                onTokenUsage(new TokenUsage(140, 90, 230, 12));
            });
        }
    }
}
