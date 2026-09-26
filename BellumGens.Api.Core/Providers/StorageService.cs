using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public class StorageService : IStorageService
	{
		private readonly IBlobContainerProvider _containerProvider;

		public StorageService(IBlobContainerProvider containerProvider)
		{
			_containerProvider = containerProvider;
		}

		/// <summary>
		/// Stores an image and returns its URL.
		/// <list type="bullet">
		/// <item>null/empty input returns "".</item>
		/// <item>An http(s) URL is an already stored image and is returned unchanged.</item>
		/// <item>Anything else is image data (a <c>data:image/...;base64,</c> URI or raw base64): it is uploaded
		/// and the stored blob's URL is returned.</item>
		/// </list>
		/// </summary>
		public async Task<string> SaveImage(string blob, string name)
		{
			if (string.IsNullOrEmpty(blob))
			{
				return "";
			}

			if (IsHttpUrl(blob))
			{
				return blob;
			}

			return await UploadToStorage(blob, name);
		}

		private static bool IsHttpUrl(string value)
		{
			// Uri.IsWellFormedUriString(..., Absolute) also accepts data: URIs, which are image data to upload.
			return Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
		}

		private async Task<string> UploadToStorage(string blob, string name)
		{
			// Strips a "data:image/png;base64," prefix; raw base64 contains no comma.
			string base64 = blob[(blob.IndexOf(',') + 1)..];
			byte[] bytes = Convert.FromBase64String(base64);

			BlobContainerClient containerClient = _containerProvider.GetContainerClient();
			BlobClient blobClient = containerClient.GetBlobClient(name + ".png");

			using MemoryStream ms = new(bytes);
			await blobClient.UploadAsync(ms, true);
			return blobClient.Uri.ToString();
		}
	}

	/// <summary>Supplies the blob container images are uploaded to.</summary>
	public interface IBlobContainerProvider
	{
		BlobContainerClient GetContainerClient();
	}

	/// <summary>
	/// Builds the container client from <c>BlobService:ConnectionString</c> / <c>BlobService:Container</c> on first
	/// use, so a missing connection string (e.g. local dev) only fails when an upload is attempted.
	/// Register as a singleton: the underlying client is thread-safe and meant to be reused.
	/// </summary>
	public sealed class ConfigurationBlobContainerProvider : IBlobContainerProvider
	{
		private readonly Lazy<BlobContainerClient> _container;

		public ConfigurationBlobContainerProvider(IConfiguration config)
		{
			// PublicationOnly: a failure (missing configuration) isn't cached, so it is retried on the next upload.
			_container = new Lazy<BlobContainerClient>(
				() => new BlobServiceClient(config["BlobService:ConnectionString"]).GetBlobContainerClient(config["BlobService:Container"]),
				LazyThreadSafetyMode.PublicationOnly);
		}

		public BlobContainerClient GetContainerClient() => _container.Value;
	}
}
