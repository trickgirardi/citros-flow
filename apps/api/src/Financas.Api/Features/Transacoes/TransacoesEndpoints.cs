using System.Security.Claims;
using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Transacoes;

public static class TransacoesEndpoints
{
    public static void MapTransacoes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}").WithTags("Transacoes").RequireAuthorization();
        group.MapWrites();
        group.MapNubankImport();

        group.MapGet("/months/{month}",
                async Task<Results<Ok<MonthSummaryResponse>, NotFound, ProblemHttpResult>> (
                    Guid boardId, string month, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (!MonthKey.TryParse(month, out var monthKey))
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                            title: "Mês inválido. Use o formato yyyy-MM, ex.: 2026-05.");

                    if (await access.GetAsync(user.GetUserId()!.Value, boardId, ct) is null)
                        return TypedResults.NotFound();

                    return TypedResults.Ok(await MonthSummaryQuery.BuildAsync(db, boardId, monthKey, ct));
                })
            .WithName("GetMonthSummary")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/suggestions",
                async Task<Results<Ok<SuggestionsResponse>, NotFound>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (await access.GetAsync(user.GetUserId()!.Value, boardId, ct) is null)
                        return TypedResults.NotFound();

                    var rows = await db.Transactions.AsNoTracking()
                        .Where(t => t.BoardId == boardId)
                        .Select(t => new { t.Category, t.Description })
                        .Distinct()
                        .ToListAsync(ct);

                    return TypedResults.Ok(new SuggestionsResponse(
                        UniqueSorted(rows.Select(r => r.Category)),
                        UniqueSorted(rows.Select(r => r.Description))));
                })
            .WithName("GetSuggestions");
    }

    /// <summary>Valores sem espaços nas pontas, sem vazios nem repetidos, em ordem pt-BR.</summary>
    private static List<string> UniqueSorted(IEnumerable<string> values) =>
        values.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().Order(PtBr.Comparer).ToList();
}
