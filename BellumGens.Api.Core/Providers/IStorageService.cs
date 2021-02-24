using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public interface IStorageService
    {
        public Task<string> SaveImage(string blob, string name);
    }
}
