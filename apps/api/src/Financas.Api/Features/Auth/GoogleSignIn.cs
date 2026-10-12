using System.Security.Claims;
using Financas.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Financas.Api.Features.Auth;

public static class GoogleSignIn
{
    /// <summary>
    /// Roda depois que o Google autentica e antes da sessão ser criada. Se o e-mail não estiver
    /// liberado, nenhuma sessão é criada e o usuário volta para /login com o motivo.
    /// </summary>
    public static async Task OnTicketReceivedAsync(TicketReceivedContext context)
    {
        var ct = context.HttpContext.RequestAborted;
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var google = context.Principal;

        var check = await LoginAllowlist.CheckAsync(
            db, google?.FindFirstValue(ClaimTypes.Email), google?.FindFirstValue("email_verified"), ct);

        if (check.User is null)
        {
            context.Response.Redirect($"/login?error={check.ErrorCode}");
            context.HandleResponse();
            return;
        }

        check.User.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        context.Principal = AuthClaims.BuildPrincipal(check.User);
        context.Properties!.IsPersistent = true;
    }

    /// <summary>Consentimento negado, state inválido, redirect_uri_mismatch etc.</summary>
    public static Task OnRemoteFailureAsync(RemoteFailureContext context)
    {
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth")
            .LogWarning(context.Failure, "Falha no login com Google");

        context.Response.Redirect("/login?error=google_failed");
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
