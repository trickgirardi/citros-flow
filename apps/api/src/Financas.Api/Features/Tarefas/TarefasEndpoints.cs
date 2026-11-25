using System.Security.Claims;
using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Tarefas;

/// <summary>
/// Quadro kanban do board (spec: .specs/tarefas-spec.md). Ler: qualquer papel no board. Escrever:
/// admin ou junta_administrativa. Board que a pessoa não vê é 404; sem permissão de escrita é 403.
/// created_by/updated_by vêm sempre da sessão.
/// </summary>
public static class TarefasEndpoints
{
    public static void MapTarefas(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}").WithTags("Tarefas").RequireAuthorization();

        group.MapGet("/tasks",
                async Task<Results<Ok<List<BoardTaskDto>>, NotFound>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (await access.GetAsync(user.GetUserId()!.Value, boardId, ct) is null)
                        return TypedResults.NotFound();

                    var tasks = await db.Tasks.AsNoTracking().Where(t => t.BoardId == boardId).ToListAsync(ct);
                    var ids = tasks.Select(t => t.Id).ToList();
                    var assignees = await (
                            from a in db.TaskAssignees.AsNoTracking()
                            join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                            where ids.Contains(a.TaskId)
                            select new { a.TaskId, Assignee = new TaskAssigneeDto(u.Id, u.DisplayName ?? u.Email) })
                        .ToListAsync(ct);
                    var byTask = assignees.ToLookup(a => a.TaskId, a => a.Assignee);

                    var dtos = tasks
                        .OrderBy(t => BoardTaskStatusMap.From(t.Status)).ThenBy(t => t.Position).ThenBy(t => t.CreatedAt)
                        .Select(t => ToDto(t, byTask[t.Id].OrderBy(a => a.Name, Common.PtBr.Comparer).ToList()))
                        .ToList();
                    return TypedResults.Ok(dtos);
                })
            .WithName("ListTasks");

        // Quem pode ser responsável: pessoas ativas com papel neste board ou global.
        group.MapGet("/task-assignees",
                async Task<Results<Ok<List<TaskAssigneeDto>>, NotFound, ForbidHttpResult>> (
                    Guid boardId, ClaimsPrincipal user, BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();

                    var people = await AssignableUsers.Query(db, boardId).AsNoTracking()
                        .Select(u => new TaskAssigneeDto(u.Id, u.DisplayName ?? u.Email))
                        .ToListAsync(ct);
                    return TypedResults.Ok(people.OrderBy(p => p.Name, Common.PtBr.Comparer).ToList());
                })
            .WithName("ListTaskAssignees")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/tasks",
                async Task<Results<Ok<BoardTaskDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, TaskRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();
                    if (TaskRules.Validate(request, out var clean) is { } error) return Bad(error);
                    if (await AssignableUsers.AnyInvalidAsync(db, boardId, clean.AssigneeIds, ct)) return Bad(AssignableUsers.InvalidMessage);

                    var statusDb = BoardTaskStatusMap.ToDb(clean.Status);
                    // Id definido aqui (e não pelo banco) para a tarefa e os responsáveis irem no mesmo SaveChanges.
                    var task = new BoardTask
                    {
                        Id = Guid.NewGuid(),
                        BoardId = boardId,
                        CreatedBy = userId,
                        Name = clean.Name,
                        Description = clean.Description,
                        Status = statusDb,
                        Tags = clean.Tags,
                        Position = await db.Tasks.CountAsync(t => t.BoardId == boardId && t.Status == statusDb, ct),
                    };
                    db.Tasks.Add(task);
                    db.TaskAssignees.AddRange(clean.AssigneeIds.Select(id => new TaskAssignee { TaskId = task.Id, UserId = id }));
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(await LoadDtoAsync(db, task, ct));
                })
            .WithName("CreateTask")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/tasks/{taskId:guid}",
                async Task<Results<Ok<BoardTaskDto>, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid taskId, TaskRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();
                    if (TaskRules.Validate(request, out var clean) is { } error) return Bad(error);

                    // Sempre por id E board: tarefa de outro board é 404, mesmo com o id certo.
                    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.BoardId == boardId, ct);
                    if (task is null) return TypedResults.NotFound();
                    if (await AssignableUsers.AnyInvalidAsync(db, boardId, clean.AssigneeIds, ct)) return Bad(AssignableUsers.InvalidMessage);

                    var statusDb = BoardTaskStatusMap.ToDb(clean.Status);
                    if (statusDb != task.Status)
                    {
                        // Mudou de coluna: vai para o fim da nova.
                        task.Position = await db.Tasks.CountAsync(t => t.BoardId == boardId && t.Status == statusDb, ct);
                        task.Status = statusDb;
                    }
                    task.Name = clean.Name;
                    task.Description = clean.Description;
                    task.Tags = clean.Tags;
                    Touch(task, userId);

                    db.TaskAssignees.RemoveRange(await db.TaskAssignees.Where(a => a.TaskId == taskId).ToListAsync(ct));
                    db.TaskAssignees.AddRange(clean.AssigneeIds.Select(id => new TaskAssignee { TaskId = taskId, UserId = id }));
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(await LoadDtoAsync(db, task, ct));
                })
            .WithName("UpdateTask")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/tasks/{taskId:guid}/move",
                async Task<Results<NoContent, NotFound, ForbidHttpResult, ProblemHttpResult>> (
                    Guid boardId, Guid taskId, MoveTaskRequest request, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var userId = user.GetUserId()!.Value;
                    var context = await access.GetAsync(userId, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();
                    if (request.Status is not { } status || request.Index is not { } index || index < 0)
                        return Bad("Informe a coluna e a posição (0 ou mais).");

                    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.BoardId == boardId, ct);
                    if (task is null) return TypedResults.NotFound();

                    var oldStatus = task.Status;
                    var newStatus = BoardTaskStatusMap.ToDb(status);

                    // A coluna de destino (sem a tarefa), com ela inserida na posição pedida e renumerada.
                    var target = await Column(db, boardId, newStatus, taskId).ToListAsync(ct);
                    target.Insert(Math.Min(index, target.Count), task);
                    task.Status = newStatus;
                    Renumber(target);

                    if (oldStatus != newStatus)
                        Renumber(await Column(db, boardId, oldStatus, taskId).ToListAsync(ct));

                    Touch(task, userId);
                    await db.SaveChangesAsync(ct);
                    return TypedResults.NoContent();
                })
            .WithName("MoveTask")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/tasks/{taskId:guid}",
                async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
                    Guid boardId, Guid taskId, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    var context = await access.GetAsync(user.GetUserId()!.Value, boardId, ct);
                    if (context is null) return TypedResults.NotFound();
                    if (!context.CanManageTasks) return TypedResults.Forbid();

                    var deleted = await db.Tasks.Where(t => t.Id == taskId && t.BoardId == boardId).ExecuteDeleteAsync(ct);
                    return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
                })
            .WithName("DeleteTask")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static IQueryable<BoardTask> Column(AppDbContext db, Guid boardId, string status, Guid exceptTaskId) =>
        db.Tasks.Where(t => t.BoardId == boardId && t.Status == status && t.Id != exceptTaskId)
            .OrderBy(t => t.Position).ThenBy(t => t.CreatedAt);

    private static void Renumber(List<BoardTask> column)
    {
        for (var i = 0; i < column.Count; i++) column[i].Position = i;
    }

    private static void Touch(BoardTask task, Guid userId)
    {
        task.UpdatedBy = userId;
        task.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static BoardTaskDto ToDto(BoardTask t, List<TaskAssigneeDto> assignees) =>
        new(t.Id, t.Name, t.Description, BoardTaskStatusMap.From(t.Status), t.Position, t.Tags, assignees);

    private static async Task<BoardTaskDto> LoadDtoAsync(AppDbContext db, BoardTask task, CancellationToken ct)
    {
        var assignees = await (
                from a in db.TaskAssignees.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                where a.TaskId == task.Id
                select new TaskAssigneeDto(u.Id, u.DisplayName ?? u.Email))
            .ToListAsync(ct);
        return ToDto(task, assignees.OrderBy(a => a.Name, Common.PtBr.Comparer).ToList());
    }

    private static ProblemHttpResult Bad(string title) =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}
