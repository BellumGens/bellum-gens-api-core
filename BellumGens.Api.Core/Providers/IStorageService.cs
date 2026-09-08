using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public interface IStorageService
    {
        public Task<string> SaveImage(string blob, string name);

        /// <summary>Saves a base64 PNG data URL into the given blob container. Null container uses the default one.</summary>
        public Task<string> SaveImage(string blob, string name, string container);
    }
}
