using System.Security.Claims;
using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Boards;

public record BoardRequest(string? Name);

/// <param name="ShareLinkCount">Links públicos não revogados.</param>
public record BoardAdminResponse(
    Guid Id, string Name, DateTimeOffset CreatedAt, int TransactionCount, int ReserveCount, int ShareLinkCount);

/// <summary>
/// Gestão dos boards (spec 7.12). Só admin global: quem não é recebe 403 sem que a resposta revele se o
/// board existe. Remover apaga em cascata (no banco) transações, reservas, links públicos e papéis do board.
/// </summary>
public static class BoardAdminEndpoints
{
    private const int NameMaxLength = 80;
    private const string NameError = "Nome obrigatório, com até 80 caracteres.";
    private const string DuplicateError = "Já existe um board com esse nome.";
    private const string DefaultAccountName = "Conta principal";

    public static void MapBoardAdmin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/boards",
                async Task<Results<Ok<List<BoardAdminResponse>>, ForbidHttpResult>> (
                    ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (!await access.IsGlobalAdminAsync(user.GetUserId()!.Value, ct)) return TypedResults.Forbid();

                    var boards = await db.Boards.AsNoTracking()
                        .Select(b => new BoardAdminResponse(
                            b.Id, b.Name, b.CreatedAt,
                            db.Transactions.Count(t => t.BoardId == b.Id),
                            db.FinancialReserves.Count(r => r.BoardId == b.Id),
                            db.BoardShareLinks.Count(l => l.BoardId == b.Id && l.RevokedAt == null)))
                        .ToListAsync(ct);
                    return TypedResults.Ok(boards.OrderBy(b => b.Name, PtBr.Comparer).ToList());
                })
            .WithName("ListAdminBoards")
            .WithTags("Boards")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPost("/api/boards",
                async Task<Results<Ok<BoardResponse>, ForbidHttpResult, ProblemHttpResult>> (
                    BoardRequest request, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (!await access.IsGlobalAdminAsync(user.GetUserId()!.Value, ct)) return TypedResults.Forbid();
                    if (!Text.TryClean(request.Name, NameMaxLength, out var name)) return Bad(400, NameError);
                    if (await NameTakenAsync(db, name, Guid.Empty, ct)) return Bad(409, DuplicateError);

                    // Não há tela de contas: o board vai para a conta mais antiga (criada se ainda não houver nenhuma).
                    var accountId = await db.Accounts
                        .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                        .Select(a => (Guid?)a.Id)
                        .FirstOrDefaultAsync(ct);
                    if (accountId is null)
                    {
                        var account = new Account { Name = DefaultAccountName };
                        db.Accounts.Add(account);
                        await db.SaveChangesAsync(ct);
                        accountId = account.Id;
                    }

                    var board = new Board { AccountId = accountId.Value, Name = name };
                    db.Boards.Add(board);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(new BoardResponse(board.Id, board.Name));
                })
            .WithName("CreateBoard")
            .WithTags("Boards")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPut("/api/boards/{boardId:guid}",
                async Task<Results<Ok<BoardResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, BoardRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (!await access.IsGlobalAdminAsync(user.GetUserId()!.Value, ct)) return TypedResults.Forbid();
                    if (!Text.TryClean(request.Name, NameMaxLength, out var name)) return Bad(400, NameError);

                    var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId, ct);
                    if (board is null) return TypedResults.NotFound();
                    if (await NameTakenAsync(db, name, boardId, ct)) return Bad(409, DuplicateError);

                    board.Name = name;
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(new BoardResponse(board.Id, board.Name));
                })
            .WithName("UpdateBoard")
            .WithTags("Boards")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapDelete("/api/boards/{boardId:guid}",
                async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (!await access.IsGlobalAdminAsync(user.GetUserId()!.Value, ct)) return TypedResults.Forbid();

                    var deleted = await db.Boards.Where(b => b.Id == boardId).ExecuteDeleteAsync(ct);
                    return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
                })
            .WithName("DeleteBoard")
            .WithTags("Boards")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>Nome de outro board (sem distinguir maiúsculas). <paramref name="exceptId"/> deixa o próprio board de fora.</summary>
    private static Task<bool> NameTakenAsync(AppDbContext db, string name, Guid exceptId, CancellationToken ct) =>
        db.Boards.AnyAsync(b => b.Id != exceptId && b.Name.ToLower() == name.ToLower(), ct);

    private static ProblemHttpResult Bad(int status, string title) =>
        TypedResults.Problem(statusCode: status, title: title);
}
