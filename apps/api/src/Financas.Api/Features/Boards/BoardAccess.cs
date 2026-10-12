using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Boards;

/// <param name="CanMutate">admin ou tesoureiro (no board ou global). Quem só pode ver é viewer.</param>
public record BoardContext(Board Board, bool CanMutate);

/// <summary>
/// Autorização por board: a API é a única porta de acesso ao banco, e todo endpoint de board passa por aqui.
/// Um papel com board_id nulo é global e vale para todos os boards.
/// </summary>
public class BoardAccess(AppDbContext db)
{
    /// <summary>
    /// O board com a permissão do usuário, ou null se o board não existe ou o usuário não pode vê-lo.
    /// O chamador responde 404 nos dois casos (não revela que o board existe) e 403 se
    /// <see cref="BoardContext.CanMutate"/> for falso numa escrita.
    /// </summary>
    public async Task<BoardContext?> GetAsync(Guid userId, Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId, ct);
        if (board is null) return null;

        var roles = await db.UserRoles
            .Where(r => r.UserId == userId && (r.BoardId == boardId || r.BoardId == null))
            .Select(r => r.Role)
            .ToListAsync(ct);
        if (roles.Count == 0) return null;

        return new BoardContext(board, roles.Any(role => role is "admin" or "tesoureiro"));
    }

    /// <summary>Boards que o usuário vê: os com papel próprio e, havendo papel global, todos.</summary>
    public async Task<List<Board>> ListVisibleAsync(Guid userId, CancellationToken ct)
    {
        var boards = await db.Boards.AsNoTracking()
            .Where(b => db.UserRoles.Any(r => r.UserId == userId && (r.BoardId == null || r.BoardId == b.Id)))
            .ToListAsync(ct);

        return boards.OrderBy(b => b.Name, Common.PtBr.Comparer).ToList();
    }
}
