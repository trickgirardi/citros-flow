using Financas.Api.Common;
using Financas.Api.Features.Tarefas;

namespace Financas.Api.Features.Estoque;

public enum StockStatus
{
    EmEstoque,
    ACaminho,
    Comprar,
    ComprarComUrgencia,
}

public static class StockStatusMap
{
    /// <summary>O banco guarda "em_estoque"/"a_caminho"/"comprar"/"comprar_com_urgencia" (com check constraint).</summary>
    public static string ToDb(StockStatus status) => status switch
    {
        StockStatus.EmEstoque => "em_estoque",
        StockStatus.ACaminho => "a_caminho",
        StockStatus.Comprar => "comprar",
        _ => "comprar_com_urgencia",
    };

    public static StockStatus From(string dbValue) => dbValue switch
    {
        "em_estoque" => StockStatus.EmEstoque,
        "a_caminho" => StockStatus.ACaminho,
        "comprar" => StockStatus.Comprar,
        _ => StockStatus.ComprarComUrgencia,
    };
}

/// <param name="Assignee">O responsável (uma pessoa), ou nulo. Mesmo formato dos responsáveis das tarefas.</param>
/// <param name="Price">String decimal ("12.50"), ou nulo.</param>
public record StockItemDto(
    Guid Id, string Name, StockStatus Status, TaskAssigneeDto? Assignee, decimal? Price, string Details);

/// <param name="Price">String decimal ("12.50"); o front normaliza "12,50" antes de enviar. Opcional.</param>
public record StockItemRequest(
    string? Name, StockStatus? Status, Guid? AssigneeId, decimal? Price, string? Details);

public record StockStatusRequest(StockStatus? Status);

public static class StockRules
{
    public const int NameMaxLength = 120;
    public const int DetailsMaxLength = 2000;

    /// <summary>Campos já validados e normalizados.</summary>
    public record Clean(string Name, StockStatus Status, Guid? AssigneeId, decimal? Price, string Details);

    /// <summary>Valida e normaliza (trim, centavos). Devolve a mensagem de erro, ou null se estiver tudo certo.</summary>
    public static string? Validate(StockItemRequest request, out Clean clean)
    {
        clean = new Clean("", StockStatus.Comprar, null, null, "");

        if (!Text.TryClean(request.Name, NameMaxLength, out var name))
            return $"Item obrigatório, com até {NameMaxLength} caracteres.";
        if (request.Status is not { } status) return "Status obrigatório.";

        var details = request.Details?.Trim() ?? "";
        if (details.Length > DetailsMaxLength) return $"Detalhes com até {DetailsMaxLength} caracteres.";

        decimal? price = null;
        if (request.Price is { } raw)
        {
            if (!Money.TryNormalizeNonNegative(raw, out var normalized))
                return "Preço inválido. Informe um valor de 0 até 9.999.999.999,99.";
            price = normalized;
        }

        clean = new Clean(name, status, request.AssigneeId, price, details);
        return null;
    }
}
