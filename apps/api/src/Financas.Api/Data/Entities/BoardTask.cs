namespace Financas.Api.Data.Entities;

/// <summary>Tarefa do quadro kanban de um board. (Não se chama Task para não colidir com System.Threading.Tasks.)</summary>
public class BoardTask
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>"a_fazer", "em_andamento" ou "concluida".</summary>
    public string Status { get; set; } = "a_fazer";
    /// <summary>Ordem dentro da coluna (0 = topo).</summary>
    public int Position { get; set; }
    public List<string> Tags { get; set; } = [];
    /// <summary>"baixa", "media", "alta" ou "urgente"; nulo = sem prioridade.</summary>
    public string? Priority { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class TaskAssignee
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
}
