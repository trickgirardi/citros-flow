using System.Security.Claims;
using Financas.Api.Features.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Financas.Api.Features.Boards;

public record BoardResponse(Guid Id, string Name);

public record BoardDetailResponse(Guid Id, string Name, bool CanMutate);

public static class BoardEndpoints
{
    public static void MapBoards(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards").WithTags("Boards").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, BoardAccess access, CancellationToken ct) =>
            {
                var boards = await access.ListVisibleAsync(user.GetUserId()!.Value, ct);
                return boards.Select(b => new BoardResponse(b.Id, b.Name)).ToList();
            })
            .WithName("ListBoards");

        group.MapGet("/{boardId:guid}", async Task<Results<Ok<BoardDetailResponse>, NotFound>> (
                Guid boardId, ClaimsPrincipal user, BoardAccess access, CancellationToken ct) =>
            {
                var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                return context is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(new BoardDetailResponse(context.Board.Id, context.Board.Name, context.CanMutate));
            })
            .WithName("GetBoard");
    }
}
