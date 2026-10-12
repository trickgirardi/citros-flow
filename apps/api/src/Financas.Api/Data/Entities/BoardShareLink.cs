namespace Financas.Api.Data.Entities;

public class BoardShareLink
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    /// <summary>SHA-256 (hex) do token público. O token cru nunca é salvo.</summary>
    public string TokenHash { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
