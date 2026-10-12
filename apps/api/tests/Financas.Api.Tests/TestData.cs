using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

/// <summary>Cria dados direto no banco. Cada teste cria os seus (ids novos), então um não atrapalha o outro.</summary>
public static class TestData
{
    public static async Task<Board> AddBoardAsync(string cs, string? name = null)
    {
        await using var db = TestDb.Open(cs);
        var account = new Account { Name = "Conta de teste" };
        var board = new Board { Name = name ?? $"Board {Guid.NewGuid():N}" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        board.AccountId = account.Id;
        db.Boards.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    /// <param name="boardId">Null = papel global.</param>
    public static async Task AddRoleAsync(string cs, Guid userId, Guid? boardId, string role)
    {
        await using var db = TestDb.Open(cs);
        db.UserRoles.Add(new UserRole { UserId = userId, BoardId = boardId, Role = role });
        await db.SaveChangesAsync();
    }

    /// <summary>Usuário novo com um papel (no board, ou global se boardId for nulo).</summary>
    public static async Task<User> AddUserWithRoleAsync(string cs, Guid? boardId, string role)
    {
        var user = await TestDb.AddUserAsync(cs);
        await AddRoleAsync(cs, user.Id, boardId, role);
        return user;
    }

    public static async Task AddTransactionAsync(
        string cs, Guid boardId, Guid createdBy, string type, decimal amount, string date,
        string category = "Geral", string description = "Descrição")
    {
        await using var db = TestDb.Open(cs);
        db.Transactions.Add(new Transaction
        {
            BoardId = boardId, CreatedBy = createdBy, Type = type, Amount = amount,
            Date = DateOnly.Parse(date), Category = category, Description = description,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Insere <paramref name="count"/> transações iguais de uma vez (generate_series).</summary>
    public static async Task AddManyTransactionsAsync(
        string cs, Guid boardId, Guid createdBy, string type, decimal amount, string date, int count)
    {
        await using var db = TestDb.Open(cs);
        var day = DateOnly.Parse(date);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into transactions (board_id, type, amount, description, category, date, created_by)
            select {boardId}, {type}, {amount}, 'em massa', 'Em massa', {day}, {createdBy}
            from generate_series(1, {count})
            """);
    }

    public static async Task AddReserveAsync(string cs, Guid boardId, Guid createdBy, string name, decimal amount)
    {
        await using var db = TestDb.Open(cs);
        db.FinancialReserves.Add(new FinancialReserve
        { BoardId = boardId, CreatedBy = createdBy, Name = name, Amount = amount });
        await db.SaveChangesAsync();
    }
}
