using SimpleRAG.Services;

namespace SimpleRAG.Processors;

public class MarkdownProcessor
{
    static readonly int _maxChunkSize = 500; // Maximum size of a chunk in characters

    const string PROCESSOR_NAME = "MarkdownProcessor";

    public async static Task ProcessMarkdownUrl(string url)
    {
        if (Database.DocumentExists(url, PROCESSOR_NAME))
        {
            Log.Information("[bold yellow]Markdown already processed. Skipping...[/]");
            return;
        }

        using var httpClient = new HttpClient();

        Log.Information("[bold yellow]Fetching HTML page...[/]");

        HttpResponseMessage response;
        try
        {
            using var rq = new HttpRequestMessage(HttpMethod.Get, url);
            rq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
            rq.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            rq.Headers.Add("Accept-Language", "en-US,en;q=0.5");
            rq.Headers.Add("Connection", "keep-alive");
            rq.Headers.Add("Upgrade-Insecure-Requests", "1");
            rq.Headers.Add("Cache-Control", "max-age=0");
            rq.Headers.Add("Pragma", "no-cache");
            rq.Headers.Add("TE", "Trailers");


            response = await httpClient.SendAsync(rq).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Log.Information("[bold red]Failed to fetch HTML page: {0}[/]", ex.Message);
            throw;
        }

        string htmlContent = await response.Content.ReadAsStringAsync();

        var documentEmbed = new DocumentEmbed();

        documentEmbed.Properties.Add("url", url);
        documentEmbed.Properties.Add("title", url);

        await ProcessDocument(htmlContent, url, documentEmbed).ConfigureAwait(false);
    }

    public async static Task ProcessMarkdownFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Information("[bold red]File not found![/]");
            throw new FileNotFoundException(filePath);
        }

        if (Database.DocumentExists(filePath, PROCESSOR_NAME))
        {
            Log.Information("[bold yellow]Markdown already processed. Skipping...[/]");
            return;
        }

        var documentEmbed = new DocumentEmbed();

        documentEmbed.Properties.Add("filePath", filePath);
        documentEmbed.Properties.Add("fileName", Path.GetFileName(filePath));

        string markdownText = await File.ReadAllTextAsync(filePath);

        await ProcessDocument(markdownText, filePath, documentEmbed).ConfigureAwait(false);
    }

    private static async Task ProcessDocument(string markdownText, string source, DocumentEmbed documentEmbed)
    {
        // Extract YAML header if present
        var yamlHeader = ExtractYamlHeader(markdownText);
        if (yamlHeader != null)
        {
            foreach (var kvp in yamlHeader)
            {
                documentEmbed.Properties.Add(kvp.Key, kvp.Value);
            }
        }

        string plainText = ConvertMarkdownToPlainText(markdownText);
        var chunks = ChunkText(plainText, ['.', '!', '?'], 700);

        Log.Information("[bold yellow]Processing markdown chunks...[/]");

#if SKIP_EMBEDDING
        var path = Path.Combine(Directory.GetCurrentDirectory(), "../../../markdown-embedding.json");
        var json = File.ReadAllText(path);
        var doc = System.Text.Json.JsonSerializer.Deserialize<DocumentEmbed>(json);
        documentEmbed = doc;
#else

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = await LLMService.GenerateEmbeddingAsync(chunk)
                .ConfigureAwait(false);

            documentEmbed.AddParagraph(chunk, embedding);

            if (i > 0 && i % 15 == 0)
            {
                Console.WriteLine("Waiting for 60 secs");
                await Task.Delay(60 * 1000);
                Console.WriteLine("Let's goooooo");
            }
        }
#endif

        Database.InsertDocument(documentEmbed, plainText, source, PROCESSOR_NAME);

        Log.Information("[bold green]All markdown chunks processed and stored in DuckDB.[/]");
    }

    static string ConvertMarkdownToPlainText(string markdownText)
    {
        // Use Markdig to convert Markdown to plain text
        // var pipeline = new MarkdownPipelineBuilder().Build();
        // var plainText = Markdown.ToPlainText(markdownText, pipeline);
        // return plainText;
        return markdownText;
    }

    static List<string> ChunkText(string text, char[] delimiters, int minChars = 500)
    {
        var chunks = new List<string>();
        var parts = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = string.Empty;
        foreach (var part in parts)
        {
            if ((currentChunk + part).Length > _maxChunkSize && currentChunk.Length >= minChars)
            {
                chunks.Add(currentChunk.TrimStart(['\r', '\n', ' ']).TrimEnd());
                currentChunk = part; // Start a new chunk
            }
            else
            {
                currentChunk += part;
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
        {
            chunks.Add(currentChunk.TrimStart(['\r', '\n', ' ']).TrimEnd());
        }

        return chunks;
    }

    static Dictionary<string, string> ExtractYamlHeader(string markdownText)
    {
        var yamlHeader = new Dictionary<string, string>();

        if (markdownText.StartsWith("---"))
        {
            var endOfYaml = markdownText.IndexOf("---", 3);
            if (endOfYaml > 0)
            {
                var yamlContent = markdownText[3..endOfYaml];
                var lines = yamlContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        yamlHeader[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
        }

        return yamlHeader;
    }
}