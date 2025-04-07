namespace SimpleRAG;

public class DocumentEmbed
{
    public List<Paragraph> Paragraphs { get; set; } = [];

    public Dictionary<string, string> Properties { get; set; } = [];

    public string this[string property] => Properties[property];

    public void AddParagraph(string text, float[] embedding)
    {
        Paragraphs.Add(new Paragraph(text, embedding));
    }
}

public record class Paragraph(string Text, float[] Embedding);