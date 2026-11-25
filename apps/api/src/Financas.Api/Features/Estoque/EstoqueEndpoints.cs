using System.Security.Claims;
using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Financas.Api.Features.Tarefas;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Estoque;

/// <summary>
/// Tabela de estoque do board (spec: .specs/estoque-spec.md). Ler: qualquer papel no board.
/// Escrever: admin ou junta_administrativa (a mesma regra das tarefas). Board que a pessoa não vê é
/// 404; sem permissão de escrita é 403. created_by/updated_by vêm sempre da sessão.
/// </summary>
public static class EstoqueEndpoints
{
    public static void MapEstoque(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/stock-items").WithTags("Estoque").RequireAuthorization();

        group.MapGet("/",
                async Task<Results<Ok<List<StockItemDto>>, NotFound>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (await access.GetAsync(user.GetUserId()!.Value, boardId, ct) is null)
                        return TypedResults.NotFound();

                    var rows = await (
                            from i in db.StockItems.AsNoTracking()
                            where i.BoardId == boardId
                            join u in db.Users.AsNoTracking() on i.AssigneeId equals u.Id into owners
                            from owner in owners.DefaultIfEmpty()
                            select new { Item = i, Owner = owner })
                        .ToListAsync(ct);
                    var items = rows
                        .Select(r => ToDto(r.Item, r.Owner))
                        .OrderBy(i => i.Name, PtBr.Comparer)
                        .ToList();
                    return TypedResults.Ok(items);
                })
            .WithName("ListStockItems");

        group.MapPost("/",
                async Task<Results<Ok<StockItemDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, StockItemRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();
                    if (StockRules.Validate(request, out var clean) is { } error) return Bad(error);
                    if (await InvalidAssigneeAsync(db, boardId, clean.AssigneeId, ct)) return Bad(AssignableUsers.InvalidMessage);

                    var item = new StockItem { BoardId = boardId, CreatedBy = userId };
                    Apply(item, clean);
                    db.StockItems.Add(item);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(await LoadDtoAsync(db, item, ct));
                })
            .WithName("CreateStockItem")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{itemId:guid}",
                async Task<Results<Ok<StockItemDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid itemId, StockItemRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();
                    if (StockRules.Validate(request, out var clean) is { } error) return Bad(error);

                    // Sempre por id E board: item de outro board é 404, mesmo com o id certo.
                    var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == itemId && i.BoardId == boardId, ct);
                    if (item is null) return TypedResults.NotFound();
                    if (await InvalidAssigneeAsync(db, boardId, clean.AssigneeId, ct)) return Bad(AssignableUsers.InvalidMessage);

                    Apply(item, clean);
                    Touch(item, userId);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(await LoadDtoAsync(db, item, ct));
                })
            .WithName("UpdateStockItem")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Troca só o status (o seletor da linha da tabela), sem revalidar o resto do item.
        group.MapPatch("/{itemId:guid}/status",
                async Task<Results<NoContent, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid itemId, StockStatusRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();
                    if (request.Status is not { } status) return Bad("Status obrigatório.");

                    var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == itemId && i.BoardId == boardId, ct);
                    if (item is null) return TypedResults.NotFound();

                    item.Status = StockStatusMap.ToDb(status);
                    Touch(item, userId);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.NoContent();
                })
            .WithName("SetStockItemStatus")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/{itemId:guid}",
                async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
                    Guid boardId, Guid itemId, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();

                    var deleted = await db.StockItems.Where(i => i.Id == itemId && i.BoardId == boardId).ExecuteDeleteAsync(ct);
                    return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
                })
            .WithName("DeleteStockItem")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<bool> InvalidAssigneeAsync(AppDbContext db, Guid boardId, Guid? assigneeId, CancellationToken ct) =>
        assigneeId is { } id && await AssignableUsers.AnyInvalidAsync(db, boardId, [id], ct);

    private static void Apply(StockItem item, StockRules.Clean clean)
    {
        item.Name = clean.Name;
        item.Status = StockStatusMap.ToDb(clean.Status);
        item.AssigneeId = clean.AssigneeId;
        item.Price = clean.Price;
        item.Details = clean.Details;
    }

    private static void Touch(StockItem item, Guid userId)
    {
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static StockItemDto ToDto(StockItem i, User? owner) =>
        new(i.Id, i.Name, StockStatusMap.From(i.Status),
            owner is null ? null : new TaskAssigneeDto(owner.Id, owner.DisplayName ?? owner.Email),
            i.Price, i.Details);

    private static async Task<StockItemDto> LoadDtoAsync(AppDbContext db, StockItem item, CancellationToken ct) =>
        ToDto(item, item.AssigneeId is { } id ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct) : null);

    private static ProblemHttpResult Bad(string title) =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}
