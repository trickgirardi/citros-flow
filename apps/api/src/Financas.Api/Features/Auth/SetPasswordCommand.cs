using System.Text;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Auth;

/// <summary>
/// `Financas.Api set-password &lt;email&gt;`: define ou troca a senha de um usuário que já existe em
/// <c>users</c> (seed). Grava só o hash. Não há tela para isso.
/// </summary>
public static class SetPasswordCommand
{
    public const int MinLength = 12;

    /// <summary>Código de saída: 0 em sucesso, diferente de 0 em erro (mensagem em stderr).</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string email)
    {
        var password = ReadPassword("Nova senha: ");
        // Pelo terminal pede de novo para confirmar; pela entrada padrão (pipe) vale a única linha lida.
        var confirmation = Console.IsInputRedirected ? password : ReadPassword("Repita a senha: ");

        var error = await SetAsync(
            services.GetRequiredService<AppDbContext>(), services.GetRequiredService<IPasswordHasher<User>>(),
            email, password, confirmation, CancellationToken.None);

        if (error is not null)
        {
            await Console.Error.WriteLineAsync(error);
            return 1;
        }

        Console.WriteLine($"Senha de {email.Trim().ToLowerInvariant()} atualizada.");
        return 0;
    }

    /// <summary>Valida e grava o hash. Devolve a mensagem de erro, ou null em sucesso.</summary>
    public static async Task<string?> SetAsync(
        AppDbContext db, IPasswordHasher<User> hasher, string email, string? password, string? confirmation,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password != confirmation)
            return "As senhas não conferem.";
        if (password.Length < MinLength)
            return $"A senha precisa ter pelo menos {MinLength} caracteres.";

        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null)
            return $"Usuário {normalized} não encontrado. Cadastre-o antes (seed) e rode de novo.";

        user.PasswordHash = hasher.HashPassword(user, password);
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Sem eco no terminal; lê uma linha quando a entrada vem de um pipe.</summary>
    private static string? ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected) return Console.ReadLine();

        Console.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }
            if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
            else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
        }
    }
}
