using System.Security.Claims;
using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Transacoes;

public static class TransacoesWriteEndpoints
{
    // Toda escrita: board que o usuário não vê é 404; vê mas não é admin/tesoureiro é 403.
    // created_by/updated_by vêm sempre da sessão, nunca do corpo da requisição.
    public static void MapWrites(this RouteGroupBuilder boards)
    {
        var group = boards.MapGroup("/transactions");

        group.MapPost("/",
                async Task<Results<Ok<TransactionDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, TransactionRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();
                    if (TransactionRules.Validate(request, out var clean) is { } error)
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: error);

                    var transaction = new Transaction { BoardId = boardId, CreatedBy = userId };
                    Apply(transaction, clean);
                    db.Transactions.Add(transaction);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(transaction.ToDto());
                })
            .WithName("CreateTransaction")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:guid}",
                async Task<Results<Ok<TransactionDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid id, TransactionRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();
                    if (TransactionRules.Validate(request, out var clean) is { } error)
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: error);

                    // Sempre por id E board: transação de outro board é 404, mesmo com o id certo.
                    var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId, ct);
                    if (transaction is null) return TypedResults.NotFound();

                    Apply(transaction, clean);
                    Touch(transaction, userId);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(transaction.ToDto());
                })
            .WithName("UpdateTransaction")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/{id:guid}/description",
                async Task<Results<Ok<TransactionDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid id, UpdateDescriptionRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();
                    if (!Text.TryClean(request.Description, TransactionRules.InlineDescriptionMaxLength, out var description))
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                            title: TransactionRules.DescriptionError(TransactionRules.InlineDescriptionMaxLength));

                    var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId, ct);
                    if (transaction is null) return TypedResults.NotFound();

                    transaction.Description = description;
                    Touch(transaction, userId);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(transaction.ToDto());
                })
            .WithName("UpdateTransactionDescription")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/category",
                async Task<Results<Ok<BulkCategoryResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, UpdateCategoryRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();

                    var ids = request.Ids?.Distinct().ToList() ?? [];
                    if (ids.Count == 0 || !Text.TryClean(request.Category, TransactionRules.CategoryMaxLength, out var category))
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                            title: "Selecione transações e uma categoria válida (até 80 caracteres).");

                    // Só as transações deste board: ids de outro board são ignorados, não geram erro.
                    var now = (DateTimeOffset?)DateTimeOffset.UtcNow;
                    var updated = await db.Transactions
                        .Where(t => t.BoardId == boardId && ids.Contains(t.Id))
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.Category, category)
                            .SetProperty(t => t.UpdatedBy, (Guid?)userId)
                            .SetProperty(t => t.UpdatedAt, now), ct);
                    return TypedResults.Ok(new BulkCategoryResponse(updated));
                })
            .WithName("UpdateTransactionsCategory")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/{id:guid}",
                async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
                    Guid boardId, Guid id, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();

                    var deleted = await db.Transactions
                        .Where(t => t.Id == id && t.BoardId == boardId)
                        .ExecuteDeleteAsync(ct);
                    return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
                })
            .WithName("DeleteTransaction")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void Apply(Transaction transaction, TransactionRequest clean)
    {
        transaction.Type = TransactionTypeMap.ToDb(clean.Type);
        transaction.Amount = clean.Amount;
        transaction.Description = clean.Description!;
        transaction.Category = clean.Category!;
        transaction.Date = clean.Date;
    }

    private static void Touch(Transaction transaction, Guid userId)
    {
        transaction.UpdatedBy = userId;
        transaction.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
