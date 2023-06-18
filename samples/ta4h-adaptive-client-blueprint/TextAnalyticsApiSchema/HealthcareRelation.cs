namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class HealthcareRelation
{
    private double _confidenceScore;

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

    public string RelationType { get; set; }

    public List<HealthcareRelationEntity> Entities { get; set; }
}

