using Financas.Api.Common;

namespace Financas.Api.Features.Transacoes;

/// <param name="Amount">String decimal ("1234.56"). O front normaliza "1.234,56" antes de enviar.</param>
public record TransactionRequest(
    TransactionType Type, decimal Amount, string? Description, string? Category, DateOnly Date);

public record UpdateDescriptionRequest(string? Description);

public record UpdateCategoryRequest(List<Guid>? Ids, string? Category);

public record BulkCategoryResponse(int UpdatedCount);

public record ImportNubankResponse(int ImportedCount);

public static class TransactionRules
{
    /// <summary>Teto de segurança. O formulário limita a 120, mas descrições importadas do Nubank passam disso.</summary>
    public const int DescriptionMaxLength = 500;
    /// <summary>Edição inline: o mesmo limite que a tela já aplicava.</summary>
    public const int InlineDescriptionMaxLength = 120;
    public const int CategoryMaxLength = 80;

    public const string InvalidAmount = "Valor inválido. Informe um valor maior que zero, com até 2 casas decimais.";

    public static string DescriptionError(int max) => $"Descrição obrigatória, com até {max} caracteres.";

    public static string CategoryError => $"Categoria obrigatória, com até {CategoryMaxLength} caracteres.";

    /// <summary>Valida e normaliza (trim, centavos). Devolve a mensagem de erro, ou null se estiver tudo certo.</summary>
    public static string? Validate(TransactionRequest request, out TransactionRequest clean)
    {
        clean = request;
        if (!Money.TryNormalizePositive(request.Amount, out var amount)) return InvalidAmount;
        if (!Text.TryClean(request.Description, DescriptionMaxLength, out var description))
            return DescriptionError(DescriptionMaxLength);
        if (!Text.TryClean(request.Category, CategoryMaxLength, out var category)) return CategoryError;

        clean = request with { Amount = amount, Description = description, Category = category };
        return null;
    }
}
