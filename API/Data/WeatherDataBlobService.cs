using Azure.Storage.Blobs;

namespace API.Data
{
    public class WeatherDataBlobService
    {
        private readonly IConfiguration _config;
        private readonly string _containerName;
        private readonly BlobContainerClient _client;

        public WeatherDataBlobService(IConfiguration config)
        {
            _config = config;
            _containerName = _config["ContainerName"];
            var connectionString = _config.GetConnectionString("WeatherDataConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Connection string cannot be empty");
            }
            var serviceClient = new BlobServiceClient(connectionString);
            _client = serviceClient.GetBlobContainerClient(_containerName);
        }

        public async Task<bool> Exists(string blobpath)
        {
            var blob = _client.GetBlobClient(blobpath);

            return await blob.ExistsAsync();
        }

        public async Task<bool> DownloadBlob(string blobpath, string savepath)
        {
            var blob = _client.GetBlobClient(blobpath);
            await blob.DownloadToAsync(savepath);
            return true;
        }

        public async Task<Stream> OpenStream(string blobpath)
        {
            var blob = _client.GetBlobClient(blobpath);

            return await blob.OpenReadAsync();
        }
    }
}