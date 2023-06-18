public class Ta4hOptions
{

    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    public string ModelVersion { get; set; } = "2023-04-01";

    public string ApiVersion { get; set; } = "2023-04-01";

    public int MaxDocsPerRequest { get; set; } = 25;

    public int MaxCharactersPerRequest { get; set; } = 125000;

    public bool RandomizeRequestSize { get; set; } = false;


}


