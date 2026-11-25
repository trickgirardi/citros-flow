using System.Globalization;
using System.Security.Claims;
using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Features.Auth;
using Financas.Api.Features.Boards;
using Financas.Api.Features.Estoque;
using Financas.Api.Features.Tarefas;
using Financas.Api.Features.Transacoes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Features.Dashboard;

/// <summary>
/// Resumo da tela inicial do board (spec: .specs/dashboard-spec.md): caixa do mês, 6 meses de histórico,
/// tarefas e estoque. Qualquer papel no board lê; nada aqui escreve. Os totais são calculados aqui,
/// com os mesmos números do resumo mensal.
/// </summary>
public static class DashboardEndpoints
{
    private const int HistoryMonths = 6;
    private const int ListSize = 5;

    public static void MapDashboard(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/boards/{boardId:guid}/dashboard",
                async Task<Results<Ok<DashboardResponse>, NotFound, ProblemHttpResult>> (
                    Guid boardId, string? month, ClaimsPrincipal user,
                    BoardAccess access, AppDbContext db, CancellationToken ct) =>
                {
                    if (!MonthKey.TryParse(month, out var monthKey))
                        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                            title: "Mês inválido. Use o formato yyyy-MM, ex.: 2026-05.");

                    var userId = user.GetUserId()!.Value;
                    if (await access.GetAsync(userId, boardId, ct) is null) return TypedResults.NotFound();

                    return TypedResults.Ok(new DashboardResponse(
                        monthKey.Key,
                        await CashAsync(db, boardId, monthKey, ct),
                        await HistoryAsync(db, boardId, monthKey, ct),
                        await TasksAsync(db, boardId, userId, ct),
                        await StockAsync(db, boardId, ct)));
                })
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<DashboardCash> CashAsync(AppDbContext db, Guid boardId, MonthKey month, CancellationToken ct)
    {
        var inMonth = await db.Transactions.AsNoTracking()
            .Where(t => t.BoardId == boardId && t.Date >= month.Start && t.Date < month.End)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);
        var entradas = inMonth.FirstOrDefault(g => g.Type == "entrada")?.Total ?? 0m;
        var saidas = inMonth.FirstOrDefault(g => g.Type == "saida")?.Total ?? 0m;

        var previous = await db.Transactions
            .Where(t => t.BoardId == boardId && t.Date < month.Start)
            .SumAsync(t => t.Type == "entrada" ? t.Amount : -t.Amount, ct);
        var reserves = await db.FinancialReserves.Where(r => r.BoardId == boardId).SumAsync(r => r.Amount, ct);

        return new DashboardCash(previous, entradas, saidas, previous + entradas - saidas, reserves, inMonth.Sum(g => g.Count));
    }

    private static async Task<List<DashboardMonth>> HistoryAsync(AppDbContext db, Guid boardId, MonthKey month, CancellationToken ct)
    {
        var first = month.Start.AddMonths(-(HistoryMonths - 1));
        var rows = await db.Transactions.AsNoTracking()
            .Where(t => t.BoardId == boardId && t.Date >= first && t.Date < month.End)
            .GroupBy(t => new { t.Date.Year, t.Date.Month, t.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        return Enumerable.Range(0, HistoryMonths).Select(i =>
        {
            var m = first.AddMonths(i);
            decimal Sum(string type) => rows.Where(r => r.Year == m.Year && r.Month == m.Month && r.Type == type).Sum(r => r.Total);
            return new DashboardMonth(m.ToString("yyyy-MM", CultureInfo.InvariantCulture), Sum("entrada"), Sum("saida"));
        }).ToList();
    }

    private static async Task<DashboardTasks> TasksAsync(AppDbContext db, Guid boardId, Guid userId, CancellationToken ct)
    {
        var counts = await db.Tasks.AsNoTracking().Where(t => t.BoardId == boardId)
            .GroupBy(t => t.Status).Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync(ct);
        int Count(string status) => counts.FirstOrDefault(c => c.Status == status)?.N ?? 0;

        var mine = db.Tasks.AsNoTracking().Where(t => t.BoardId == boardId && t.Status != "concluida"
            && db.TaskAssignees.Any(a => a.TaskId == t.Id && a.UserId == userId));
        var items = await mine
            .OrderBy(t => t.Status == "em_andamento" ? 0 : 1).ThenBy(t => t.Position).ThenBy(t => t.CreatedAt)
            .Take(ListSize)
            .Select(t => new { t.Id, t.Name, t.Status })
            .ToListAsync(ct);

        return new DashboardTasks(
            Count("a_fazer"), Count("em_andamento"), Count("concluida"),
            items.Select(t => new DashboardTaskItem(t.Id, t.Name, BoardTaskStatusMap.From(t.Status))).ToList(),
            await mine.CountAsync(ct));
    }

    private static async Task<DashboardStock> StockAsync(AppDbContext db, Guid boardId, CancellationToken ct)
    {
        var counts = await db.StockItems.AsNoTracking().Where(i => i.BoardId == boardId)
            .GroupBy(i => i.Status).Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync(ct);
        int Count(string status) => counts.FirstOrDefault(c => c.Status == status)?.N ?? 0;

        // Poucos itens por board: ordena em memória para a ordem de nome pt-BR (acentos no lugar certo).
        var rows = await (
                from i in db.StockItems.AsNoTracking()
                where i.BoardId == boardId && (i.Status == "comprar_com_urgencia" || i.Status == "comprar")
                join u in db.Users.AsNoTracking() on i.AssigneeId equals u.Id into owners
                from owner in owners.DefaultIfEmpty()
                select new { i.Id, i.Name, i.Status, Owner = owner })
            .ToListAsync(ct);
        var attention = rows
            .OrderBy(r => r.Status == "comprar_com_urgencia" ? 0 : 1).ThenBy(r => r.Name, PtBr.Comparer)
            .Take(ListSize)
            .Select(r => new DashboardStockItem(r.Id, r.Name, StockStatusMap.From(r.Status),
                r.Owner is null ? null : new TaskAssigneeDto(r.Owner.Id, r.Owner.DisplayName ?? r.Owner.Email)))
            .ToList();

        return new DashboardStock(
            Count("em_estoque"), Count("a_caminho"), Count("comprar"), Count("comprar_com_urgencia"),
            attention, rows.Count);
    }
}
