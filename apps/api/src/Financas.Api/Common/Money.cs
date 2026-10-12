namespace Financas.Api.Common;

public static class Money
{
    /// <summary>Maior valor de numeric(12,2).</summary>
    public const decimal MaxAmount = 9_999_999_999.99m;

    /// <summary>
    /// Arredonda para centavos (metade para longe do zero, como o Math.round do front) e exige
    /// 0 &lt; valor &lt;= máximo. Valor que arredonda para 0,00 (ex.: 0.004) é inválido.
    /// </summary>
    public static bool TryNormalizePositive(decimal value, out decimal normalized)
    {
        normalized = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return normalized is > 0 and <= MaxAmount;
    }
}
