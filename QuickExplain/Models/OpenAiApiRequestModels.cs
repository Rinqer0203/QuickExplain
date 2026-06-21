using System.Collections.Generic;

namespace QuickExplain.Models
{
    public class OpenAiApiRequestModels
    {
        public record Message(string role, object content);
        public record StreamOptions(bool include_usage = true);
        public record Request(string model, Message[] messages, bool stream = true, StreamOptions? stream_options = null);
        public record ImageUrl(string url);
        public record ContentPart(string type, string? text = null, ImageUrl? image_url = null);

        public static Request CreateRequest(string modelName, string instruction, ReadOnlySpan<(string role, string text)> messages)
        {
            int messageCount = messages.Length + 1;
            Message[] messageArray = new Message[messageCount];

            // システムメッセージを設定
            messageArray[0] = new Message("system", instruction);

            for (int i = 0; i < messages.Length; i++)
            {
                var (role, text) = messages[i];
                messageArray[i + 1] = new Message(ConvertRole(role), text);
            }

            return new Request(modelName, messageArray, stream_options: new StreamOptions());
        }

        /// <summary>
        /// OpenAI APIのロールに変換する
        /// </summary>
        private static string ConvertRole(string role)
        {
            if (role == "user")
                return "user";
            return "system";
        }
    }
}
