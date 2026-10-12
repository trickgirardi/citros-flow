using System.Security.Claims;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Financas.Api.Features.Transacoes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Compartilhamento;

/// <param name="Token">Token público cru. Aparece só nesta resposta; o front monta a URL /share/{token}.</param>
public record ShareLinkResponse(string Token);

public record ShareMonthResponse(string BoardName, MonthSummaryResponse Summary);

public static class CompartilhamentoEndpoints
{
    public static void MapCompartilhamento(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/boards/{boardId:guid}/share-links",
                async Task<Results<Ok<ShareLinkResponse>, NotFound, ForbidHttpResult>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();

                    var token = ShareToken.Create();
                    db.BoardShareLinks.Add(new BoardShareLink
                    {
                        BoardId = boardId, CreatedBy = userId, TokenHash = ShareToken.Hash(token),
                    });
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(new ShareLinkResponse(token));
                })
            .WithTags("Compartilhamento")
            .RequireAuthorization()
            .WithName("CreateShareLink")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Público: sem sessão. O token é validado por hash; revogado, expirado ou inexistente é sempre 404.
        // Devolve só nome do board e o resumo do mês (nada de sugestões nem lista de reservas).
        app.MapGet("/api/share/{token}/months/{month}",
                async Task<Results<Ok<ShareMonthResponse>, NotFound, ProblemHttpResult>> (
                    string token, string month, HttpContext http, AppDbContext db, CancellationToken ct) =>
                {
                    if (!MonthKey.TryParse(month, out var monthKey))
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                            title: "Mês inválido. Use o formato yyyy-MM, ex.: 2026-05.");

                    var hash = ShareToken.Hash(token);
                    var now = DateTimeOffset.UtcNow;
                    var board = await (
                        from link in db.BoardShareLinks
                        join b in db.Boards on link.BoardId equals b.Id
                        where link.TokenHash == hash && link.RevokedAt == null
                              && (link.ExpiresAt == null || link.ExpiresAt > now)
                        select new { b.Id, b.Name }).FirstOrDefaultAsync(ct);
                    if (board is null) return TypedResults.NotFound();

                    // Link revogado precisa parar de funcionar mesmo com cache no meio do caminho.
                    http.Response.Headers.CacheControl = "no-store";
                    var summary = await MonthSummaryQuery.BuildAsync(db, board.Id, monthKey, ct);
                    return TypedResults.Ok(new ShareMonthResponse(board.Name, summary));
                })
            .WithTags("Compartilhamento")
            .AllowAnonymous()
            .WithName("GetSharedMonth")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
