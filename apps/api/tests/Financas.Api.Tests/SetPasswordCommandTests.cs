using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class SetPasswordCommandTests(PostgresFixture postgres)
{
    private const string Password = "uma-senha-bem-longa";
    private readonly PasswordHasher<User> _hasher = new();

    private string Cs => postgres.ConnectionString;

    private async Task<string?> Set(string email, string? password, string? confirmation = null)
    {
        await using var db = TestDb.Open(Cs);
        return await SetPasswordCommand.SetAsync(db, _hasher, email, password, confirmation ?? password, CancellationToken.None);
    }

    private async Task<User> Reload(Guid id)
    {
        await using var db = TestDb.Open(Cs);
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Fact]
    public async Task Grava_so_o_hash_e_ele_confere_com_a_senha()
    {
        var user = await TestDb.AddUserAsync(Cs);

        Assert.Null(await Set(user.Email, Password));

        var saved = await Reload(user.Id);
        Assert.NotNull(saved.PasswordHash);
        Assert.DoesNotContain(Password, saved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(saved, saved.PasswordHash, Password));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(saved, saved.PasswordHash, "outra-senha-qualquer"));
    }

    [Fact]
    public async Task Email_e_normalizado_e_trocar_a_senha_substitui_o_hash()
    {
        var user = await TestDb.AddUserAsync(Cs, email: $"{Guid.NewGuid():N}@teste.org");

        Assert.Null(await Set(user.Email, Password));
        var first = (await Reload(user.Id)).PasswordHash;
        Assert.Null(await Set($"  {user.Email.ToUpperInvariant()} ", "outra-senha-bem-longa"));

        var saved = await Reload(user.Id);
        Assert.NotEqual(first, saved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(saved, saved.PasswordHash!, "outra-senha-bem-longa"));
    }

    [Fact]
    public async Task Senha_curta_ou_diferente_da_confirmacao_e_recusada_sem_gravar()
    {
        var user = await TestDb.AddUserAsync(Cs);

        Assert.NotNull(await Set(user.Email, new string('a', SetPasswordCommand.MinLength - 1)));
        Assert.NotNull(await Set(user.Email, Password, "senha-diferente-aqui"));
        Assert.NotNull(await Set(user.Email, ""));
        Assert.NotNull(await Set(user.Email, null));

        Assert.Null((await Reload(user.Id)).PasswordHash);
    }

    [Fact]
    public async Task Senha_com_exatamente_o_minimo_de_caracteres_e_aceita()
    {
        var user = await TestDb.AddUserAsync(Cs);

        Assert.Null(await Set(user.Email, new string('a', SetPasswordCommand.MinLength)));
    }

    [Fact]
    public async Task Usuario_inexistente_da_erro_e_nao_e_criado()
    {
        var email = $"{Guid.NewGuid():N}@teste.org";

        var error = await Set(email, Password);

        Assert.Contains("não encontrado", error);
        await using var db = TestDb.Open(Cs);
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }
}
