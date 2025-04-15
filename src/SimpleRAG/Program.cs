// See https://aka.ms/new-console-template for more information
using SimpleRAG;
using SimpleRAG.Processors;
using SimpleRAG.Services;
using System.Text;

var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "log.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(outputPath)
    .CreateLogger();

Log.Information("Starting the LLM RAG Console App");

Database.Initialize();

await HtmlProcessor.ProcessHtmlPage("https://gohugo.io/about/license/").ConfigureAwait(false);
await HtmlProcessor.ProcessHtmlPage("https://www.stevejgordon.co.uk/authenticating-a-github-app-using-a-json-web-token-in-dotnet").ConfigureAwait(false);
await MarkdownProcessor.ProcessMarkdownUrl("https://raw.githubusercontent.com/AngleSharp/AngleSharp/refs/heads/devel/README.md").ConfigureAwait(false);
await TextProcessor.ProcessFile(Path.Combine(Directory.GetCurrentDirectory(), "assets/book.txt")).ConfigureAwait(false);

while (true)
{
    Console.WriteLine("Ask a question (or type 'exit' to quit)");
    string question = Console.ReadLine() ?? string.Empty;


    if (question?.ToLower() == "exit")
    {
        Log.Information("[bold red]Exiting...[/]");
        break;
    }

    Console.WriteLine($"Answer: ");
    await foreach (string line in AskQuestionStream(question ?? throw new ArgumentException()))
    {
        Console.Write(line);
    }
    Console.WriteLine();
}

async IAsyncEnumerable<string> AskQuestionStream(string question)
{
    Log.Information($"[bold cyan]Processing question: {question}[/]");

    float[] queryEmbedding = await LLMService.GenerateEmbeddingAsync(question);
    var relevateChunks = Database.RetrieveRelevantChunks(queryEmbedding);

    var chunks = relevateChunks.ToArray();

    if (chunks.Length == 0) yield return "[bold red]No relevant information found.[/]";

    StringBuilder sb = new();
    foreach (var (title, text, processor) in chunks)
    {
        sb.AppendFormat("\n********\nChunk from embedding: [Type: {0}] [Title: {1}]\n\n {2}", processor, title, text);
    }

    string relevantText = string.Join("\n********\nChunk from embedding:\n\n", chunks);

    string prompt = $"Based on the following information from the knowledge base, answer the question:" +
        $"\n\"\"\"\n{sb.ToString()}\n\"\"\"\n" +
        $"Question: {question}";

    await foreach (var item in LLMService.GenerateResponseStreamAsync(prompt))
    {
        yield return item;
    }
}