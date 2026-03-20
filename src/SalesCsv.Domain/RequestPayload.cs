
using System.Text.Json.Serialization;

namespace SalesCsv.Domain {
    public class RequestPayload {

        [JsonPropertyName("jobId")]
        public Guid JobId { get; set; }

        [JsonPropertyName("dateFrom")]
        public DateTime DateFrom { get; set; }

        [JsonPropertyName("dateTo")]
        public DateTime DateTo { get; set; }

        [JsonPropertyName("fileName")]

        public string? FileName { get; set; }

        [JsonPropertyName("isReprocessing")]
        public bool? IsReprocessing { get; set; }     
    }
}
