using Financas.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Auth;

public static class SessionCookie
{
    public const string Name = "citros_session";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// A sessão só é reemitida (Set-Cookie) quando faltar menos que isso para expirar. O sliding
    /// padrão do ASP.NET reemitiria depois da metade da validade (3,5 dias), com muito mais Set-Cookie.
    /// </summary>
    private static readonly TimeSpan RenewWhenRemaining = TimeSpan.FromDays(2);

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = Name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Segue o request: em dev (http) funciona; atrás de HTTPS vira Secure.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = Lifetime;
        // Desligado: a renovação é feita à mão em ValidateAsync, com a regra acima.
        options.SlidingExpiration = false;

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

    /// <summary>
    /// A cada request: usuário desativado ou removido perde a sessão na hora; e, perto do fim da
    /// validade, a sessão é renovada por mais 7 dias (a renovação mantém o tempo total do ticket).
    /// </summary>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.GetUserId();
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        var active = userId is not null
            && await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, context.HttpContext.RequestAborted);
        if (!active)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var now = (context.Options.TimeProvider ?? TimeProvider.System).GetUtcNow();
        if (context.Properties.ExpiresUtc is { } expires && expires - now < RenewWhenRemaining)
            context.ShouldRenew = true;
    }
}
