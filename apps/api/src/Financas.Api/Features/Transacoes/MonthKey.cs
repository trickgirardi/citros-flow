using System.Globalization;

namespace Financas.Api.Features.Transacoes;

/// <summary>Mês "yyyy-MM" como intervalo fechado-aberto [Start, End). Não depende do fuso do servidor.</summary>
public readonly record struct MonthKey(string Key, DateOnly Start, DateOnly End)
{
    public static bool TryParse(string? value, out MonthKey month)
    {
        month = default;
        // yyyy-MM exato (4 dígitos, mês com 2): "2026-1" e "2026-13" não passam.
        if (value is not { Length: 7 }
            || !DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || start.Year >= 9999)
            return false;

        month = new MonthKey(value, start, start.AddMonths(1));
        return true;
    }
}
