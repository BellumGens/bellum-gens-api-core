using Microsoft.AspNetCore.Hosting;
using System;
using System.Drawing;
using System.IO;

namespace BellumGens.Api.Core.Providers
{
    public class FileService : IFileService
	{
		private readonly IWebHostEnvironment _hostEnvironment;

		public FileService(IWebHostEnvironment environment)
		{
			_hostEnvironment = environment;
		}
		public string SaveImageFile(string blob, string name)
        {
			string resultPath = "";
			if (!string.IsNullOrEmpty(blob) && !Uri.IsWellFormedUriString(blob, UriKind.Absolute))
			{
				string base64 = blob.Substring(blob.IndexOf(',') + 1);
				byte[] bytes = Convert.FromBase64String(base64);

				Image image;
				using (MemoryStream ms = new MemoryStream(bytes))
				{
					image = Image.FromStream(ms);
					string path = Path.Combine(_hostEnvironment.ContentRootPath, "/Content/Strats");
					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
					}
					path = Path.Combine(path, $"{name}.png");
					image.Save(path);
					resultPath = CORSConfig.apiDomain + $"/Content/Strats/{name}.png";
				}
			}
			return resultPath;
		}
    }
}
