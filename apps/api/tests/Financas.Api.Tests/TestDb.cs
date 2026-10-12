using Financas.Api.Data;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

public static class TestDb
{
    public static AppDbContext Open(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options);

    public static async Task<User> AddUserAsync(
        string connectionString, string? email = null, bool active = true, string? displayName = null)
    {
        await using var db = Open(connectionString);
        var user = new User
        {
            Email = email ?? $"{Guid.NewGuid():N}@teste.org",
            IsActive = active,
            DisplayName = displayName,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
