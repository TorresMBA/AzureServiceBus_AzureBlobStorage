
namespace SalesCsv.Domain {
    public class RequestPayload {

        public Guid JobId { get; set; }

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public string? FileName { get; set; }
    }
}
