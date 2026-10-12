using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Financas.Api.Tests;

/// <summary>Sobe um Postgres 18 real uma vez, com as migrations aplicadas, para todos os testes de integração.</summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>API compartilhada (Google "configurado", sem PublicOrigin). Testes com outra config criam a sua.</summary>
    public ApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = TestDb.Open(ConnectionString);
        await db.Database.MigrateAsync();
        Factory = new ApiFactory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
