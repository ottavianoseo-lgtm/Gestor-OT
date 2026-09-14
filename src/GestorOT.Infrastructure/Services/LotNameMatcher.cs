using System.Globalization;
using System.Text;

namespace GestorOT.Infrastructure.Services;

/// <summary>
/// Cruza el nombre de un polígono importado con el de un lote. Los .dbf reales vienen sucios:
/// "LOTE 01", "Lote Nº 1", "POTRERO 1", "1 ", "Lote_1", con acentos. El match por igualdad de
/// string se queda corto para 122 polígonos, así que además de normalizar se puntúa por
/// similitud.
///
/// Es estático y sin dependencias para poder fijar la regla por test sin base de datos.
/// </summary>
public static class LotNameMatcher
{
    /// <summary>Igual o más que esto se propone vincular sin intervención del operador.</summary>
    public const double AutoThreshold = 0.82;

    /// <summary>Desde acá el lote se ofrece como candidato para elegir a mano.</summary>
    public const double CandidateThreshold = 0.55;

    // Palabras genéricas que no aportan identidad: "LOTE 3" y "3" son el mismo lote.
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "LOTE", "LOTES", "LOT", "POTRERO", "POTREROS", "PARCELA", "PARCELAS",
        "CAMPO", "NRO", "NUMERO", "NUM", "N", "NO"
    };

    /// <summary>
    /// Deja solo letras y dígitos en mayúsculas, sin acentos, sin separadores, sin palabras
    /// genéricas y sin ceros a la izquierda. "Lote Nº 01" y "1" comparten la misma clave.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sinAcentos = QuitarAcentos(name).ToUpperInvariant();

        // Todo lo que no sea letra ASCII o dígito es separador: "_", "-", ".", "º", "ª", etc.
        // El filtro es ASCII a propósito: tras quitar acentos, cualquier "letra" no-ASCII que
        // quede (º, ª) no aporta al nombre y sí ensucia la comparación.
        var sb = new StringBuilder(sinAcentos.Length);
        foreach (var ch in sinAcentos)
            sb.Append(((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) ? ch : ' ');

        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var utiles = tokens.Where(t => !StopWords.Contains(t)).ToList();

        // Si el nombre era solo una palabra genérica ("LOTE"), el fallback evita dejarlo vacío:
        // se compara igual contra lo que haya.
        if (utiles.Count == 0)
            return string.Concat(tokens).TrimStart('0');

        return string.Join(' ', utiles.Select(TrimLeadingZeros));
    }

    /// <summary>0..1. 1 es igual; 0 no se parecen en nada.</summary>
    public static double Score(string? a, string? b) => ScoreNormalized(Normalize(a), Normalize(b));

    /// <summary>Igual que <see cref="Score"/> pero con las claves ya normalizadas.</summary>
    public static double ScoreNormalized(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;

        // Uno contenido en el otro ("1" vs "1 A"): es una variante del mismo nombre.
        if (a.Contains(b) || b.Contains(a)) return 0.9;

        var ratio = 1.0 - (double)Levenshtein(a, b) / Math.Max(a.Length, b.Length);
        return Math.Round(Math.Clamp(ratio, 0, 1), 4);
    }

    private static string TrimLeadingZeros(string token)
    {
        if (token.Length <= 1 || !token.All(char.IsDigit)) return token;
        var trimmed = token.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static string QuitarAcentos(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
