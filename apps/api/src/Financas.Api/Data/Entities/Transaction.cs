namespace Financas.Api.Data.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    /// <summary>"entrada" ou "saida".</summary>
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
