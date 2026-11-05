using System.Globalization;

namespace Financas.Api.Features.Transacoes;

/// <summary>Período [Start, End] com as duas pontas inclusivas, em datas yyyy-MM-dd. Não depende do fuso do servidor.</summary>
public readonly record struct PeriodRange(DateOnly Start, DateOnly End)
{
    public const int MaxDays = 366;

    public static bool TryParse(string? start, string? end, out PeriodRange period, out string error)
    {
        period = default;

        if (!TryParseDate(start, out var startDate) || !TryParseDate(end, out var endDate))
        {
            error = "Período inválido. Use datas no formato yyyy-MM-dd, ex.: 2026-10-02.";
            return false;
        }

        if (endDate < startDate)
        {
            error = "A data de fim não pode ser anterior à data de início.";
            return false;
        }

        if (endDate.DayNumber - startDate.DayNumber + 1 > MaxDays)
        {
            error = $"O período não pode passar de {MaxDays} dias.";
            return false;
        }

        period = new PeriodRange(startDate, endDate);
        error = "";
        return true;
    }

    // yyyy-MM-dd exato. Ano 9999 fica de fora para o fim exclusivo (fim + 1 dia) não estourar.
    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        && date.Year < 9999;
}
