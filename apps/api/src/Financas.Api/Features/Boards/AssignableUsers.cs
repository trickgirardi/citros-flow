using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Boards;

/// <summary>Quem pode ser responsável por uma tarefa ou um item de estoque: pessoas ativas com papel no board ou global.</summary>
public static class AssignableUsers
{
    public const string InvalidMessage = "Responsável inválido: só pessoas ativas com acesso a este board.";

    public static IQueryable<User> Query(AppDbContext db, Guid boardId) =>
        db.Users.Where(u => u.IsActive
            && db.UserRoles.Any(r => r.UserId == u.Id && (r.BoardId == boardId || r.BoardId == null)));

    /// <summary>True se algum dos ids não for uma pessoa que pode ser responsável neste board.</summary>
    public static async Task<bool> AnyInvalidAsync(
        AppDbContext db, Guid boardId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count > 0 && await Query(db, boardId).CountAsync(u => ids.Contains(u.Id), ct) != ids.Count;
}
