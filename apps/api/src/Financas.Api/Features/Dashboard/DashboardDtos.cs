using Financas.Api.Features.Estoque;
using Financas.Api.Features.Tarefas;

namespace Financas.Api.Features.Dashboard;

/// <summary>Os mesmos números do resumo mensal (/months/{month}), sem as listas.</summary>
public record DashboardCash(
    decimal PreviousBalance,
    decimal EntradasTotal,
    decimal SaidasTotal,
    decimal Balance,
    decimal ReservesTotal,
    int TransactionCount);

/// <param name="Month">yyyy-MM.</param>
public record DashboardMonth(string Month, decimal Entradas, decimal Saidas);

public record DashboardTaskItem(Guid Id, string Name, BoardTaskStatus Status, TaskPriority? Priority);

/// <param name="Mine">Até 5 tarefas da pessoa logada que não estão concluídas (mais prioritárias primeiro; empate: em andamento antes de a fazer).</param>
/// <param name="MineTotal">Quantas são, no total.</param>
public record DashboardTasks(
    int AFazer, int EmAndamento, int Concluida, List<DashboardTaskItem> Mine, int MineTotal);

public record DashboardStockItem(Guid Id, string Name, StockStatus Status, TaskAssigneeDto? Assignee);

/// <param name="Attention">Até 5 itens para agir: "Comprar com urgência" primeiro, depois "Comprar", por nome.</param>
/// <param name="AttentionTotal">Quantos são, no total.</param>
public record DashboardStock(
    int EmEstoque,
    int ACaminho,
    int Comprar,
    int ComprarComUrgencia,
    List<DashboardStockItem> Attention,
    int AttentionTotal);

/// <param name="Month">O mês pedido (yyyy-MM).</param>
/// <param name="History">6 meses em ordem cronológica; o último é <paramref name="Month"/>. Mês sem lançamentos vem com 0,00.</param>
public record DashboardResponse(
    string Month,
    DashboardCash Cash,
    List<DashboardMonth> History,
    DashboardTasks Tasks,
    DashboardStock Stock);
