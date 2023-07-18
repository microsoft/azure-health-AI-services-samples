public class DocumentInput
{
    public DocumentInput(string id, string text, string language)
    {
        Id = id;
        Text = text;
        Language = language;
    }

    public string Id { get; set; }

    public string Text { get; set; }

    public string Language { get; set; }

}

