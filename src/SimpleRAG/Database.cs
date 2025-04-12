using DuckDB.NET.Data;
using Spectre.Console;
using System.Data;
using System.Text.Json;

namespace SimpleRAG;

public static class DuckDbExtensions
{
    public static void AddWithValue(this DuckDBParameterCollection parameterCollection, string parameterName, object value)
    {
        parameterCollection.Add(new DuckDBParameter(parameterName, value));
    }
}

public class Database
{

    static readonly string _createDbSql = @"
CREATE SEQUENCE IF NOT EXISTS Documents_Seq;

CREATE TABLE IF NOT EXISTS Documents (
    id INTEGER DEFAULT nextval('Documents_Seq') PRIMARY KEY,
    title TEXT,
    metadata JSON,
    source TEXT,
    processor TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    content TEXT
);

CREATE SEQUENCE IF NOT EXISTS embeddings_id_seq;

CREATE TABLE IF NOT EXISTS embeddings (
    id INTEGER DEFAULT nextval('embeddings_id_seq') PRIMARY KEY,
    text TEXT NOT NULL,
    embedding FLOAT[],
    document_id INTEGER,
    FOREIGN KEY(document_id) REFERENCES Documents(id)
);

CREATE SEQUENCE IF NOT EXISTS Chunks_Seq;

CREATE TABLE IF NOT EXISTS Chunks (
    id INTEGER DEFAULT nextval('Documents_Seq') PRIMARY KEY,
    document_id INTEGER,
    chunk_text TEXT,
    FOREIGN KEY(document_id) REFERENCES Documents(id)
);

";

    public static void Initialize()
    {
        // Ensure DuckDB database file exists
        if (!File.Exists(Settings.ConnectionString))
        {
            AnsiConsole.MarkupLine("Creating DuckDB database...");
        }

        using var connection = new DuckDBConnection(Settings.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = _createDbSql;
        command.ExecuteNonQuery();

        AnsiConsole.MarkupLine("DuckDB initialized and table created.");
    }

    static int InsertDocument(string title, Dictionary<string, string> metadata, string content, string source, string processor)
    {
        using var conn = new DuckDBConnection(Settings.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "INSERT INTO Documents (title, metadata, content, source, processor) VALUES ($title, $metadata, $content, $source, $processor) RETURNING id;";
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(metadata));
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("source", source);
        cmd.Parameters.AddWithValue("processor", processor);

        var result = cmd.ExecuteScalar();
        return result == null ? throw new InvalidOperationException("Failed to insert document and retrieve the ID.") : (int)result;
    }

    static void InsertEmbedding(int documentId, string text, float[] embedding)
    {
        using var connection = new DuckDBConnection(Settings.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO embeddings (text, embedding, document_id) VALUES ($text, $embedding, $document_id);
        ";
        command.Parameters.AddWithValue("text", text);
        command.Parameters.AddWithValue("embedding", embedding);
        command.Parameters.AddWithValue("document_id", documentId);
        command.ExecuteNonQuery();
    }

    public static int InsertDocument(DocumentEmbed document, string text, string source, string processor)
    {
        // Check if the document already exists in the database
        if (DocumentExists(source, processor))
        {
            AnsiConsole.MarkupLine("[bold yellow]Document already exists in the database. Skipping insertion.[/]");
            return -1; // Indicate that the document was not inserted
        }

        var title = document.Properties.ContainsKey("title") ? document.Properties["title"] : document.Properties["fileName"];
        var documentId = InsertDocument(title, document.Properties, text, source, processor);

        foreach (var item in document.Paragraphs)
        {
            InsertEmbedding(documentId, item.Text, item.Embedding);
        }

        return documentId;
    }

    public static bool DocumentExists(string source, string processor)
    {
        using var conn = new DuckDBConnection(Settings.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM Documents WHERE source = $source AND processor = $processor;";
        cmd.Parameters.AddWithValue("source", source);
        cmd.Parameters.AddWithValue("processor", processor);

        var result = cmd.ExecuteScalar();
        return result != null && (long)result > 0;
    }

    internal static string RetrieveRelevantText(float[] queryEmbedding, int topK = 3)
    {
        using var connection = new DuckDBConnection(Settings.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT text, embedding FROM embeddings
        ";

        using var reader = command.ExecuteReader();
        var results = new List<(string text, float similarity)>();

        while (reader.Read())
        {
            string text = reader.GetString(0);

            var floatList = (List<float>)reader["embedding"];  // DuckDB.NET returns List<float>
            float[] storedEmbedding = floatList.ToArray();

            float similarity = CosineSimilarity(queryEmbedding, storedEmbedding);
            results.Add((text, similarity));
        }

        // Sort results by similarity (descending) and return top K
        return string.Join("\n", results.OrderByDescending(r => r.similarity).Take(topK).Select(r => r.text));
    }

    static float CosineSimilarity(float[] vec1, float[] vec2)
    {
        double dot = 0.0, normA = 0.0, normB = 0.0;
        for (int i = 0; i < vec1.Length; i++)
        {
            dot += vec1[i] * vec2[i];
            normA += Math.Pow(vec1[i], 2);
            normB += Math.Pow(vec2[i], 2);
        }
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    public static IEnumerable<string> RetrieveRelevantChunks(float[] queryEmbedding, int topK = 3)
    {
        using var connection = new DuckDBConnection(Settings.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT text, embedding FROM embeddings
        ";

        using var reader = command.ExecuteReader();
        var results = new List<(string text, float similarity)>();

        while (reader.Read())
        {
            string text = reader.GetString(0);

            var floatList = (List<float>)reader["embedding"];  // DuckDB.NET returns List<float>
            float[] storedEmbedding = [.. floatList];


            //float[] storedEmbedding =  ConvertFromBlob((byte[])reader[1]);
            float similarity = CosineSimilarity(queryEmbedding, storedEmbedding);
            results.Add((text, similarity));
        }

        // Sort results by similarity (descending) and return top K
        return results.OrderByDescending(r => r.similarity).Take(topK).Select(r => r.text);
    }
}
