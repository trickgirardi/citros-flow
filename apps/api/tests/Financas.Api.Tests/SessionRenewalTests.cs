using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Financas.Api.Tests;

/// <summary>Relógio que só anda quando o teste manda.</summary>
public class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}

/// <summary>
/// Sessão de 7 dias que só é reemitida quando faltarem menos de 2 dias. O objetivo é não mandar
/// Set-Cookie em todo request (o sliding padrão renovaria após 3,5 dias) e, ainda assim, manter
/// logado quem usa o sistema.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SessionRenewalTests(PostgresFixture postgres)
{
    // Longe no futuro: o CookieContainer do HttpClient compara o Expires do cookie com o relógio real.
    private static readonly DateTimeOffset Start = new(2040, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private async Task<(FakeClock Clock, ApiFactory Factory, HttpClient Client, Guid UserId)> SignIn()
    {
        var clock = new FakeClock(Start);
        var factory = new ApiFactory(postgres.ConnectionString, clock: clock);
        var user = await TestDb.AddUserAsync(postgres.ConnectionString);
        return (clock, factory, await factory.SignedInClientAsync(user.Id), user.Id);
    }

    private static string? SessionSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(c => c.StartsWith("citros_session="))
            : null;

    private static DateTimeOffset Expires(string setCookie) =>
        DateTimeOffset.Parse(setCookie.Split(';')
            .Single(p => p.Trim().StartsWith("expires=", StringComparison.OrdinalIgnoreCase)).Split('=', 2)[1]);

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(3.5)]   // metade da validade: o sliding padrão já reemitiria aqui
    [InlineData(4.99)]  // ainda faltam um pouco mais de 2 dias
    public async Task Com_mais_de_2_dias_de_validade_nao_ha_set_cookie(double daysAfterLogin)
    {
        var (clock, factory, client, _) = await SignIn();
        using var _ = factory;
        clock.Advance(TimeSpan.FromDays(daysAfterLogin));

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(SessionSetCookie(response));
    }

    [Theory]
    [InlineData(5.01)]  // faltam menos de 2 dias
    [InlineData(6.5)]
    public async Task Com_menos_de_2_dias_de_validade_a_sessao_e_reemitida_por_mais_7_dias(double daysAfterLogin)
    {
        var (clock, factory, client, _) = await SignIn();
        using var _ = factory;
        clock.Advance(TimeSpan.FromDays(daysAfterLogin));

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = SessionSetCookie(response);
        Assert.NotNull(setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(Expires(setCookie) - clock.GetUtcNow(), TimeSpan.FromDays(6.99), TimeSpan.FromDays(7.01));
    }

    [Fact]
    public async Task Depois_de_renovada_nao_reemite_de_novo_e_quem_usa_continua_logado_alem_dos_7_dias()
    {
        var (clock, factory, client, _) = await SignIn();
        using var _ = factory;

        clock.Advance(TimeSpan.FromDays(5.5));
        Assert.NotNull(SessionSetCookie(await client.GetAsync("/api/me"))); // renovou: vale até o dia 12,5
        Assert.Null(SessionSetCookie(await client.GetAsync("/api/me")));     // logo depois, nada de Set-Cookie

        clock.Advance(TimeSpan.FromDays(2.5)); // dia 8: a sessão original já teria expirado
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Quem_fica_mais_de_7_dias_sem_abrir_o_sistema_perde_a_sessao()
    {
        var (clock, factory, client, _) = await SignIn();
        using var _ = factory;

        clock.Advance(TimeSpan.FromDays(7.1));

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Usuario_desativado_nao_e_renovado_e_perde_a_sessao_mesmo_perto_do_fim()
    {
        var (clock, factory, client, userId) = await SignIn();
        using var _ = factory;
        await using (var db = TestDb.Open(postgres.ConnectionString))
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        clock.Advance(TimeSpan.FromDays(6)); // dentro da janela de renovação

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // o que sai no Set-Cookie é a remoção do cookie, nunca uma sessão nova
        Assert.StartsWith("citros_session=;", SessionSetCookie(response));
    }
}
