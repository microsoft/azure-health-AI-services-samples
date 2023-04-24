using Newtonsoft.Json;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class ReponseHealthDocument
    {
        [JsonProperty("fhirBundle")]
        public object FhirBundle { get; set; }
    }
}
