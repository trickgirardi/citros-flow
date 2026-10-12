using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Financas.Api.Features.Auth;

public record MeResponse(Guid Id, string Email, string? DisplayName);

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Auth");

        // Inicia o login: redireciona para o Google. O callback (/api/auth/signin-google) é do handler.
        group.MapGet("/auth/login", (string? returnUrl, IConfiguration config) =>
                AuthExtensions.GoogleConfigured(config)
                    ? Results.Challenge(
                        new AuthenticationProperties { RedirectUri = ReturnUrl.Sanitize(returnUrl) },
                        [GoogleDefaults.AuthenticationScheme])
                    : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Login com Google não configurado."))
            .WithName("Login")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/auth/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            })
            .WithName("Logout")
            .Produces(StatusCodes.Status302Found);

        group.MapGet("/me", (ClaimsPrincipal user) =>
                TypedResults.Ok(new MeResponse(
                    user.GetUserId()!.Value,
                    user.FindFirstValue(ClaimTypes.Email)!,
                    user.FindFirstValue(ClaimTypes.Name))))
            .WithName("GetMe")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
