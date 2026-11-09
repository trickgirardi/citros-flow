using System.Collections.Concurrent;

namespace Financas.Api.Features.Auth;

/// <summary>
/// Limite de tentativas do login por senha, por e-mail e em memória (a API roda em uma réplica).
/// Depois de <see cref="MaxFailures"/> falhas dentro de <see cref="Window"/>, o e-mail fica bloqueado
/// até a janela acabar, mesmo com a senha certa. Por e-mail, e não por IP, porque atrás do proxy
/// todo request chega com o mesmo IP.
/// </summary>
public class LoginThrottle(TimeProvider clock)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int PruneAbove = 1000;

    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset Start)> _failures = new();

    public bool IsBlocked(string email) =>
        _failures.TryGetValue(email, out var f) && clock.GetUtcNow() - f.Start < Window && f.Count >= MaxFailures;

    public void RecordFailure(string email)
    {
        var now = clock.GetUtcNow();
        _failures.AddOrUpdate(email,
            _ => (1, now),
            (_, f) => now - f.Start >= Window ? (1, now) : (f.Count + 1, f.Start));

        // Quem tenta e-mails aleatórios não pode encher a memória: acima do teto, descarta o que já venceu.
        if (_failures.Count > PruneAbove)
            foreach (var (key, f) in _failures)
                if (now - f.Start >= Window) _failures.TryRemove(key, out _);
    }

    public void Reset(string email) => _failures.TryRemove(email, out _);
}
