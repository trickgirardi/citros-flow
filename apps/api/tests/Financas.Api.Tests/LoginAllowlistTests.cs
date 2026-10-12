using Financas.Api.Features.Auth;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class LoginAllowlistTests(PostgresFixture postgres)
{
    private async Task<LoginCheck> Check(string? email, string? verified)
    {
        await using var db = TestDb.Open(postgres.ConnectionString);
        return await LoginAllowlist.CheckAsync(db, email, verified, CancellationToken.None);
    }

    [Fact]
    public async Task Email_cadastrado_e_verificado_entra()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);

        var result = await Check(user.Email, "true");

        Assert.Equal(user.Id, result.User?.Id);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task Comparacao_ignora_maiusculas_e_espacos()
    {
        // O banco guarda sempre normalizado; o Google pode devolver caixa diferente.
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);
        var variants = new[] { user.Email.ToUpperInvariant(), $"  {user.Email}  ", $"\t{user.Email.ToUpperInvariant()}\n" };

        foreach (var googleEmail in variants)
        {
            var result = await Check(googleEmail, "True");
            Assert.Equal(user.Id, result.User?.Id);
        }
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("talvez")]
    public async Task Email_nao_verificado_e_negado_mesmo_que_cadastrado(string? verified)
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);

        var result = await Check(user.Email, verified);

        Assert.Null(result.User);
        Assert.Equal("unverified", result.ErrorCode);
    }

    [Fact]
    public async Task Email_inexistente_e_negado()
    {
        var result = await Check($"{Guid.NewGuid():N}@naocadastrado.org", "true");

        Assert.Null(result.User);
        Assert.Equal("not_allowed", result.ErrorCode);
    }

    [Fact]
    public async Task Usuario_inativo_e_negado_com_o_mesmo_erro_de_nao_cadastrado()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString, active: false);

        var result = await Check(user.Email, "true");

        Assert.Null(result.User);
        Assert.Equal("not_allowed", result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Email_vazio_e_negado(string? email)
    {
        var result = await Check(email, "true");

        Assert.Null(result.User);
        Assert.Equal("not_allowed", result.ErrorCode);
    }
}
