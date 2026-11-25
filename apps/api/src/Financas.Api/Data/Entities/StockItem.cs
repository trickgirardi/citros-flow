namespace Financas.Api.Data.Entities;

/// <summary>Item do estoque de um board (tabela do módulo Estoque).</summary>
public class StockItem
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>"em_estoque", "a_caminho", "comprar" ou "comprar_com_urgencia".</summary>
    public string Status { get; set; } = "comprar";
    /// <summary>Um só responsável, opcional.</summary>
    public Guid? AssigneeId { get; set; }
    /// <summary>Preço do item em reais, opcional.</summary>
    public decimal? Price { get; set; }
    public string Details { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
