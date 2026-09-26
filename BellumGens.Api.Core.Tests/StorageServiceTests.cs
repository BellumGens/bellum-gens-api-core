using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    // Azure is never reached: the container/blob clients are Moq mocks (their members are virtual).
    public class StorageServiceTests
    {
        private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly string PngBase64 = Convert.ToBase64String(PngBytes);
        private static readonly Uri StoredUri = new("https://bellumgens.blob.core.windows.net/strategies/strat-id.png");

        private readonly Mock<IBlobContainerProvider> _containerProvider;
        private readonly Mock<BlobContainerClient> _container;
        private readonly Mock<BlobClient> _blob;
        private readonly StorageService _service;
        private byte[]? _uploadedBytes;

        public StorageServiceTests()
        {
            _blob = new Mock<BlobClient>();
            _blob.SetupGet(b => b.Uri).Returns(StoredUri);
            _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, bool, CancellationToken>((stream, _, _) =>
                {
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    _uploadedBytes = copy.ToArray();
                })
                .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

            _container = new Mock<BlobContainerClient>();
            _container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_blob.Object);

            _containerProvider = new Mock<IBlobContainerProvider>();
            _containerProvider.Setup(p => p.GetContainerClient()).Returns(_container.Object);

            _service = new StorageService(_containerProvider.Object);
        }

        private void VerifyNoUpload()
        {
            _containerProvider.Verify(p => p.GetContainerClient(), Times.Never);
            _blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SaveImage_WithNullOrEmptyBlob_ReturnsEmptyWithoutUploading(string? blob)
        {
            var result = await _service.SaveImage(blob!, "strat-id");

            Assert.Equal(string.Empty, result);
            VerifyNoUpload();
        }

        // Regression: an existing image URL came back as "", so a caller that saved the result erased the image.
        [Theory]
        [InlineData("https://bellumgens.blob.core.windows.net/strategies/existing.png")]
        [InlineData("http://storage.example.com/strategies/existing.png?v=2")]
        [InlineData("HTTPS://storage.example.com/Existing.png")]
        public async Task SaveImage_WithHttpUrl_ReturnsItUnchangedWithoutUploading(string url)
        {
            var result = await _service.SaveImage(url, "strat-id");

            Assert.Equal(url, result);
            VerifyNoUpload();
        }

        // Regression: Uri.IsWellFormedUriString(..., Absolute) is true for data: URIs, so they were never uploaded.
        [Theory]
        [InlineData("data:image/png;base64,")]
        [InlineData("data:image/jpeg;base64,")]
        public async Task SaveImage_WithDataUri_UploadsDecodedBytesAndReturnsStoredUrl(string prefix)
        {
            var result = await _service.SaveImage(prefix + PngBase64, "strat-id");

            Assert.Equal(StoredUri.ToString(), result);
            Assert.Equal(PngBytes, _uploadedBytes);
            _container.Verify(c => c.GetBlobClient("strat-id.png"), Times.Once);
            _blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveImage_WithRawBase64_UploadsDecodedBytes()
        {
            var result = await _service.SaveImage(PngBase64, "strat-id");

            Assert.Equal(StoredUri.ToString(), result);
            Assert.Equal(PngBytes, _uploadedBytes);
        }

        [Fact]
        public async Task SaveImage_WithInvalidBase64_ThrowsFormatExceptionWithoutUploading()
        {
            await Assert.ThrowsAsync<FormatException>(() => _service.SaveImage("data:image/png;base64,***not base64***", "strat-id"));

            VerifyNoUpload();
        }

        [Fact]
        public async Task SaveImage_WhenUploadFails_PropagatesTheError()
        {
            _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(403, "AuthorizationFailure"));

            await Assert.ThrowsAsync<RequestFailedException>(() => _service.SaveImage(PngBase64, "strat-id"));
        }

        private static IConfiguration Config(string? connectionString, string? container = "strategies") =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "BlobService:ConnectionString", connectionString },
                    { "BlobService:Container", container }
                })
                .Build();

        [Fact]
        public void ConfigurationBlobContainerProvider_WithMissingConnectionString_ConstructsButFailsOnFirstUse()
        {
            // Construction (DI registration/resolution) must not throw in local dev without storage configured.
            var provider = new ConfigurationBlobContainerProvider(Config(null));

            Assert.ThrowsAny<ArgumentException>(() => provider.GetContainerClient());
        }

        [Fact]
        public async Task SaveImage_WithMissingConnectionString_FailsOnlyWhenUploading()
        {
            var service = new StorageService(new ConfigurationBlobContainerProvider(Config(null)));

            Assert.Equal("https://storage.example.com/existing.png", await service.SaveImage("https://storage.example.com/existing.png", "strat-id"));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SaveImage(PngBase64, "strat-id"));
        }

        [Fact]
        public void ConfigurationBlobContainerProvider_BuildsContainerFromConfigurationOnce()
        {
            // Syntactically valid connection string for a non-existent account; building the client sends no request.
            var provider = new ConfigurationBlobContainerProvider(Config(
                "DefaultEndpointsProtocol=https;AccountName=bellumgenstest;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net"));

            var container = provider.GetContainerClient();

            Assert.Equal("strategies", container.Name);
            Assert.Equal("bellumgenstest", container.AccountName);
            Assert.Same(container, provider.GetContainerClient());
        }
    }
}
