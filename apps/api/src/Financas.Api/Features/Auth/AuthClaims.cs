using System.Security.Claims;
using Financas.Api.Data.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Financas.Api.Features.Auth;

public static class AuthClaims
{
    /// <summary>Identidade interna da sessão: só id, e-mail e nome do <c>users</c>. Nada vindo do Google.</summary>
    public static ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };
        if (user.DisplayName is not null)
            claims.Add(new Claim(ClaimTypes.Name, user.DisplayName));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
