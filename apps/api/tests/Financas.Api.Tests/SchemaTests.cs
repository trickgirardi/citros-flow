using Npgsql;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class SchemaTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly NpgsqlDataSource _db;

    public SchemaTests(PostgresFixture postgres)
    {
        _factory = new ApiFactory(postgres.ConnectionString);
        _db = NpgsqlDataSource.Create(postgres.ConnectionString);
    }

    // Subir o host aplica as migrations (Database:MigrateOnStartup).
    public Task InitializeAsync() { _ = _factory.Server; return Task.CompletedTask; }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    private async Task<T?> Scalar<T>(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        return (T?)await cmd.ExecuteScalarAsync();
    }

    private async Task Exec(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<Guid> NewUser() => (await Scalar<Guid>(
        $"insert into users (email) values ('{Guid.NewGuid()}@teste.org') returning id"));

    private async Task<Guid> NewBoard()
    {
        var account = await Scalar<Guid>("insert into accounts (name) values ('Conta') returning id");
        return await Scalar<Guid>($"insert into boards (account_id, name) values ('{account}', 'Board') returning id");
    }

    [Fact]
    public async Task Migration_cria_as_sete_tabelas_em_snake_case()
    {
        var count = await Scalar<long>("""
            select count(*) from information_schema.tables
            where table_schema = 'public'
              and table_name in ('users','accounts','boards','transactions','user_roles','board_share_links','financial_reserves')
            """);
        Assert.Equal(7, count);

        var updatedColumns = await Scalar<long>("""
            select count(*) from information_schema.columns
            where table_name = 'transactions' and column_name in ('updated_at','updated_by')
            """);
        Assert.Equal(2, updatedColumns);
    }

    [Fact]
    public async Task Dinheiro_e_numeric_12_2()
    {
        var precisions = await Scalar<long>("""
            select count(*) from information_schema.columns
            where column_name = 'amount' and table_name in ('transactions','financial_reserves')
              and data_type = 'numeric' and numeric_precision = 12 and numeric_scale = 2
            """);
        Assert.Equal(2, precisions);
    }

    [Fact]
    public async Task Email_precisa_estar_normalizado_e_e_unico()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Exec("insert into users (email) values ('Maiuscula@Teste.org')"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);

        var email = $"{Guid.NewGuid()}@teste.org";
        await Exec($"insert into users (email) values ('{email}')");
        var dup = await Assert.ThrowsAsync<PostgresException>(() =>
            Exec($"insert into users (email) values ('{email}')"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, dup.SqlState);
    }

    [Fact]
    public async Task Papel_global_nao_pode_ser_duplicado_mas_papel_por_board_pode()
    {
        var user = await NewUser();
        await Exec($"insert into user_roles (user_id, board_id, role) values ('{user}', null, 'viewer')");
        var dup = await Assert.ThrowsAsync<PostgresException>(() =>
            Exec($"insert into user_roles (user_id, board_id, role) values ('{user}', null, 'admin')"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, dup.SqlState);

        var board = await NewBoard();
        await Exec($"insert into user_roles (user_id, board_id, role) values ('{user}', '{board}', 'tesoureiro')");

        var invalid = await Assert.ThrowsAsync<PostgresException>(() =>
            Exec($"insert into user_roles (user_id, board_id, role) values ('{user}', '{board}', 'root')"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
    }

    [Fact]
    public async Task Transacao_exige_valor_positivo_e_tipo_valido()
    {
        var user = await NewUser();
        var board = await NewBoard();
        string Insert(string type, string amount) => $"""
            insert into transactions (board_id, type, amount, description, category, date, created_by)
            values ('{board}', '{type}', {amount}, 'd', 'c', '2026-05-01', '{user}')
            """;

        await Exec(Insert("entrada", "10.50"));

        var zero = await Assert.ThrowsAsync<PostgresException>(() => Exec(Insert("saida", "0")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, zero.SqlState);
        var type = await Assert.ThrowsAsync<PostgresException>(() => Exec(Insert("outro", "1")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, type.SqlState);
    }
}
