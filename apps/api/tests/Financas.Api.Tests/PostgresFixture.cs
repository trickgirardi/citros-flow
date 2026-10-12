using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Financas.Api.Tests;

/// <summary>Sobe um Postgres 18 real uma vez, com as migrations aplicadas, para todos os testes de integração.</summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = TestDb.Open(ConnectionString);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
