using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;
using SalesCsv.Domain;
using System.Text.Json;

namespace SalesCsvEnqueuerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesEnqueueController : ControllerBase
    {
        private readonly ServiceBusSender _sender;

        public SalesEnqueueController(ServiceBusSender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public IActionResult EnqueueSales()
        {
            // Here you would add your logic to enqueue the sales data for processing.
            // For demonstration purposes, we'll just return a success message.
            return Ok(new
            {
                Mesage = "Sales data enqueued successfully",
            });
        }

        /// <summary>
        /// Body esperado:
        ///     {
        ///        "dateFrom": "2025-11-08T04:00:00Z",
        ///       "dateTo":   "2025-11-08T04:30:00Z",
        ///       "fileName": "ventas_turno_noche.csv",
        ///       "runAtUtc": "2025-11-08T04:35:00Z", // opcional: programa el mensaje
        ///       "correlationId": "abc-123"          // opcional
        ///     }
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        [HttpPost("enqueue-sales-csv")]
        public async Task<IActionResult> EnqueueSalesAsync(EnqueuePayload payload)
        {
            var strBody = JsonSerializer.Serialize(payload);

            // Serializa exactamente el payload que espera el Logic App B / consumidor
            var message = new ServiceBusMessage(strBody)
            {
                ContentType = "application/json",
                MessageId = payload.CorrelationId ?? Guid.NewGuid().ToString(), // idempotencia básica
                CorrelationId = payload.CorrelationId ?? Guid.NewGuid().ToString()
            };

            // Props útiles para trazabilidad/filtrado
            message.ApplicationProperties["type"] = "sales-csv-request";
            if(payload.DateFrom.HasValue) message.ApplicationProperties["dateFrom"] = payload.DateFrom.Value.ToString("O");
            if(payload.DateTo.HasValue) message.ApplicationProperties["dateTo"] = payload.DateTo.Value.ToString("O");
            if(!string.IsNullOrWhiteSpace(payload.FileName)) message.ApplicationProperties["fileName"] = payload.FileName;
            message.ApplicationProperties["isReprocessing"] = payload.IsReprocessing;

            // Encolado inmediato o programado (Schedule)
            if(payload.RunAtUtc.HasValue)
            {
                // Programa el mensaje para procesarse a la hora indicada
                var seq = await _sender.ScheduleMessageAsync(message, payload.RunAtUtc.Value);
                return Ok(new { status = "scheduled", sequenceNumber = seq, runAtUtc = payload.RunAtUtc });
            } else
            {
                await _sender.SendMessageAsync(message);
                return Ok(new { status = "enqueued", messageId = message.MessageId });
            }
        }
    }
}
