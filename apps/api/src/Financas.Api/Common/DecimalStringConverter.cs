using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Financas.Api.Common;

/// <summary>
/// Dinheiro trafega como string decimal ("1234.56") no JSON, nunca como número,
/// para o front não perder precisão em float. Na leitura aceita string ou número.
/// </summary>
public class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDecimal(),
            JsonTokenType.String when decimal.TryParse(
                reader.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value) => value,
            _ => throw new JsonException("Valor monetário inválido. Use uma string decimal, ex.: \"1234.56\"."),
        };

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("F2", CultureInfo.InvariantCulture));
}
