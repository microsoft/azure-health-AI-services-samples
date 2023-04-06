namespace text_analytics_for_health_support_functions.Models
{
    internal class Translation
    {
        public string Text { get; set; }
        public TextResult Transliteration { get; set; }
        public string To { get; set; }
    }
}
