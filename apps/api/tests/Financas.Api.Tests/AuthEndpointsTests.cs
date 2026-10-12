using System.Net;
using System.Net.Http.Json;
using Financas.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class AuthEndpointsTests(PostgresFixture postgres) : IDisposable
{
    private readonly ApiFactory _factory = new(postgres.ConnectionString);

    public void Dispose() => _factory.Dispose();

    private async Task<HttpResponseMessage> SignIn(HttpClient client, Guid userId) =>
        await client.GetAsync($"/test/signin?userId={userId}");

    [Fact]
    public async Task Me_sem_sessao_responde_401_sem_redirect()
    {
        var response = await _factory.CreateNoRedirectClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Me_com_sessao_devolve_os_dados_do_usuario()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString, displayName: "Fulana");
        var client = _factory.CreateNoRedirectClient();
        await SignIn(client, user.Id);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(new MeResponse(user.Id, user.Email, "Fulana"), me);
    }

    [Fact]
    public async Task Cookie_de_sessao_e_httponly_lax_e_dura_7_dias()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);

        var response = await SignIn(_factory.CreateNoRedirectClient(), user.Id);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("citros_session="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        var expires = DateTimeOffset.Parse(setCookie.Split(';')
            .Single(p => p.Trim().StartsWith("expires=", StringComparison.OrdinalIgnoreCase)).Split('=', 2)[1]);
        Assert.InRange(expires - DateTimeOffset.UtcNow, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7.1));
    }

    [Fact]
    public async Task Usuario_desativado_perde_a_sessao_no_request_seguinte()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);
        var client = _factory.CreateNoRedirectClient();
        await SignIn(client, user.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        await using (var db = TestDb.Open(postgres.ConnectionString))
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Usuario_removido_perde_a_sessao()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);
        var client = _factory.CreateNoRedirectClient();
        await SignIn(client, user.Id);

        await using (var db = TestDb.Open(postgres.ConnectionString))
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_encerra_a_sessao_e_volta_para_o_login()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);
        var client = _factory.CreateNoRedirectClient();
        await SignIn(client, user.Id);

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Login_redireciona_para_o_google_com_o_redirect_uri_do_origin_publico()
    {
        using var factory = new ApiFactory(postgres.ConnectionString,
            new() { ["App:PublicOrigin"] = "http://localhost:3000" });

        var response = await factory.CreateNoRedirectClient().GetAsync("/api/auth/login?returnUrl=/board?month=2026-05");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);
        // Mesmo chegando na API como localhost:5080 (rewrite do Next), o redirect_uri usa o origin do Next.
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fapi%2Fauth%2Fsignin-google", location);
        Assert.Contains("client_id=test-client-id", location);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            c => c.Contains(".Correlation.", StringComparison.Ordinal)
                 && c.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_com_returnUrl_malicioso_ainda_redireciona_para_o_google()
    {
        var response = await _factory.CreateNoRedirectClient().GetAsync("/api/auth/login?returnUrl=https://evil.com");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://accounts.google.com/", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Login_sem_google_configurado_responde_503_e_a_api_continua_de_pe()
    {
        using var factory = new ApiFactory(postgres.ConnectionString,
            new() { ["Google:ClientId"] = "", ["Google:ClientSecret"] = "" });
        var client = factory.CreateNoRedirectClient();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/auth/login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }
}
