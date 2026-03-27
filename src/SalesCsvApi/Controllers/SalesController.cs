using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Abstractions;
using SalesCsv.Domain;
using SalesCsvApi.Helpers;
using System;
using System.ComponentModel;
using System.Text;

namespace SalesCsvApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly MySettings _settings;
        private readonly IConfiguration _config;

        public SalesController(IOptionsSnapshot<MySettings> settings, IConfiguration config)
        {
            _settings = settings.Value; // Acceso tipado (Seguro)
            _config = config;           // Acceso dinámico (Flexible)
        }

        [HttpGet]
        public IActionResult ListarVentas()
        {
            var cn = _settings.MensajeBienvenida;
            var reintentos = _settings.ReintentosMaximos;
            var isMaintenance = _config.GetValue<string>("max-retries");
            //string message = _config["BannerMessage"] ?? "Default Msg";
            return Ok(new
            {
                Sales = new[] {
                    new { OrderId = 1, CustomerName = "Juan Perez", Sku = "PROD001", Quantity = 2, UnitPrice = 15.50m, CreatedUtc = DateTime.UtcNow.AddMinutes(-25), Mensaje = cn },
                    new { OrderId = 2, CustomerName = "Maria Gomez", Sku = "PROD002", Quantity = 1, UnitPrice = 25.00m, CreatedUtc = DateTime.UtcNow.AddMinutes(-20), Mensaje = cn },
                    new { OrderId = 3, CustomerName = "Carlos Ruiz", Sku = "PROD003", Quantity = 3, UnitPrice = 9.99m, CreatedUtc = DateTime.UtcNow.AddMinutes(-15), Mensaje = cn }
                }
            });
        }

        /// <summary>
        ///   // Payload opcional:
        /// {
        ///   "dateFrom": "2025-11-08T04:00:00Z",
        ///   "dateTo":   "2025-11-08T04:30:00Z",
        ///   "fileName": "ventas_2025-11-08_04-30.csv"
        /// }
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("generate-sales-csv")]
        public async Task<IActionResult> GenerarCsv(RequestPayload req)
        {          
            if(req == null)
            {
                return BadRequest(new { message = "Payload inválido" });
            }

            if(req.JobId == Guid.Empty)
            {
                req.JobId = Guid.NewGuid();
            }

                var nowUtc = DateTime.UtcNow;
            var minutesBack = int.TryParse(_config.GetValue<string>("max-retries"), out var mb) ? mb : 30;

            var dateFrom = req.DateFrom ?? nowUtc.AddMinutes(-minutesBack);
            var dateTo = req.DateTo ?? nowUtc;

            IEnumerable<SaleRow> rows = new List<SaleRow>
            {
                new() { OrderId = 1, CustomerName = "Juan Perez", Sku = "PROD001", Quantity = 2, UnitPrice = 15.50m, CreatedUtc = dateFrom.AddMinutes(5) },
                new() { OrderId = 2, CustomerName = "Maria Gomez", Sku = "PROD002", Quantity = 1, UnitPrice = 25.00m, CreatedUtc = dateFrom.AddMinutes(10) },
                new() { OrderId = 3, CustomerName = "Carlos Ruiz", Sku = "PROD003", Quantity = 3, UnitPrice = 9.99m, CreatedUtc = dateFrom.AddMinutes(15) }
            };

            // 2) Generar CSV en memoria
            var csv = new StringBuilder();
            csv.AppendLine("JobId,OrderId,CustomerName,Sku,Quantity,UnitPrice,Total,CreatedUtc");
            foreach(var r in rows)
            {
                var total = r.Quantity * r.UnitPrice;
                // Escapar comas simples (si esperas comas o comillas, agrega el escape requerido)
                csv.AppendLine($"{req.JobId.ToString()},{r.OrderId},{Escape(r.CustomerName)},{r.Sku},{r.Quantity},{r.UnitPrice},{total},{r.CreatedUtc:O}");
            }
            static string Escape(string? s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

            // 3) Subir a Blob Storage con Managed Identity
            var containerName = _config.GetValue<string>("Storage:Container") ?? "filescsv";
            var storageConnection = _config.GetValue<string>("Storage:ConnectionString") ?? ""; // ej: https://<storage>.blob.core.windows.net/
            var optionsAzure = new DefaultAzureCredentialOptions()
            {
                TenantId = "d20a9516-617b-4700-8e7c-cafc939164dc"
            };
            var credential = new DefaultAzureCredential(optionsAzure); // Usa la MI del App Service
            var blobService = new BlobServiceClient(storageConnection);
            var container = blobService.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync();

            var fileName = $"{DateTime.Now:yyyyMMdd_HHmm}_{req.FileName}" ?? $"Transactions_{dateFrom:yyyyMMdd_HHmm}-{dateTo:yyyyMMdd_HHmm}.csv";
            if(req.IsReprocessing.HasValue)
            {
                fileName = req.IsReprocessing.Value ? fileName.Replace(".csv", "_REPROCESSING.csv") : fileName.Replace(".csv", "_ORIGINAL.csv");
            }

            var blob = container.GetBlobClient(fileName);

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
            await blob.UploadAsync(ms, overwrite: true);

            var newUri = TokenSAS.GenerarUrlSas(blobService, containerName, fileName);

            return Ok(new
            {
                message = "CSV generado",
                blob = newUri,
                from = dateFrom,
                to = dateTo,
                rows = rows.Count()
            });
        }
    }
}
