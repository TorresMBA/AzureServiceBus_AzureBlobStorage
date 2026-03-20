using Azure.Storage.Blobs;
using CsvHelper;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SalesCsvProcessorFunc
{
    public class ProcessCsvBlob
    {
        [FunctionName("ProcessCsvBlob")]
        public async Task Run([BlobTrigger("inbound/{name}", Connection = "AzureWebJobsStorage")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            var errors = new List<string>();
            var outputObjects = new List<object>();

            try
            {
                using var reader = new StreamReader(myBlob, Encoding.UTF8);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                // Map dynamic or strong type; here using dynamic
                var records = csv.GetRecords<dynamic>();

                int row = 0;
                foreach(var rec in records)
                {
                    row++;
                    try
                    {
                        // Ejemplo: validar campos esperados
                        var dict = rec as IDictionary<string, object>;
                        if(dict == null)
                        {
                            errors.Add($"Row {row}: unable to parse");
                            continue;
                        }

                        // Validaciones de ejemplo
                        if(!dict.ContainsKey("Id") || string.IsNullOrWhiteSpace(Convert.ToString(dict["Id"])))
                        {
                            errors.Add($"Row {row}: Missing Id");
                            continue;
                        }

                        // parse date example
                        string dateStr = Convert.ToString(dict.ContainsKey("Date") ? dict["Date"] : "");
                        if(!DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            errors.Add($"Row {row}: Invalid Date '{dateStr}'");
                            continue;
                        }

                        // Build object to save as JSON
                        var obj = new {
                            Id = Convert.ToString(dict["Id"]),
                            Date = parsedDate,
                            Raw = dict
                        };

                        outputObjects.Add(obj);
                    } catch(Exception exRow)
                    {
                        errors.Add($"Row {row}: {exRow.Message}");
                    }
                }

                // Prepare Blob client to write JSON
                var storageConn = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                var blobService = new BlobServiceClient(storageConn);
                var container = blobService.GetBlobContainerClient("processed");
                await container.CreateIfNotExistsAsync();

                var outBlobName = $"processed-{DateTime.UtcNow:yyyyMMddHHmmss}-{name}.json";
                var blobClient = container.GetBlobClient(outBlobName);

                using var ms = new MemoryStream();
                await JsonSerializer.SerializeAsync(ms, outputObjects, new JsonSerializerOptions { WriteIndented = false });
                ms.Position = 0;
                await blobClient.UploadAsync(ms, overwrite: true);

                log.LogInformation($"Wrote processed JSON to processed/{outBlobName}");

                // Move original CSV to archive (copy+delete)
                var inboundContainer = blobService.GetBlobContainerClient("filescsv");
                var sourceBlob = inboundContainer.GetBlobClient(name);

                var archiveContainer = blobService.GetBlobContainerClient("filecsv-archive");
                await archiveContainer.CreateIfNotExistsAsync();
                var destBlob = archiveContainer.GetBlobClient($"archived-{DateTime.UtcNow:yyyyMMddHHmmss}-{name}");

                await destBlob.StartCopyFromUriAsync(sourceBlob.Uri);
                await sourceBlob.DeleteIfExistsAsync();

                if(errors.Count > 0)
                {
                    // Save error log
                    var errorContainer = blobService.GetBlobContainerClient("filecsv-error");
                    await errorContainer.CreateIfNotExistsAsync();
                    var errBlob = errorContainer.GetBlobClient($"errors-{DateTime.UtcNow:yyyyMMddHHmmss}-{name}.log");
                    using var errMs = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", errors)));
                    await errBlob.UploadAsync(errMs, overwrite: true);
                    log.LogWarning($"Processing completed with {errors.Count} errors. Error log written to error/");
                } else
                {
                    log.LogInformation("Processing completed with no errors.");
                }
            } catch(Exception ex)
            {
                log.LogError(ex, $"Fatal error processing blob {name}");
                // Rethrow to let Function retry behaviour take place if configured
                throw;
            }
        }
    }
}
