using Financas.Api.Data;
using Financas.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

/// <summary>
/// Endpoint só de teste: cria a sessão (cookie real, mesmo esquema e mesmas claims da produção)
/// para um usuário, sem passar pelo Google.
/// </summary>
public class TestSignInStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path != "/test/signin")
            {
                await nextMiddleware(context);
                return;
            }

            var userId = Guid.Parse(context.Request.Query["userId"]!);
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthClaims.BuildPrincipal(user),
                new AuthenticationProperties { IsPersistent = true });
        });
        next(app);
    };
}
