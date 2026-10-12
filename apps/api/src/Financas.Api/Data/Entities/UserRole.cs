namespace Financas.Api.Data.Entities;

public class UserRole
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Null = papel global, vale para todos os boards.</summary>
    public Guid? BoardId { get; set; }
    /// <summary>"admin", "tesoureiro" ou "viewer".</summary>
    public string Role { get; set; } = "";
}
