using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace SimpleRAG;

public static class Settings
{
    public const string SystemPrompt = "You are a helpful reading assistant who answers questions based " +
     "on the snippets of text provided in context. Answer only using the context provided, " +
     "being as concise as possible. If you are unsure, just say you don't know. Context: ";

    public const float Temperature = 0.7f;

    public static string GithubToken => Configuration["Github:Token"] ?? throw new ArgumentException();

    public static string ConnectionString
    {
        get
        {
            var db = Configuration["Database:EmbeddingDb"] ?? throw new ArgumentException();
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "../../../", db);

            return $"Data Source={dir}";
        }
    }

    public static string EmbeddingModel => Configuration["LLM:EmbeddingModel"] ?? throw new ArgumentException();

    public static string LLMModel => Configuration["LLM:Model"] ?? throw new ArgumentException();

    public static string LLMEndpoint => Configuration["LLM:Endpoint"] ?? throw new ArgumentException();

    static IConfiguration Configuration =>
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets("d82e25cc-cc7c-49df-a9a5-4f36803d7688")
            .Build();
}
