public class Ta4hOptions
{
    /// <summary>
    /// The Language Resource Endpoint - can be retrieved from the azure resource using azur eportal or az cli
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// The Language secret Api Key - can be retrieved from the azure resource using azur eportal or az cli
    /// </summary>
    public string ApiKey { get; set; }

    public string ApiKeyHeaderName { get; set; } = "Ocp-Apim-Subscription-Key";

    public Dictionary<string,string> AdditionalGetHeaders { get; set; } = new Dictionary<string,string>();

    public Dictionary<string, string> AdditionalPostHeaders { get; set; } = new Dictionary<string, string>();


    /// <summary>
    /// Text Analytics for Health Model Version to use. e.g. "2023-04-01", "2023-04-15-preview", "2022-03-01"
    /// </summary>
    public string ModelVersion { get; set; } = "latest";

    public bool StructureToFhir { get; set; } = false;

    public string DocumentType { get; set; }

    /// <summary>
    /// Language API api verison to use.
    /// </summary>
    public string ApiVersion { get; set; } = "2023-04-01";

    /// <summary>
    /// The Language of the documents. available languages: "en", "fr", "de", "it", "fr", "es", "pt". for onprem container also "he" is supported.
    /// If not all documents are in the same language, you can use the auto language detection feature by specifying "auto" as the language - note that this
    /// is less efficient, and only supported on some api versions, like ApiVersion="2023-04-15-preview". 
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Max number of documents to send in a single request. Deaults to the maximum number allowed by the API.
    /// </summary>
    public int MaxDocsPerRequest { get; set; } = 25;

    /// <summary>
    /// Max number of total characters to send in a single request. Deaults to the maximum number allowed by the API.
    /// </summary>
    public int MaxCharactersPerRequest { get; set; } = 125000;

    /// <summary>
    /// If true, the number of documents for each request will be a random value smaller or equal to MaxDocsPerRequest.
    /// </summary>
    public bool RandomizeRequestSize { get; set; } = false;

    public int MaxHttpRetries { get; set; } = 5;


}


