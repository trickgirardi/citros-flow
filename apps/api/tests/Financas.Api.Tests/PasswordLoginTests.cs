using System.Net;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class PasswordLoginTests : IDisposable
{
    private const string Password = "uma-senha-bem-longa";
    private const string Invalid = "/login?error=invalid_credentials&next=%2Fboard";

    private readonly PostgresFixture _postgres;
    private readonly FakeClock _clock = new(new DateTimeOffset(2040, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly ApiFactory _factory;

    public PasswordLoginTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new ApiFactory(postgres.ConnectionString, clock: _clock);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<User> UserWithPassword(string password = Password, bool active = true)
    {
        var user = await TestDb.AddUserAsync(_postgres.ConnectionString, active: active);
        await using var db = TestDb.Open(_postgres.ConnectionString);
        Assert.Null(await SetPasswordCommand.SetAsync(
            db, new PasswordHasher<User>(), user.Email, password, password, CancellationToken.None));
        return user;
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string? email, string? password, string? returnUrl = null) =>
        client.PostAsync("/api/auth/password-login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email ?? "",
            ["password"] = password ?? "",
            ["returnUrl"] = returnUrl ?? "",
        }));

    private static async Task<HttpStatusCode> MeStatus(HttpClient client) => (await client.GetAsync("/api/me")).StatusCode;

    private static bool HasSessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("citros_session="));

    [Fact]
    public async Task Senha_certa_cria_a_sessao_e_redireciona_para_o_board()
    {
        var user = await UserWithPassword();
        var client = _factory.CreateNoRedirectClient();

        var response = await Login(client, user.Email, Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/board", response.Headers.Location?.OriginalString);
        Assert.True(HasSessionCookie(response));
        Assert.Equal(HttpStatusCode.OK, await MeStatus(client));
        await using var db = TestDb.Open(_postgres.ConnectionString);
        Assert.Equal(_clock.GetUtcNow(), (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).LastLoginAt);
    }

    [Fact]
    public async Task Cookie_e_o_mesmo_da_sessao_do_google_httponly_lax()
    {
        var user = await UserWithPassword();

        var response = await Login(_factory.CreateNoRedirectClient(), user.Email, Password);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("citros_session="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Email_ignora_maiusculas_e_espacos()
    {
        var user = await UserWithPassword();

        var response = await Login(_factory.CreateNoRedirectClient(), $"  {user.Email.ToUpperInvariant()}  ", Password);

        Assert.Equal("/board", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/board/abc?month=2026-05", "/board/abc?month=2026-05")]
    [InlineData("//evil.com", "/board")]
    [InlineData("https://evil.com", "/board")]
    [InlineData("/\\evil.com", "/board")]
    [InlineData("", "/board")]
    public async Task Return_url_passa_pela_mesma_validacao_do_google(string returnUrl, string expected)
    {
        var user = await UserWithPassword();

        var response = await Login(_factory.CreateNoRedirectClient(), user.Email, Password, returnUrl);

        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Senha_errada_nao_cria_sessao_e_volta_para_o_login()
    {
        var user = await UserWithPassword();
        var client = _factory.CreateNoRedirectClient();

        var response = await Login(client, user.Email, "senha-errada-qualquer");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Invalid, response.Headers.Location?.OriginalString);
        Assert.False(HasSessionCookie(response));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatus(client));
    }

    [Fact]
    public async Task Todas_as_falhas_tem_a_mesma_resposta_e_nao_revelam_o_motivo()
    {
        var comSenha = await UserWithPassword();
        var inativo = await UserWithPassword(active: false);
        var semSenha = await TestDb.AddUserAsync(_postgres.ConnectionString); // só Google

        var respostas = new[]
        {
            await Login(_factory.CreateNoRedirectClient(), comSenha.Email, "senha-errada-qualquer"),
            await Login(_factory.CreateNoRedirectClient(), $"{Guid.NewGuid():N}@teste.org", Password), // não existe
            await Login(_factory.CreateNoRedirectClient(), inativo.Email, Password),                   // senha certa, inativo
            await Login(_factory.CreateNoRedirectClient(), semSenha.Email, Password),                  // sem hash
            await Login(_factory.CreateNoRedirectClient(), "", ""),
            await Login(_factory.CreateNoRedirectClient(), comSenha.Email, ""),
        };

        foreach (var response in respostas)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(Invalid, response.Headers.Location?.OriginalString);
            Assert.False(HasSessionCookie(response));
        }
    }

    [Fact]
    public async Task Cinco_falhas_bloqueiam_o_email_mesmo_com_a_senha_certa_ate_a_janela_acabar()
    {
        var user = await UserWithPassword();
        var client = _factory.CreateNoRedirectClient();
        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
            Assert.Equal(Invalid, (await Login(client, user.Email, "senha-errada-qualquer")).Headers.Location?.OriginalString);

        var blocked = await Login(client, user.Email, Password);

        Assert.Equal("/login?error=too_many_attempts&next=%2Fboard", blocked.Headers.Location?.OriginalString);
        Assert.False(HasSessionCookie(blocked));

        _clock.Advance(LoginThrottle.Window);
        var allowed = await Login(client, user.Email, Password);
        Assert.Equal("/board", allowed.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, await MeStatus(client));
    }

    [Fact]
    public async Task Bloqueio_de_um_email_nao_afeta_os_outros_e_login_com_sucesso_zera_o_contador()
    {
        var alvo = await UserWithPassword();
        var outro = await UserWithPassword();
        var client = _factory.CreateNoRedirectClient();
        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
            await Login(client, alvo.Email, "senha-errada-qualquer");

        Assert.Equal("/board", (await Login(_factory.CreateNoRedirectClient(), outro.Email, Password)).Headers.Location?.OriginalString);

        // 4 falhas, sucesso, 4 falhas: o sucesso zerou, então ainda não bloqueou.
        var user = await UserWithPassword();
        for (var i = 0; i < LoginThrottle.MaxFailures - 1; i++) await Login(client, user.Email, "senha-errada-qualquer");
        await Login(_factory.CreateNoRedirectClient(), user.Email, Password);
        for (var i = 0; i < LoginThrottle.MaxFailures - 1; i++) await Login(client, user.Email, "senha-errada-qualquer");

        Assert.Equal("/board", (await Login(_factory.CreateNoRedirectClient(), user.Email, Password)).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Usuario_desativado_depois_de_logar_perde_a_sessao()
    {
        var user = await UserWithPassword();
        var client = _factory.CreateNoRedirectClient();
        await Login(client, user.Email, Password);
        Assert.Equal(HttpStatusCode.OK, await MeStatus(client));

        await using (var db = TestDb.Open(_postgres.ConnectionString))
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatus(client));
    }

    [Fact]
    public async Task Post_de_outro_origin_e_recusado_pelo_csrf()
    {
        var user = await UserWithPassword();
        var client = _factory.CreateNoRedirectClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password-login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
                { ["email"] = user.Email, ["password"] = Password }),
        };
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
