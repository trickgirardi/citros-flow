using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<BoardShareLink> BoardShareLinks => Set<BoardShareLink>();
    public DbSet<FinancialReserve> FinancialReserves => Set<FinancialReserve>();
    public DbSet<BoardTask> Tasks => Set<BoardTask>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
