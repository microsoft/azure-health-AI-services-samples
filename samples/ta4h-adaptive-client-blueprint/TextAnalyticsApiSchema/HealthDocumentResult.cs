using Newtonsoft.Json.Linq;

namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class HealthDocumentResult
{
    public string Id { get; set; }
    public List<HealthcareEntity> Entities { get; set; }
    public List<HealthcareRelation> Relations { get; set; }
    public JToken FhirBundle { get; set; }

}
