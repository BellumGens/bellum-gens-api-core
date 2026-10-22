using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public class StorageService : IStorageService
	{
		// SVG is excluded: served from the public container it could run script.
		private static readonly Dictionary<string, string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "image/png", ".png" },
			{ "image/jpeg", ".jpg" },
			{ "image/gif", ".gif" },
			{ "image/webp", ".webp" }
		};

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
		/// <item>Anything else is image data (a <c>data:image/...;base64,</c> URI or raw base64, taken as PNG): it is
		/// uploaded with a matching extension and content type, and the stored blob's URL is returned. PNG, JPEG, GIF
		/// and WebP are accepted; other types throw <see cref="NotSupportedException"/>.</item>
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
			// Raw base64 contains no comma and is treated as PNG.
			int comma = blob.IndexOf(',');
			string mediaType = comma < 0 ? "image/png" : ParseDataUriMediaType(blob[..comma]);
			if (!ImageExtensions.TryGetValue(mediaType, out string extension))
			{
				throw new NotSupportedException($"Unsupported image type '{mediaType}'.");
			}
			byte[] bytes = Convert.FromBase64String(blob[(comma + 1)..]);

			BlobContainerClient containerClient = _containerProvider.GetContainerClient();
			BlobClient blobClient = containerClient.GetBlobClient(name + extension);

			using MemoryStream ms = new(bytes);
			// No access conditions, so an existing blob is overwritten.
			await blobClient.UploadAsync(ms, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = mediaType } });
			return blobClient.Uri.ToString();
		}

		/// <summary>Returns the media type of a "data:image/png;base64" header, lower-cased.</summary>
		private static string ParseDataUriMediaType(string header)
		{
			if (!header.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || !header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
			{
				throw new FormatException("Image data must be a base64 data URI or raw base64.");
			}
			return header[5..].Split(';')[0].Trim().ToLowerInvariant();
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
