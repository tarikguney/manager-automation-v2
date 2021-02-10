using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TarikGuney.ManagerAutomation
{
	// todo Move this to under a Models directory.
    public class WorkItemMessage
    {
        [JsonPropertyName("ids")]
        public List<long> Ids { get; set; }

        [JsonPropertyName("$expand")]
        public string Expand => "fields";
    }
}
