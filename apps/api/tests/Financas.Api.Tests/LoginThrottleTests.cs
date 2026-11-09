using Financas.Api.Features.Auth;

namespace Financas.Api.Tests;

public class LoginThrottleTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2040, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests() => _throttle = new LoginThrottle(_clock);

    private void Fail(string email, int times = 1)
    {
        for (var i = 0; i < times; i++) _throttle.RecordFailure(email);
    }

    [Fact]
    public void Bloqueia_na_quinta_falha_e_nao_antes()
    {
        Fail("a@teste.org", LoginThrottle.MaxFailures - 1);
        Assert.False(_throttle.IsBlocked("a@teste.org"));

        Fail("a@teste.org");
        Assert.True(_throttle.IsBlocked("a@teste.org"));
    }

    [Fact]
    public void Bloqueio_vale_so_para_o_email_que_falhou()
    {
        Fail("a@teste.org", LoginThrottle.MaxFailures);

        Assert.True(_throttle.IsBlocked("a@teste.org"));
        Assert.False(_throttle.IsBlocked("b@teste.org"));
    }

    [Fact]
    public void Bloqueio_termina_quando_a_janela_de_15_minutos_acaba()
    {
        Fail("a@teste.org", LoginThrottle.MaxFailures);

        _clock.Advance(LoginThrottle.Window - TimeSpan.FromSeconds(1));
        Assert.True(_throttle.IsBlocked("a@teste.org"));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_throttle.IsBlocked("a@teste.org"));
    }

    [Fact]
    public void Falha_depois_da_janela_recomeca_a_contagem()
    {
        Fail("a@teste.org", LoginThrottle.MaxFailures - 1);
        _clock.Advance(LoginThrottle.Window);

        Fail("a@teste.org", LoginThrottle.MaxFailures - 1);

        Assert.False(_throttle.IsBlocked("a@teste.org"));
    }

    [Fact]
    public void Reset_zera_o_contador()
    {
        Fail("a@teste.org", LoginThrottle.MaxFailures);

        _throttle.Reset("a@teste.org");

        Assert.False(_throttle.IsBlocked("a@teste.org"));
    }

    [Fact]
    public void Muitos_emails_diferentes_nao_travam_nem_quebram_a_poda()
    {
        for (var i = 0; i < 1500; i++) Fail($"u{i}@teste.org");
        _clock.Advance(LoginThrottle.Window);
        Fail("novo@teste.org", LoginThrottle.MaxFailures); // dispara a poda dos vencidos

        Assert.True(_throttle.IsBlocked("novo@teste.org"));
        Assert.False(_throttle.IsBlocked("u0@teste.org"));
    }
}
