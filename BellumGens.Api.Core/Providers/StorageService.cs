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

        public Task<string> SaveImage(string blob, string name)
        {
            return SaveImage(blob, name, null);
        }

        public async Task<string> SaveImage(string blob, string name, string container)
        {
			string resultPath = "";
			if (!string.IsNullOrEmpty(blob) && !Uri.IsWellFormedUriString(blob, UriKind.Absolute))
			{
                resultPath = await UploadToStorage(blob, name, container ?? _config["BlobService:Container"]);
            }
			return resultPath;
		}

		private async Task<string> UploadToStorage(string blob, string name, string container)
        {
            string connectionString = _config["BlobService:ConnectionString"];
            BlobServiceClient blobServiceClient = new(connectionString);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(container);
            BlobClient blobClient = containerClient.GetBlobClient(name + ".png");

            string base64 = blob[(blob.IndexOf(',') + 1)..];
            byte[] bytes = Convert.FromBase64String(base64);
            using MemoryStream ms = new(bytes);

            await blobClient.UploadAsync(ms, true);
            return blobClient.Uri.ToString();
        }
    }
}
