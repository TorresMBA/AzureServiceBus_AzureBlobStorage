using Azure.Identity;
using Azure.Storage.Blobs;
using Dapper;
using SalesCsv.Domain;
using System.Data.SqlClient;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configuración (App Settings en App Service)
var cfg = builder.Configuration;

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("/generate-sales-csv", async (HttpRequest req) => {
    // Payload opcional:
    // {
    //   "dateFrom": "2025-11-08T04:00:00Z",
    //   "dateTo":   "2025-11-08T04:30:00Z",
    //   "fileName": "ventas_2025-11-08_04-30.csv"
    // }
    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();
    var payload = string.IsNullOrWhiteSpace(body) ? null : System.Text.Json.JsonSerializer.Deserialize<RequestPayload>(body);

    var nowUtc = DateTime.UtcNow;
    var minutesBack = int.TryParse(cfg["DefaultReport:MinutesBack"], out var mb) ? mb : 30;

    var dateFrom = payload?.DateFrom ?? nowUtc.AddMinutes(-minutesBack);
    var dateTo = payload?.DateTo ?? nowUtc;

    // 1) Traer datos (SQL de ejemplo)
    //var connStr = cfg.GetConnectionString("SalesDb") ?? cfg["Sql:ConnectionString"];
    //IEnumerable<SaleRow> rows;
    //await using(var conn = new SqlConnection(connStr))
    //{
    //    await conn.OpenAsync();
    //    rows = await conn.QueryAsync<SaleRow>(@"
    //        SELECT OrderId, CustomerName, Sku, Quantity, UnitPrice, CreatedUtc
    //        FROM dbo.Sales
    //        WHERE CreatedUtc >= @From AND CreatedUtc < @To
    //        ORDER BY CreatedUtc ASC;",
    //        new { From = dateFrom, To = dateTo });
    //}
    IEnumerable<SaleRow> rows = new List<SaleRow>
    {
        new SaleRow { OrderId = 1, CustomerName = "Juan Perez", Sku = "PROD001", Quantity = 2, UnitPrice = 15.50m, CreatedUtc = dateFrom.AddMinutes(5) },
        new SaleRow { OrderId = 2, CustomerName = "Maria Gomez", Sku = "PROD002", Quantity = 1, UnitPrice = 25.00m, CreatedUtc = dateFrom.AddMinutes(10) },
        new SaleRow { OrderId = 3, CustomerName = "Carlos Ruiz", Sku = "PROD003", Quantity = 3, UnitPrice = 9.99m, CreatedUtc = dateFrom.AddMinutes(15) }
    };

    // 2) Generar CSV en memoria
    var csv = new StringBuilder();
    csv.AppendLine("OrderId,CustomerName,Sku,Quantity,UnitPrice,Total,CreatedUtc");
    foreach(var r in rows)
    {
        var total = r.Quantity * r.UnitPrice;
        // Escapar comas simples (si esperas comas o comillas, agrega el escape requerido)
        csv.AppendLine($"{r.OrderId},{Escape(r.CustomerName)},{r.Sku},{r.Quantity},{r.UnitPrice},{total},{r.CreatedUtc:O}");
    }
    static string Escape(string? s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

    // 3) Subir a Blob Storage con Managed Identity
    var containerName = cfg["Storage:Container"] ?? "filescsv";
    var accountUrl = cfg["Storage:AccountUrl"]; // ej: https://<storage>.blob.core.windows.net/
    var credential = new DefaultAzureCredential(); // Usa la MI del App Service
    var blobService = new BlobServiceClient(new Uri(accountUrl), credential);
    var container = blobService.GetBlobContainerClient(containerName);
    await container.CreateIfNotExistsAsync();

    var fileName = payload?.FileName?? $"Transactions_{dateFrom:yyyyMMdd_HHmm}-{dateTo:yyyyMMdd_HHmm}.csv";
    var blob = container.GetBlobClient(fileName);

    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
    await blob.UploadAsync(ms, overwrite: true);

    return Results.Ok(new {
        message = "CSV generado",
        blob = blob.Uri.ToString(),
        from = dateFrom,
        to = dateTo,
        rows = rows.Count()
    });
})
.WithName("GenerateSalesCsv");


app.Run();
