using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Auth;

/// <summary>Resultado da checagem: ou há um usuário liberado, ou um código de erro para a tela de login.</summary>
public record LoginCheck(User? User, string? ErrorCode);

public static class LoginAllowlist
{
    /// <summary>
    /// Não existe auto-cadastro: só entra quem já está em <c>users</c>, ativo e com e-mail
    /// verificado pelo Google. A comparação de e-mail ignora maiúsculas e espaços.
    /// </summary>
    public static async Task<LoginCheck> CheckAsync(
        AppDbContext db, string? email, string? emailVerified, CancellationToken ct)
    {
        // O Google devolve o booleano como "True"/"true"/"false"; ausente conta como não verificado.
        if (!bool.TryParse(emailVerified, out var verified) || !verified)
            return new LoginCheck(null, "unverified");

        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            return new LoginCheck(null, "not_allowed");

        // Usuário inativo recebe o mesmo erro de "não cadastrado": não revela que o e-mail existe.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive, ct);
        return user is null ? new LoginCheck(null, "not_allowed") : new LoginCheck(user, null);
    }
}
