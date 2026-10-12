using Financas.Api.Common;
using Financas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Transacoes;

public static class MonthSummaryQuery
{
    /// <summary>
    /// Tudo que o dashboard mostra de um mês. O saldo anterior é um SUM com sinal no banco (sem trazer
    /// as linhas); as somas do mês são feitas em decimal. Autorização é responsabilidade do chamador.
    /// </summary>
    public static async Task<MonthSummaryResponse> BuildAsync(
        AppDbContext db, Guid boardId, MonthKey month, CancellationToken ct)
    {
        var rows = await db.Transactions.AsNoTracking()
            .Where(t => t.BoardId == boardId && t.Date >= month.Start && t.Date < month.End)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.Type, t.Amount, t.Description, t.Category, t.Date })
            .ToListAsync(ct);

        var transactions = rows
            .Select(t => new TransactionDto(
                t.Id, TransactionTypeMap.From(t.Type), t.Amount, t.Description, t.Category, t.Date))
            .ToList();
        var entradas = Group(transactions.Where(t => t.Type == TransactionType.Entrada));
        var saidas = Group(transactions.Where(t => t.Type == TransactionType.Saida));

        var previousBalance = await db.Transactions
            .Where(t => t.BoardId == boardId && t.Date < month.Start)
            .SumAsync(t => t.Type == "entrada" ? t.Amount : -t.Amount, ct);
        var reservesTotal = await db.FinancialReserves
            .Where(r => r.BoardId == boardId)
            .SumAsync(r => r.Amount, ct);

        var entradasTotal = entradas.Sum(g => g.Total);
        var saidasTotal = saidas.Sum(g => g.Total);

        return new MonthSummaryResponse(
            month.Key, previousBalance, entradasTotal, saidasTotal,
            previousBalance + entradasTotal - saidasTotal, reservesTotal, transactions.Count, entradas, saidas);
    }

    /// <summary>Agrupa pela categoria exata (maiúsculas contam), ordena pt-BR; dentro do grupo mantém a ordem recebida.</summary>
    private static List<CategoryGroupDto> Group(IEnumerable<TransactionDto> transactions) =>
        transactions
            .GroupBy(t => t.Category)
            .Select(g => new CategoryGroupDto(g.Key, g.Sum(t => t.Amount), g.ToList()))
            .OrderBy(g => g.Category, PtBr.Comparer)
            .ToList();
}
