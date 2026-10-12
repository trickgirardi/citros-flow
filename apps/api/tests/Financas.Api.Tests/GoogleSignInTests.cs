using System.Security.Claims;
using System.Text.Json;
using Financas.Api.Data;
using Financas.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class GoogleSignInTests(PostgresFixture postgres) : IDisposable
{
    private readonly ApiFactory _factory = new(postgres.ConnectionString);

    public void Dispose() => _factory.Dispose();

    private static ClaimsPrincipal GooglePrincipal(string email, string? verified)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "google-sub-123"), new(ClaimTypes.Email, email) };
        if (verified is not null) claims.Add(new Claim("email_verified", verified));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
    }

    private async Task<(TicketReceivedContext Context, DefaultHttpContext Http)> Receive(string email, string? verified)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var scheme = new AuthenticationScheme(GoogleDefaults.AuthenticationScheme, null, typeof(GoogleHandler));
        var ticket = new AuthenticationTicket(
            GooglePrincipal(email, verified), new AuthenticationProperties { RedirectUri = "/board" }, scheme.Name);
        var context = new TicketReceivedContext(http, scheme, new GoogleOptions(), ticket);

        await GoogleSignIn.OnTicketReceivedAsync(context);
        return (context, http);
    }

    [Fact]
    public async Task Usuario_liberado_recebe_identidade_interna_e_last_login_at()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString, displayName: "Fulana");

        var (context, _) = await Receive(user.Email.ToUpperInvariant(), "True");

        Assert.Null(context.Result); // não foi tratado: o handler segue e cria o cookie
        Assert.Equal(user.Id, context.Principal!.GetUserId());
        Assert.Equal(user.Email, context.Principal!.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Fulana", context.Principal!.FindFirstValue(ClaimTypes.Name));
        Assert.Null(context.Principal!.FindFirst("email_verified")); // nada do Google vaza para a sessão
        Assert.True(context.Properties!.IsPersistent);
        Assert.Equal("/board", context.Properties.RedirectUri);

        await using var db = TestDb.Open(postgres.ConnectionString);
        Assert.NotNull((await db.Users.SingleAsync(u => u.Id == user.Id)).LastLoginAt);
    }

    [Theory]
    [InlineData(false, "True", "not_allowed")]
    [InlineData(true, "False", "unverified")]
    [InlineData(true, null, "unverified")]
    public async Task Login_negado_nao_cria_sessao_e_volta_para_o_login(bool existe, string? verified, string erro)
    {
        var email = existe
            ? (await TestDb.AddUserAsync(postgres.ConnectionString)).Email
            : $"{Guid.NewGuid():N}@naocadastrado.org";

        var (context, http) = await Receive(email, verified);

        Assert.True(context.Result?.Handled);
        Assert.Equal($"/login?error={erro}", http.Response.Headers.Location.ToString());
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
    }

    [Fact]
    public async Task Usuario_inativo_nao_entra()
    {
        var user = await TestDb.AddUserAsync(postgres.ConnectionString, active: false);

        var (context, http) = await Receive(user.Email, "True");

        Assert.True(context.Result?.Handled);
        Assert.Equal("/login?error=not_allowed", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Claim_email_verified_do_userinfo_do_google_chega_no_formato_que_a_allowlist_entende()
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(GoogleDefaults.AuthenticationScheme);
        var userinfo = JsonDocument.Parse(
            """{"sub":"123","name":"Fulano","email":"Fulano@Teste.org","email_verified":true}""").RootElement;

        var identity = new ClaimsIdentity("Google");
        foreach (var action in options.ClaimActions)
            action.Run(userinfo, identity, "Google");

        Assert.Equal("Fulano@Teste.org", identity.FindFirst(ClaimTypes.Email)?.Value);
        Assert.True(bool.TryParse(identity.FindFirst("email_verified")?.Value, out var verified) && verified);
    }

    [Fact]
    public void Google_usa_callback_sob_api_e_cookies_lax()
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(GoogleDefaults.AuthenticationScheme);

        Assert.Equal("/api/auth/signin-google", options.CallbackPath.Value);
        Assert.Equal(SameSiteMode.Lax, options.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.CorrelationCookie.SecurePolicy);
    }
}
