using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SalesCsv.Domain {
    public class EnqueuePayload {

        [JsonPropertyName("dateFrom")]
        public DateTime? DateFrom {get;set;}

        [JsonPropertyName("dateTo")]
        public DateTime? DateTo {get;set;}

        [JsonPropertyName("fileName")]
        public string? FileName {get;set;}

        [JsonPropertyName("runAtUtc")]
        public DateTime? RunAtUtc {get;set;}

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("isReprocessing")]
        public bool? IsReprocessing { get; set; }
    }
}
