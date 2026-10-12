namespace Financas.Api.Data.Entities;

/// <summary>Allowlist de login: só e-mails cadastrados aqui entram. Não há auto-cadastro.</summary>
public class User
{
    public Guid Id { get; set; }
    /// <summary>Sempre normalizado (trim + lower).</summary>
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
