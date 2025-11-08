using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalesCsv.Domain {
    public class EnqueuePayload {

        public DateTime? DateFrom {get;set;}

        public DateTime? DateTo {get;set;}

        public string? FileName {get;set;}

        public DateTime? RunAtUtc {get;set;}

        public string? CorrelationId { get; set; }
    }
}
