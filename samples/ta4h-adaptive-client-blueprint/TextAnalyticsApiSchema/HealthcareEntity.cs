namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class HealthcareEntity
{

    private double _confidenceScore;

    public int Offset { get; set; }

    public int Length { get; set; }

    public string Text { get; set; }

    public string Category { get; set; }

    public double ConfidenceScore
    {
        get
        {
            return _confidenceScore;
        }

        set
        {
            _confidenceScore = Math.Round(value, 2);
        }
    }

    public HealthcareAssertion Assertion { get; set; }

    public string Name { get; set; }

    public List<HealthcareEntityLink> Links { get; set; }
}

