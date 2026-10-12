using Financas.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Auth;

public static class SessionCookie
{
    public const string Name = "citros_session";

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = Name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Segue o request: em dev (http) funciona; atrás de HTTPS vira Secure.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // API: sem sessão é 401, sem permissão é 403. Nunca redirect para uma página de login.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = ValidateAsync;
    }

    /// <summary>A cada request: usuário desativado ou removido perde a sessão na hora.</summary>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.GetUserId();
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        var active = userId is not null
            && await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, context.HttpContext.RequestAborted);
        if (active) return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
