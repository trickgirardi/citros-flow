using Npgsql;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class SeedTests(PostgresFixture postgres)
{
    private static string SeedSql()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "infra", "postgres", "seed.example.sql")))
            dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir?.FullName ?? throw new FileNotFoundException("seed.example.sql"),
            "infra", "postgres", "seed.example.sql"));
    }

    private async Task<long> Count(NpgsqlDataSource db, string sql)
    {
        await using var cmd = db.CreateCommand(sql);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Seed_de_exemplo_cria_usuarios_e_papeis_e_e_idempotente()
    {
        await using var db = NpgsqlDataSource.Create(postgres.ConnectionString);
        var sql = SeedSql();

        for (var i = 0; i < 2; i++) // a segunda execução não pode falhar nem duplicar
        {
            await using var cmd = db.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync();
        }

        Assert.Equal(3, await Count(db, "select count(*) from users where email like '%@exemplo.org'"));
        Assert.Equal(1, await Count(db, "select count(*) from accounts where name = 'Sede Central'"));
        Assert.Equal(2, await Count(db, "select count(*) from boards where name like 'Departamento %'"));
        Assert.Equal(1, await Count(db, """
            select count(*) from user_roles r join users u on u.id = r.user_id
            where u.email = 'admin@exemplo.org' and r.board_id is null and r.role = 'admin'
            """));
        Assert.Equal(3, await Count(db, """
            select count(*) from user_roles r join users u on u.id = r.user_id where u.email like '%@exemplo.org'
            """));
    }
}
