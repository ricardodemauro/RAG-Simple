﻿using SimpleRAG.Services;
using Spectre.Console;

namespace SimpleRAG.Processors;

public class TextProcessor
{
    static readonly int _maxChunkSize = 500; // Maximum size of a chunk in characters

    public async static Task<DocumentEmbed> ProcessFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine("[bold red]File not found![/]");
            throw new FileNotFoundException(filePath);
        }

        var documentEmbed = new DocumentEmbed();

        documentEmbed.Properties.Add("filePath", filePath);
        documentEmbed.Properties.Add("fileName", Path.GetFileName(filePath));

        string text = await File.ReadAllTextAsync(filePath);
        var chunks = ChunkText(text, ['.', '!', '?'], 700);

        AnsiConsole.MarkupLine("[bold yellow]Processing chunks...[/]");

#if SKIP_EMBEDDING
        var path = Path.Combine(Directory.GetCurrentDirectory(), "../../../documentEmbedding.json");
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


        Database.InsertDocument(documentEmbed, text);

        AnsiConsole.MarkupLine("[bold green]All chunks processed and stored in DuckDB.[/]");

        return documentEmbed;
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

    static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        var words = text.Split(' ');

        var currentChunk = string.Empty;
        foreach (var word in words)
        {
            if ((currentChunk + " " + word).Length > _maxChunkSize)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = word; // Start a new chunk
            }
            else
            {
                currentChunk += " " + word;
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
        {
            chunks.Add(currentChunk.Trim());
        }

        return chunks;
    }
}
