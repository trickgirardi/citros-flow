using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Auth;

public record PasswordLoginForm(string? Email, string? Password, string? ReturnUrl);

public static class PasswordLogin
{
    // Hash válido de uma senha qualquer: falha por e-mail inexistente também paga uma verificação,
    // para a resposta demorar o mesmo e não revelar se o e-mail existe.
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), "dummy-password");

    /// <summary>
    /// Formulário do /login: o browser faz POST direto e recebe redirect, como no fluxo do Google.
    /// Qualquer falha volta para /login com o mesmo código, sem distinguir o motivo.
    /// </summary>
    public static void MapPasswordLogin(this RouteGroupBuilder group) =>
        group.MapPost("/auth/password-login", async (
                [FromForm] PasswordLoginForm form, HttpContext http, AppDbContext db,
                IPasswordHasher<User> hasher, LoginThrottle throttle, TimeProvider clock, CancellationToken ct) =>
            {
                var returnUrl = ReturnUrl.Sanitize(form.ReturnUrl);
                var email = form.Email?.Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(email) || email.Length > 254 || string.IsNullOrEmpty(form.Password))
                    return Failure("invalid_credentials", returnUrl);

                if (throttle.IsBlocked(email))
                    return Failure("too_many_attempts", returnUrl);

                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
                var verified = hasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? DummyHash, form.Password)
                    != PasswordVerificationResult.Failed;

                if (user?.PasswordHash is null || !verified)
                {
                    throttle.RecordFailure(email);
                    return Failure("invalid_credentials", returnUrl);
                }

                throttle.Reset(email);
                user.LastLoginAt = clock.GetUtcNow();
                await db.SaveChangesAsync(ct);

                await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    AuthClaims.BuildPrincipal(user), new AuthenticationProperties { IsPersistent = true });
                return Results.Redirect(returnUrl);
            })
            .WithName("PasswordLogin")
            // CSRF já coberto pelo OriginCheck e pelo SameSite=Lax; o antiforgery do ASP.NET exigiria um token no form.
            .DisableAntiforgery()
            .Accepts<PasswordLoginForm>("application/x-www-form-urlencoded")
            .Produces(StatusCodes.Status302Found);

    private static IResult Failure(string errorCode, string returnUrl) =>
        Results.Redirect($"/login?error={errorCode}&next={Uri.EscapeDataString(returnUrl)}");
}
