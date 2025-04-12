using Microsoft.Extensions.Configuration;

namespace SimpleRAG;

public static class Settings
{
    public const string SystemPrompt = @"
    You are an intelligent assistant for Simple RAG Corp.
    You are designed to provide helpful answers to user questions about
    your knowledge base.

    Instructions:
    - If you're unsure of an answer, say ""I don't know"" and recommend users search themselves.
    - Your response  should be complete.
    - Format the content so that it can be printed to the Command Line console.

";

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
