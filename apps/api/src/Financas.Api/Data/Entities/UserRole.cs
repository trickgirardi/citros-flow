namespace Financas.Api.Data.Entities;

public class UserRole
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Null = papel global, vale para todos os boards.</summary>
    public Guid? BoardId { get; set; }
    /// <summary>"admin", "tesoureiro", "junta_administrativa" ou "viewer". Uma pessoa pode ter vários no mesmo board.</summary>
    public string Role { get; set; } = "";
}
