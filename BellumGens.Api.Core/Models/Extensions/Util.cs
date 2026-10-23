using System;

namespace BellumGens.Api.Core.Common
{
	public static class Util
	{
		public static string GenerateHashString(int length = 0)
		{
			string text = "";
			string possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			Random random = new();

			for (int i = 0; i < length; i++)
			{
				text += possible[(int)Math.Floor(random.NextDouble() * possible.Length)];
			}

			return text;
		}
	}
}
