using QuickExplain.Models;
using System.Text;

namespace QuickExplain.Services.AiProviders
{
    internal sealed class DummyOllamaProvider : IAiProvider
    {
        public AiType Type => AiType.Ollama;

        public Task StreamGenerateContentAsync(
            AiProviderRequest request,
            Action<string> onGetContent,
            Action<string> onStatus,
            Action<string> onError,
            Action<TokenUsage> onTokenUsage,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                await Task.Delay(500, cancellationToken);

                var sb = new StringBuilder();
                sb.AppendLine("Dummy Ollama API Response:");
                sb.AppendLine($"Model: {request.ModelName}");
                sb.AppendLine($"System Instruction: {request.SystemInstruction}");
                sb.AppendLine("--------------------------");

                foreach (var (role, text) in request.Messages)
                {
                    sb.AppendLine($"{role} : {text}");
                }

                if (request.ImageBytes != null)
                    sb.AppendLine($"user : [画像:{request.ImageMimeType}, {request.ImageBytes.Length} bytes]");

                var fullText = sb.ToString();
                const int chunkSize = 10;
                for (var i = 0; i < fullText.Length; i += chunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    onGetContent(fullText.Substring(i, Math.Min(chunkSize, fullText.Length - i)));
                    await Task.Delay(50, cancellationToken);
                }

                onTokenUsage(new TokenUsage(120, 80, 200));
            });
        }
    }
}
