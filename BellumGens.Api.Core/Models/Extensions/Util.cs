using BellumGens.Api.Core.Models;
using System;
using System.Collections.Generic;

namespace BellumGens.Api.Core.Common
{
	public static class Util
	{
		private static readonly Dictionary<JerseyCut?, string> _jerseyCutNames = new()
		{
			{ JerseyCut.Male, "Мъжка" },
			{ JerseyCut.Female, "Дамска" }
		};

		private static readonly Dictionary<JerseySize?, string> _jerseySizeNames = new()
		{
            { JerseySize.XS, "XS" },
			{ JerseySize.S, "S" },
			{ JerseySize.M, "M" },
			{ JerseySize.L, "L" },
			{ JerseySize.XL, "XL" },
			{ JerseySize.XXL, "XXL" },
			{ JerseySize.XXXL, "XXXL" }
		};

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

		public static Dictionary<JerseyCut?, string> JerseyCutNames
        {
			get
            {
				return _jerseyCutNames;
            }
        }

		public static Dictionary<JerseySize?, string> JerseySizeNames
		{
			get
			{
				return _jerseySizeNames;
			}
		}
	}
}