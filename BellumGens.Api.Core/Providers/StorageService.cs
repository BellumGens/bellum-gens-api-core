using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public class StorageService : IStorageService
	{
		private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IConfiguration _config;

		public StorageService(IWebHostEnvironment environment, IConfiguration config)
		{
			_hostEnvironment = environment;
            _config = config;
		}

        public object CloudStorageAccount { get; private set; }

        public async Task<string> SaveImage(string blob, string name)
        {
			string resultPath = "";
			if (!string.IsNullOrEmpty(blob) && !Uri.IsWellFormedUriString(blob, UriKind.Absolute))
			{
                resultPath = await UploadToStorage(blob, name);
            }
			return resultPath;
		}

		private async Task<string> UploadToStorage(string blob, string name)
        {
            string connectionString = _config["BlobService:ConnectionString"];
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_config["BlobService:Container"]);
            BlobClient blobClient = containerClient.GetBlobClient(name + ".png");

            string base64 = blob[(blob.IndexOf(',') + 1)..];
            byte[] bytes = Convert.FromBase64String(base64);
            using MemoryStream ms = new MemoryStream(bytes);

            await blobClient.UploadAsync(ms, true);
            return blobClient.Uri.ToString();
        }
    }
}
