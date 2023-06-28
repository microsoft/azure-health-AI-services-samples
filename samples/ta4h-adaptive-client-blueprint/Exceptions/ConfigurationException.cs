public class ConfigurationException : ArgumentException
{
    public ConfigurationException(string propertyName, string invalidValue, IEnumerable<string> validValues = null)
        : base(BuildMessage(propertyName, invalidValue, validValues))
    {
    }

    private static string BuildMessage(string propertyName, string invalidValue, IEnumerable<string> validValues)
    {
        string message;
        if (string.IsNullOrEmpty(invalidValue))
        {
            message = $"Configuration field {propertyName} must be defined.";
        }
        else
        {
            message = $"Invalid value {invalidValue} provided for configuration field {propertyName}.";
        }
        if (validValues != null && validValues.Any())
        {
            var validValuesString = string.Join(", ", validValues);
            message += $" Supported values are {validValuesString}.";
        }
        return message;
    }
}