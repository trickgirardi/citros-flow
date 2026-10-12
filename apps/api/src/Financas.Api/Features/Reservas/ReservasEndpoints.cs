using System.Security.Claims;
using Financas.Api.Data;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Reservas;

public record ReserveDto(Guid Id, string Name, decimal Amount);

public static class ReservasEndpoints
{
    public static void MapReservas(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/reserves").WithTags("Reservas").RequireAuthorization();

        // A lista (nomes e valores) é para quem gerencia as reservas. Viewer e link público só veem o
        // total, que já vem no resumo do mês (spec 4.6).
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
    }
}
