namespace Financas.Api.Features.Transacoes;

public enum TransactionType
{
    Entrada,
    Saida,
}

public static class TransactionTypeMap
{
    /// <summary>O banco guarda "entrada"/"saida" (com check constraint).</summary>
    public static TransactionType From(string dbValue) =>
        dbValue == "entrada" ? TransactionType.Entrada : TransactionType.Saida;

    public static string ToDb(TransactionType type) =>
        type == TransactionType.Entrada ? "entrada" : "saida";
}

/// <param name="Date">Dia da transação, sem hora (yyyy-MM-dd).</param>
public record TransactionDto(
    Guid Id, TransactionType Type, decimal Amount, string Description, string Category, DateOnly Date);

public record CategoryGroupDto(string Category, decimal Total, List<TransactionDto> Transactions);

/// <param name="Month">Mês pedido (yyyy-MM).</param>
/// <param name="PreviousBalance">Saldo acumulado de tudo antes do primeiro dia do mês.</param>
/// <param name="Balance">PreviousBalance + EntradasTotal - SaidasTotal. As reservas não entram.</param>
/// <param name="ReservesTotal">Total das reservas financeiras do board (informativo, sem filtro por mês).</param>
public record MonthSummaryResponse(
    string Month,
    decimal PreviousBalance,
    decimal EntradasTotal,
    decimal SaidasTotal,
    decimal Balance,
    decimal ReservesTotal,
    int TransactionCount,
    List<CategoryGroupDto> Entradas,
    List<CategoryGroupDto> Saidas);

public record SuggestionsResponse(List<string> Categories, List<string> Descriptions);

public static class TransactionMapping
{
    public static TransactionDto ToDto(this Data.Entities.Transaction t) =>
        new(t.Id, TransactionTypeMap.From(t.Type), t.Amount, t.Description, t.Category, t.Date);
}
