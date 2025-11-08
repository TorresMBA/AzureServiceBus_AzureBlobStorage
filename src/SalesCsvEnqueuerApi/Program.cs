using Azure.Identity;
using Azure.Messaging.ServiceBus;
using SalesCsv.Domain;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// DI del cliente de Service Bus (MI o Connection String)
builder.Services.AddSingleton<ServiceBusClient>(sp => {
    var ns = cfg["ServiceBus:FullyQualifiedNamespace"];
    var cs = cfg["ServiceBus:ConnectionString"];
    if(!string.IsNullOrWhiteSpace(cs))
    {
        return new ServiceBusClient(cs); // usando SAS connection string
    }
    if(string.IsNullOrWhiteSpace(ns))
        throw new InvalidOperationException("Configura ServiceBus:FullyQualifiedNamespace o ServiceBus:ConnectionString");

    var cred = new DefaultAzureCredential(); // Managed Identity en Azure
    return new ServiceBusClient(ns, cred);
});

builder.Services.AddSingleton(sp => {
    var client = sp.GetRequiredService<ServiceBusClient>();
    var queue = cfg["ServiceBus:QueueName"] ?? "sales-csv-requests";
    return client.CreateSender(queue);
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("/enqueue-sales-csv", async (HttpRequest req, ServiceBusSender sender) => {
    // Body esperado:
    // {
    //   "dateFrom": "2025-11-08T04:00:00Z",
    //   "dateTo":   "2025-11-08T04:30:00Z",
    //   "fileName": "ventas_turno_noche.csv",
    //   "runAtUtc": "2025-11-08T04:35:00Z", // opcional: programa el mensaje
    //   "correlationId": "abc-123"          // opcional
    // }

    using var sr = new StreamReader(req.Body);
    var bodyStr = await sr.ReadToEndAsync();
    if(string.IsNullOrWhiteSpace(bodyStr))
        return Results.BadRequest(new { error = "Body JSON requerido" });

    var payload = JsonSerializer.Deserialize<EnqueuePayload>(bodyStr);
    if(payload is null)
        return Results.BadRequest(new { error = "JSON inválido" });

    // Serializa exactamente el payload que espera el Logic App B / consumidor
    var message = new ServiceBusMessage(bodyStr) {
        ContentType = "application/json",
        MessageId = payload.CorrelationId ?? Guid.NewGuid().ToString(), // idempotencia básica
        CorrelationId = payload.CorrelationId ?? Guid.NewGuid().ToString()
    };

    // Props útiles para trazabilidad/filtrado
    message.ApplicationProperties["type"] = "sales-csv-request";
    if(payload.DateFrom.HasValue) message.ApplicationProperties["dateFrom"] = payload.DateFrom.Value.ToString("O");
    if(payload.DateTo.HasValue) message.ApplicationProperties["dateTo"] = payload.DateTo.Value.ToString("O");
    if(!string.IsNullOrWhiteSpace(payload.FileName)) message.ApplicationProperties["fileName"] = payload.FileName;

    // Encolado inmediato o programado (Schedule)
    if(payload.RunAtUtc.HasValue)
    {
        // Programa el mensaje para procesarse a la hora indicada
        var seq = await sender.ScheduleMessageAsync(message, payload.RunAtUtc.Value);
        return Results.Ok(new { status = "scheduled", sequenceNumber = seq, runAtUtc = payload.RunAtUtc });
    } else
    {
        await sender.SendMessageAsync(message);
        return Results.Ok(new { status = "enqueued", messageId = message.MessageId });
    }
})
.WithName("EnqueueSalesCsv");


app.Run();
