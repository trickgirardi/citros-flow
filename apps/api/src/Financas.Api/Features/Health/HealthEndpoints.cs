using Financas.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Financas.Api.Features.Health;

public record HealthResponse(string Status);

public static class HealthEndpoints
{
    public static void MapHealth(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/health", async Task<Results<Ok<HealthResponse>, ProblemHttpResult>> (
                AppDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? TypedResults.Ok(new HealthResponse("ok"))
                : TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Banco de dados indisponível"))
            .WithName("GetHealth")
            .WithTags("Health")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
