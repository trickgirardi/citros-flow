namespace Financas.Api.Data.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
