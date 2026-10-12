using System.Security.Claims;
using System.Text;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Financas.Api.Features.Transacoes;

public static class NubankImportEndpoint
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    public static void MapNubankImport(this RouteGroupBuilder boards) =>
        boards.MapPost("/transactions/import/nubank",
                async Task<Results<Ok<ImportNubankResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, IFormFile? file, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanMutate) return TypedResults.Forbid();

                    if (file is null || file.Length == 0)
                        return Bad("Selecione um arquivo CSV válido.");
                    if (file.Length > MaxFileBytes)
                        return Bad("Arquivo muito grande (máximo 5 MB).");

                    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
                    var (imported, error) = NubankCsvParser.Parse(await reader.ReadToEndAsync(ct));
                    if (imported is null) return Bad(error!);

                    // Uma única SaveChanges = uma transação de banco: ou entra tudo, ou nada.
                    db.Transactions.AddRange(imported.Select(t => new Transaction
                    {
                        BoardId = boardId,
                        CreatedBy = userId,
                        Type = TransactionTypeMap.ToDb(t.Type),
                        Amount = t.Amount,
                        Description = t.Description,
                        Category = NubankCsvParser.Category,
                        Date = t.Date,
                    }));
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(new ImportNubankResponse(imported.Count));
                })
            // CSRF aqui é coberto por SameSite=Lax + checagem de Origin; o antiforgery do form exigiria token.
            .DisableAntiforgery()
            .WithName("ImportNubankCsv")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static ProblemHttpResult Bad(string title) =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}
