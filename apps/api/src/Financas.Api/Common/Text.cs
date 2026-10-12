namespace Financas.Api.Common;

public static class Text
{
    /// <summary>Tira espaços das pontas; vazio ou maior que <paramref name="maxLength"/> é inválido.</summary>
    public static bool TryClean(string? value, int maxLength, out string cleaned)
    {
        cleaned = value?.Trim() ?? "";
        return cleaned.Length > 0 && cleaned.Length <= maxLength;
    }
}
