using OpenAI.Chat;
using OpenAI.Embeddings;
using System.ClientModel;

namespace SimpleRAG.Services;

public class LLMService
{
    static EmbeddingClient EmbeddingClient => new(Settings.EmbeddingModel, new ApiKeyCredential(Settings.GithubToken), new()
    {
        Endpoint = new Uri(Settings.LLMEndpoint)
    });

    static ChatClient ChatClient => new(Settings.LLMModel, new ApiKeyCredential(Settings.GithubToken), new()
    {
        Endpoint = new Uri(Settings.LLMEndpoint)
    });


    public static async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embedingResult = await EmbeddingClient.GenerateEmbeddingAsync(text);

        return embedingResult.Value.ToFloats().ToArray();
    }

    internal static async Task<string> GenerateResponseAsync(string prompt)
    {
        var response = await CompleteChat(prompt);

        return response.Content[0].Text.ToString();
    }

    internal static async IAsyncEnumerable<string> GenerateResponseStreamAsync(string prompt)
    {
        await foreach (var item in CompleteStream(prompt))
        {
            if (item.ContentUpdate.Count > 0)
                yield return item.ContentUpdate[0].Text;
            else
                yield return "";
        }
    }

    static IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStream(string input)
    {
        List<ChatMessage> messages = new()
        {
            new SystemChatMessage(Settings.SystemPrompt),
            new UserChatMessage(input)
        };

        var requestOptions = new ChatCompletionOptions()
        {
            Temperature = Settings.Temperature,
            MaxOutputTokenCount = 4096
        };

        return ChatClient.CompleteChatStreamingAsync(messages, requestOptions);
    }

    static async Task<ChatCompletion> CompleteChat(string input)
    {
        List<ChatMessage> messages = new()
        {
            new SystemChatMessage(Settings.SystemPrompt),
            new UserChatMessage(input)
        };

        var requestOptions = new ChatCompletionOptions()
        {
            Temperature = Settings.Temperature,
            MaxOutputTokenCount = 4096
        };

        var response = await ChatClient.CompleteChatAsync(messages, requestOptions);
        return response.Value;
    }
}