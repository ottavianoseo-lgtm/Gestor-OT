using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestorOT.Mcp.Api;

/// <summary>
/// Los decimales llegan con la escala de la columna (155.0000, 0.070000) y System.Text.Json la
/// conserva al reenviarlos al modelo. Se normalizan al leer: mismo valor, sin ceros de relleno,
/// menos tokens.
/// </summary>
public sealed class NormalizedDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Normalize(reader.TokenType == JsonTokenType.String ? decimal.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);

    // Dividir por 1 con 28 decimales deja la escala mínima que representa el valor.
    private static decimal Normalize(decimal value) => value / 1.0000000000000000000000000000m;
}
