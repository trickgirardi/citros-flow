using Financas.Api.Common;

namespace Financas.Api.Features.Tarefas;

/// <summary>Colunas do quadro. (Não se chama TaskStatus para não colidir com System.Threading.Tasks.)</summary>
public enum BoardTaskStatus
{
    AFazer,
    EmAndamento,
    Concluida,
}

public static class BoardTaskStatusMap
{
    /// <summary>O banco guarda "a_fazer"/"em_andamento"/"concluida" (com check constraint).</summary>
    public static string ToDb(BoardTaskStatus status) => status switch
    {
        BoardTaskStatus.AFazer => "a_fazer",
        BoardTaskStatus.EmAndamento => "em_andamento",
        _ => "concluida",
    };

    public static BoardTaskStatus From(string dbValue) => dbValue switch
    {
        "a_fazer" => BoardTaskStatus.AFazer,
        "em_andamento" => BoardTaskStatus.EmAndamento,
        _ => BoardTaskStatus.Concluida,
    };
}

/// <summary>Prioridade da tarefa (opcional: sem prioridade é nulo).</summary>
public enum TaskPriority
{
    Baixa,
    Media,
    Alta,
    Urgente,
}

public static class TaskPriorityMap
{
    /// <summary>O banco guarda "baixa"/"media"/"alta"/"urgente" (com check constraint); nulo = sem prioridade.</summary>
    public static string? ToDb(TaskPriority? priority) => priority switch
    {
        TaskPriority.Baixa => "baixa",
        TaskPriority.Media => "media",
        TaskPriority.Alta => "alta",
        TaskPriority.Urgente => "urgente",
        _ => null,
    };

    public static TaskPriority? From(string? dbValue) => dbValue switch
    {
        "baixa" => TaskPriority.Baixa,
        "media" => TaskPriority.Media,
        "alta" => TaskPriority.Alta,
        "urgente" => TaskPriority.Urgente,
        _ => null,
    };
}

public record TaskAssigneeDto(Guid Id, string Name);

/// <param name="Position">Ordem dentro da coluna (0 = topo).</param>
public record BoardTaskDto(
    Guid Id,
    string Name,
    string Description,
    BoardTaskStatus Status,
    int Position,
    List<string> Tags,
    List<TaskAssigneeDto> Assignees,
    TaskPriority? Priority);

/// <param name="Priority">Opcional. O PUT troca todos os campos: sem prioridade (ou nula) tira a prioridade.</param>
public record TaskRequest(
    string? Name, string? Description, BoardTaskStatus? Status, List<string>? Tags, List<Guid>? AssigneeIds,
    TaskPriority? Priority = null);

/// <param name="Index">Posição na coluna de destino (0 = topo). Além do fim, vai para o fim.</param>
public record MoveTaskRequest(BoardTaskStatus? Status, int? Index);

public static class TaskRules
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 2000;
    public const int TagMaxLength = 30;
    public const int MaxTags = 10;
    public const int MaxAssignees = 10;

    /// <summary>Campos já validados e normalizados.</summary>
    public record Clean(
        string Name, string Description, BoardTaskStatus Status, List<string> Tags, List<Guid> AssigneeIds,
        TaskPriority? Priority);

    /// <summary>Valida e normaliza (trim, tags sem vazias e sem repetidas). Devolve a mensagem de erro, ou null.</summary>
    public static string? Validate(TaskRequest request, out Clean clean)
    {
        clean = new Clean("", "", BoardTaskStatus.AFazer, [], [], null);

        if (!Text.TryClean(request.Name, NameMaxLength, out var name))
            return $"Nome obrigatório, com até {NameMaxLength} caracteres.";

        var description = request.Description?.Trim() ?? "";
        if (description.Length > DescriptionMaxLength)
            return $"Descrição com até {DescriptionMaxLength} caracteres.";

        if (request.Status is not { } status) return "Status obrigatório.";

        var tags = new List<string>();
        foreach (var raw in request.Tags ?? [])
        {
            var tag = raw?.Trim() ?? "";
            if (tag.Length == 0) continue;
            if (tag.Length > TagMaxLength) return $"Cada tag pode ter até {TagMaxLength} caracteres.";
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) tags.Add(tag);
        }
        if (tags.Count > MaxTags) return $"No máximo {MaxTags} tags por tarefa.";

        var assignees = (request.AssigneeIds ?? []).Distinct().ToList();
        if (assignees.Count > MaxAssignees) return $"No máximo {MaxAssignees} responsáveis por tarefa.";

        clean = new Clean(name, description, status, tags, assignees, request.Priority);
        return null;
    }
}
