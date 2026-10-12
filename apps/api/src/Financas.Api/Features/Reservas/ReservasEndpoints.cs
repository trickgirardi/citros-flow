using System.Security.Claims;
using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Financas.Api.Features.Transacoes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Reservas;

public record ReserveDto(Guid Id, string Name, decimal Amount);

/// <param name="Amount">String decimal ("1234.56").</param>
public record ReserveRequest(string? Name, decimal Amount);

public static class ReservasEndpoints
{
    private const int NameMaxLength = 80;
    private const string NameError = "Nome obrigatório, com até 80 caracteres.";

    public static void MapReservas(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/reserves").WithTags("Reservas").RequireAuthorization();

        // A lista (nomes e valores) é para quem gerencia as reservas. Viewer e link público só veem o
        // total, que já vem no resumo do mês (spec 4.6). Criar, editar e remover exigem o mesmo papel.
        group.MapGet("/",
                async Task<Results<Ok<List<ReserveDto>>, NotFound, ForbidHttpResult>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();

                    var reserves = await db.FinancialReserves.AsNoTracking()
                        .Where(r => r.BoardId == boardId)
                        .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                        .Select(r => new ReserveDto(r.Id, r.Name, r.Amount))
                        .ToListAsync(ct);
                    return TypedResults.Ok(reserves);
                })
            .WithName("ListReserves")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/",
                async Task<Results<Ok<ReserveDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, ReserveRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();
                    if (Validate(request, out var name, out var amount) is { } error) return Bad(error);

                    var reserve = new FinancialReserve { BoardId = boardId, CreatedBy = userId, Name = name, Amount = amount };
                    db.FinancialReserves.Add(reserve);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(new ReserveDto(reserve.Id, reserve.Name, reserve.Amount));
                })
            .WithName("CreateReserve")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:guid}",
                async Task<Results<Ok<ReserveDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid id, ReserveRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();
                    if (Validate(request, out var name, out var amount) is { } error) return Bad(error);

                    var reserve = await db.FinancialReserves.FirstOrDefaultAsync(r => r.Id == id && r.BoardId == boardId, ct);
                    if (reserve is null) return TypedResults.NotFound();

                    reserve.Name = name;
                    reserve.Amount = amount;
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(new ReserveDto(reserve.Id, reserve.Name, reserve.Amount));
                })
            .WithName("UpdateReserve")
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

                    var deleted = await db.FinancialReserves
                        .Where(r => r.Id == id && r.BoardId == boardId)
                        .ExecuteDeleteAsync(ct);
                    return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
                })
            .WithName("DeleteReserve")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static string? Validate(ReserveRequest request, out string name, out decimal amount)
    {
        amount = 0;
        if (!Text.TryClean(request.Name, NameMaxLength, out name)) return NameError;
        return Money.TryNormalizePositive(request.Amount, out amount) ? null : TransactionRules.InvalidAmount;
    }

    private static ProblemHttpResult Bad(string title) =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}
