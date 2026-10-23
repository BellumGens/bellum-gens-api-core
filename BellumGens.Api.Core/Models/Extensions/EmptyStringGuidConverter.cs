using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models.Extensions
{
	/// <summary>
	/// Reads an empty (or whitespace) JSON string as <see cref="Guid.Empty"/>. The clients send new entities with
	/// <c>id: ''</c>, which the default converter rejects, failing model binding before the action runs.
	/// </summary>
	public class EmptyStringGuidConverter : JsonConverter<Guid>
	{
		public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
			{
				return Guid.Empty;
			}
			string value = reader.GetString();
			if (string.IsNullOrWhiteSpace(value))
			{
				return Guid.Empty;
			}
			if (Guid.TryParse(value, out Guid id))
			{
				return id;
			}
			throw new JsonException($"'{value}' is not a valid id.");
		}

		public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value);
		}
	}
}
