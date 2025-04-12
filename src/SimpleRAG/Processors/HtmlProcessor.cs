using SimpleRAG.Services;
using Spectre.Console;
using AngleSharp.Html.Parser;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Text;
using System.Net;

namespace SimpleRAG.Processors;

public partial class HtmlProcessor
{
    static readonly int _maxChunkSize = 500; // Maximum size of a chunk in characters

    public async static Task ProcessHtmlPage(string url)
    {
        if (Database.DocumentExists(url, "HtmlProcessor"))
        {
            AnsiConsole.MarkupLine("[bold yellow]URL already processed. Skipping...[/]");
            return;
        }

        using var httpClient = new HttpClient();

        AnsiConsole.MarkupLine("[bold yellow]Fetching HTML page...[/]");

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
            AnsiConsole.MarkupLine("[bold red]Failed to fetch HTML page: {0}[/]", ex.Message);
            throw;
        }

        string htmlContent = await response.Content.ReadAsStringAsync();

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(htmlContent));

        var metadata = ExtractMetadata(document);

        string title = document.Title ?? string.Empty;
        AnsiConsole.MarkupLine($"[bold green]Page Title: {title}[/]");

        string plainText = ConvertHtmlToPlainText(document);
        var chunks = ChunkText(plainText, ['.', '!', '?'], 700);

        var documentEmbed = new DocumentEmbed();
        documentEmbed.Properties.Add("url", url);

        if (!string.IsNullOrEmpty(title)) documentEmbed.Properties.Add("title", title);

        foreach (var item in metadata)
        {
            if (documentEmbed.Properties.ContainsKey(item.Key))
            {
                documentEmbed.Properties[item.Key] = item.Value; // Update existing property
            }
            else
            {
                documentEmbed.Properties.Add(item.Key, item.Value); // Add new property
            }
        }


        AnsiConsole.MarkupLine("[bold yellow]Processing HTML chunks...[/]");

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

        Database.InsertDocument(documentEmbed, plainText, url, "HtmlProcessor");

        AnsiConsole.MarkupLine("[bold green]All HTML chunks processed and stored in DuckDB.[/]");
    }

    static Dictionary<string, string> ExtractMetadata(IDocument document)
    {
        var metadata = new Dictionary<string, string>();

        // Query all <meta> tags
        var metaTags = document.QuerySelectorAll("meta");

        foreach (var meta in metaTags)
        {
            // Check for "name", "property", and "content" attributes
            var name = meta.GetAttribute("name");
            var property = meta.GetAttribute("property");
            var content = meta.GetAttribute("content");

            // Add the metadata to dictionary
            if (!string.IsNullOrEmpty(content))
            {
                if (!string.IsNullOrEmpty(name))
                {
                    metadata[name] = content;
                }
                else if (!string.IsNullOrEmpty(property))
                {
                    metadata[property] = content;
                }
            }
        }

        return metadata;
    }

    static string ExtractCleanedText(IDocument document)
    {
        // Remove unwanted tags
        CleanUnwantedElements(document);

        var htmlContent = document.Body?.TextContent ?? string.Empty;
        return NormalizeText(htmlContent);

        var sb = new StringBuilder();
        // Extract meta tags
        foreach (var meta in document.QuerySelectorAll("meta"))
        {
            var name = meta.GetAttribute("name") ?? meta.GetAttribute("property");
            var content = meta.GetAttribute("content");
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(content))
            {
                sb.AppendLine($"[Meta - {name}]: {content}");
            }
        }

        // Structured content extraction
        foreach (var element in document.Body?.Descendants() ?? Array.Empty<IElement>())
        {
            ProcessElement(sb, element);
        }

        return NormalizeText(sb.ToString());
    }

    private static void ProcessElement(StringBuilder sb, INode element)
    {
        if (element is IHtmlParagraphElement)
        {
            AppendBlock(sb, element.TextContent);
        }
        else if (element is IHtmlHeadingElement heading)
        {
            var count = 0;
            switch (heading.NodeName)
            {
                case "H1": count = 1; break;
                case "H2": count = 2; break;
                case "H3": count = 3; break;
                case "H4": count = 4; break;
                case "H5": count = 5; break;
                case "H6": count = 6; break;
            }
            AppendBlock(sb, $"{new string('#', count)} {heading.TextContent}");
        }
        else if (element is IHtmlAnchorElement a)
        {
            var text = a.TextContent.Trim();
            var href = a.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                AppendBlock(sb, $"[Link: {text}] {href}");
            }
        }
        else if (element.NodeName.ToLower() == "li")
        {
            AppendBlock(sb, $"- {element.TextContent.Trim()}");
        }
        else if (element.NodeName.ToLower() == "pre" || element.NodeName == "code")
        {
            AppendBlock(sb, "```\n" + element.TextContent.Trim() + "\n```");
        }
        else if (element.NodeName.ToLower() == "blockquote")
        {
            AppendBlock(sb, "> " + element.TextContent.Trim());
        }
        else if (element.NodeName.ToLower() == "div")
        {
            foreach (var item in element.Descendants())
            {
                ProcessElement(sb, item);
            }
        }
    }

    static void AppendBlock(StringBuilder sb, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            sb.AppendLine(text.Trim());
            sb.AppendLine(); // Blank line for readability
        }
    }

    static void CleanUnwantedElements(IDocument document)
    {
        string[] selectors =
        {
            "script",
            "style",
            "nav",
            "footer",
            "header",
            ".sidebar",
            ".ads",
            "noscript",
            "form",
            "[role=\"complementary\"]",
            "#footer-area",
            ".site-footer",
            ".footer-inner"
        };

        foreach (var selector in selectors)
        {
            var elements = document.QuerySelectorAll(selector);
            foreach (var el in elements)
            {
                el.Remove();
            }
        }
    }


    static string ConvertHtmlToPlainText(IDocument document)
    {
        return ExtractCleanedText(document);
    }

    static string NormalizeText(string input)
    {
        var cleaned = WebUtility.HtmlDecode(input); // handles entities
        cleaned = CollapseWhiteSpacesRegex().Replace(cleaned, " "); // collapse spaces
        cleaned = CollapseLineBreaksRegex().Replace(cleaned, "\n\n"); // collapse line breaks
        return cleaned.Trim();
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
                chunks.Add(currentChunk.Trim());
                currentChunk = part; // Start a new chunk
            }
            else
            {
                currentChunk += part;
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
        {
            chunks.Add(currentChunk.Trim());
        }

        return chunks;
    }

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex CollapseWhiteSpacesRegex();

    [GeneratedRegex(@"(\r?\n\s*){2,}")]
    private static partial Regex CollapseLineBreaksRegex();
}