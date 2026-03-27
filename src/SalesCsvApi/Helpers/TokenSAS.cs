using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace SalesCsvApi.Helpers
{
    public static class TokenSAS
    {
        public static string GenerarUrlSas(BlobServiceClient blobServiceClient, string containerName, string blobName)
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMonths(2)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            // 🔥 ESTA es la clave
            var sasUri = blobClient.GenerateSasUri(sasBuilder);

            return sasUri.ToString();
        }
    }
}
