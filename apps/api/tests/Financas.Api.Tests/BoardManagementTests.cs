using System.Net;
using System.Text.Json;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class BoardManagementTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private async Task<HttpClient> AdminClient() =>
        await postgres.Factory.SignedInClientAsync((await TestData.AddUserWithRoleAsync(Cs, null, "admin")).Id);

    private async Task<HttpClient> ClientWithRole(Guid? boardId, string role) =>
        await postgres.Factory.SignedInClientAsync((await TestData.AddUserWithRoleAsync(Cs, boardId, role)).Id);

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Send(
        HttpClient client, HttpMethod method, string url, object? body = null)
    {
        var response = await client.SendJsonAsync(method, url, body);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length > 0 && response.Content.Headers.ContentType?.MediaType?.Contains("json") == true
            ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    private static Task<(HttpStatusCode Status, JsonElement Body)> Create(HttpClient client, string? name) =>
        Send(client, HttpMethod.Post, "/api/boards", new { name });

    private async Task<Guid> NewBoard(HttpClient admin, string? name = null)
    {
        var (status, body) = await Create(admin, name ?? $"Board {Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, status);
        return body.GetProperty("id").GetGuid();
    }

    // ---- quem pode ----

    [Fact]
    public async Task Me_diz_se_o_usuario_gerencia_boards_so_admin_global()
    {
        var board = await TestData.AddBoardAsync(Cs);

        foreach (var (client, expected) in new[]
                 {
                     (await AdminClient(), true),
                     (await ClientWithRole(board.Id, "admin"), false),      // admin só de um board
                     (await ClientWithRole(null, "tesoureiro"), false),     // global, mas não admin
                     (await ClientWithRole(null, "viewer"), false),
                 })
        {
            var (_, me) = await client.GetJsonAsync("/api/me");
            Assert.Equal(expected, me.GetProperty("canManageBoards").GetBoolean());
        }
    }

    [Fact]
    public async Task Quem_nao_e_admin_global_recebe_403_em_tudo_mesmo_em_board_que_existe_ou_nao()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var clients = new[]
        {
            await ClientWithRole(board.Id, "admin"),
            await ClientWithRole(board.Id, "tesoureiro"),
            await ClientWithRole(board.Id, "viewer"),
            await ClientWithRole(null, "tesoureiro"),
            await ClientWithRole(null, "viewer"),
        };

        foreach (var client in clients)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/boards")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Create(client, "Qualquer")).Status);
            foreach (var id in new[] { board.Id, Guid.NewGuid() }) // não revela se existe
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Put, $"/api/boards/{id}", new { name = "Novo" })).Status);
                Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Delete, $"/api/boards/{id}")).Status);
            }
        }

        // nada foi alterado
        await using var db = TestDb.Open(Cs);
        Assert.True(await db.Boards.AnyAsync(b => b.Id == board.Id));
    }

    [Fact]
    public async Task Sem_sessao_responde_401()
    {
        var client = postgres.Factory.CreateNoRedirectClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/boards")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Create(client, "X")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Put, $"/api/boards/{Guid.NewGuid()}", new { name = "X" })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Delete, $"/api/boards/{Guid.NewGuid()}")).Status);
    }

    // ---- listar ----

    [Fact]
    public async Task Lista_traz_nome_data_e_contagens_em_ordem_pt_br()
    {
        var admin = await AdminClient();
        var user = await TestDb.AddUserAsync(Cs);
        var comDados = await NewBoard(admin, $"Água {Guid.NewGuid():N}");
        var vazio = await NewBoard(admin, $"Zebra {Guid.NewGuid():N}");
        await TestData.AddTransactionAsync(Cs, comDados, user.Id, "entrada", 10m, "2026-05-01");
        await TestData.AddTransactionAsync(Cs, comDados, user.Id, "saida", 5m, "2026-05-02");
        await TestData.AddReserveAsync(Cs, comDados, user.Id, "Caixinha", 100m);
        await AddShareLink(comDados, user.Id, revoked: false);
        await AddShareLink(comDados, user.Id, revoked: true); // revogado não conta

        var (status, list) = await admin.GetJsonAsync("/api/admin/boards");

        Assert.Equal(HttpStatusCode.OK, status);
        var boards = list.EnumerateArray().ToList();
        var names = boards.Select(b => b.GetProperty("name").GetString()!).ToList();
        Assert.Equal(names.Order(Financas.Api.Common.PtBr.Comparer).ToList(), names);
        var row = boards.Single(b => b.GetProperty("id").GetGuid() == comDados);
        Assert.Equal(2, row.GetProperty("transactionCount").GetInt32());
        Assert.Equal(1, row.GetProperty("reserveCount").GetInt32());
        Assert.Equal(1, row.GetProperty("shareLinkCount").GetInt32());
        Assert.True(DateTimeOffset.TryParse(row.GetProperty("createdAt").GetString(), out _));
        var empty = boards.Single(b => b.GetProperty("id").GetGuid() == vazio);
        Assert.Equal(0, empty.GetProperty("transactionCount").GetInt32());
        Assert.Equal(0, empty.GetProperty("reserveCount").GetInt32());
        Assert.Equal(0, empty.GetProperty("shareLinkCount").GetInt32());
    }

    private async Task AddShareLink(Guid boardId, Guid userId, bool revoked)
    {
        await using var db = TestDb.Open(Cs);
        db.BoardShareLinks.Add(new BoardShareLink
        {
            BoardId = boardId, CreatedBy = userId, TokenHash = Guid.NewGuid().ToString("N"),
            RevokedAt = revoked ? DateTimeOffset.UtcNow : null,
        });
        await db.SaveChangesAsync();
    }

    // ---- criar ----

    [Fact]
    public async Task Cria_board_com_nome_sem_espacos_nas_pontas_e_o_admin_ja_o_enxerga()
    {
        var admin = await AdminClient();
        var name = $"Eventos {Guid.NewGuid():N}";

        var (status, body) = await Create(admin, $"   {name}  ");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(name, body.GetProperty("name").GetString());
        var id = body.GetProperty("id").GetGuid();
        var (_, mine) = await admin.GetJsonAsync("/api/boards");
        Assert.Contains(id, mine.EnumerateArray().Select(b => b.GetProperty("id").GetGuid()));
        var (getStatus, detail) = await admin.GetJsonAsync($"/api/boards/{id}");
        Assert.Equal(HttpStatusCode.OK, getStatus);
        Assert.True(detail.GetProperty("canMutate").GetBoolean());
    }

    [Fact]
    public async Task Board_novo_vai_para_a_conta_mais_antiga_sem_criar_outra()
    {
        var admin = await AdminClient();
        await using var db = TestDb.Open(Cs);
        var oldest = await db.Accounts.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.Id).FirstAsync();
        var accountsBefore = await db.Accounts.CountAsync();

        var id = await NewBoard(admin);

        Assert.Equal(oldest, (await db.Boards.AsNoTracking().SingleAsync(b => b.Id == id)).AccountId);
        Assert.Equal(accountsBefore, await db.Accounts.CountAsync());
    }

    [Fact]
    public async Task Sem_nenhuma_conta_a_api_cria_a_conta_principal()
    {
        // Banco próprio, vazio: nos demais testes sempre existe conta.
        var database = $"vazio_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(Cs))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"create database {database}", admin);
            await command.ExecuteNonQueryAsync();
        }
        var cs = new NpgsqlConnectionStringBuilder(Cs) { Database = database, Pooling = false }.ConnectionString;
        await using (var migrate = TestDb.Open(cs)) await migrate.Database.MigrateAsync();
        var user = await TestDb.AddUserAsync(cs);
        await TestData.AddRoleAsync(cs, user.Id, null, "admin");
        using var factory = new ApiFactory(cs);
        var client = await factory.SignedInClientAsync(user.Id);

        var (status, body) = await Create(client, "Primeiro");

        Assert.Equal(HttpStatusCode.OK, status);
        await using var db = TestDb.Open(cs);
        var account = await db.Accounts.SingleAsync();
        Assert.Equal("Conta principal", account.Name);
        Assert.Equal(account.Id, (await db.Boards.SingleAsync(b => b.Id == body.GetProperty("id").GetGuid())).AccountId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nome_vazio_responde_400(string? name)
    {
        var (status, body) = await Create(await AdminClient(), name);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Nome_com_80_caracteres_passa_e_com_81_e_recusado()
    {
        var admin = await AdminClient();

        Assert.Equal(HttpStatusCode.OK, (await Create(admin, new string('a', 79) + Guid.NewGuid().ToString("N")[..1])).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, new string('b', 81))).Status);
    }

    [Fact]
    public async Task Nome_repetido_responde_409_sem_distinguir_maiusculas_nem_espacos()
    {
        var admin = await AdminClient();
        var name = $"Social {Guid.NewGuid():N}";
        await NewBoard(admin, name);

        Assert.Equal(HttpStatusCode.Conflict, (await Create(admin, name)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Create(admin, $"  {name.ToUpperInvariant()} ")).Status);
    }

    // ---- editar ----

    [Fact]
    public async Task Renomeia_o_board()
    {
        var admin = await AdminClient();
        var id = await NewBoard(admin);
        var name = $"Novo nome {Guid.NewGuid():N}";

        var (status, body) = await Send(admin, HttpMethod.Put, $"/api/boards/{id}", new { name = $" {name} " });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(name, body.GetProperty("name").GetString());
        var (_, detail) = await admin.GetJsonAsync($"/api/boards/{id}");
        Assert.Equal(name, detail.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Pode_trocar_so_as_maiusculas_do_proprio_nome_mas_nao_usar_o_de_outro_board()
    {
        var admin = await AdminClient();
        var name = $"Caixa {Guid.NewGuid():N}";
        var id = await NewBoard(admin, name);
        var outro = await NewBoard(admin);

        Assert.Equal(HttpStatusCode.OK, (await Send(admin, HttpMethod.Put, $"/api/boards/{id}", new { name = name.ToUpperInvariant() })).Status);
        Assert.Equal(HttpStatusCode.OK, (await Send(admin, HttpMethod.Put, $"/api/boards/{id}", new { name = name.ToUpperInvariant() })).Status); // mesmo nome
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, HttpMethod.Put, $"/api/boards/{outro}", new { name })).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Renomear_para_vazio_responde_400(string name)
    {
        var admin = await AdminClient();
        var id = await NewBoard(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Put, $"/api/boards/{id}", new { name })).Status);
    }

    [Fact]
    public async Task Renomear_board_inexistente_responde_404()
    {
        var (status, _) = await Send(await AdminClient(), HttpMethod.Put, $"/api/boards/{Guid.NewGuid()}", new { name = "X" });

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    // ---- remover ----

    [Fact]
    public async Task Remove_o_board_e_apaga_em_cascata_so_o_que_era_dele()
    {
        var admin = await AdminClient();
        var user = await TestDb.AddUserAsync(Cs);
        var alvo = await NewBoard(admin);
        var outro = await NewBoard(admin);
        foreach (var board in new[] { alvo, outro })
        {
            await TestData.AddTransactionAsync(Cs, board, user.Id, "entrada", 10m, "2026-05-01");
            await TestData.AddReserveAsync(Cs, board, user.Id, "Caixinha", 50m);
            await AddShareLink(board, user.Id, revoked: false);
        }
        var scoped = await TestData.AddUserWithRoleAsync(Cs, alvo, "tesoureiro");

        var (status, _) = await Send(admin, HttpMethod.Delete, $"/api/boards/{alvo}");

        Assert.Equal(HttpStatusCode.NoContent, status);
        await using var db = TestDb.Open(Cs);
        Assert.False(await db.Boards.AnyAsync(b => b.Id == alvo));
        Assert.False(await db.Transactions.AnyAsync(t => t.BoardId == alvo));
        Assert.False(await db.FinancialReserves.AnyAsync(r => r.BoardId == alvo));
        Assert.False(await db.BoardShareLinks.AnyAsync(l => l.BoardId == alvo));
        Assert.False(await db.UserRoles.AnyAsync(r => r.BoardId == alvo));
        // o outro board e os usuários ficam intactos
        Assert.True(await db.Boards.AnyAsync(b => b.Id == outro));
        Assert.Equal(1, await db.Transactions.CountAsync(t => t.BoardId == outro));
        Assert.Equal(1, await db.FinancialReserves.CountAsync(r => r.BoardId == outro));
        Assert.Equal(1, await db.BoardShareLinks.CountAsync(l => l.BoardId == outro));
        Assert.True(await db.Users.AnyAsync(u => u.Id == scoped.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/boards/{alvo}")).StatusCode);
    }

    [Fact]
    public async Task Remover_board_inexistente_ou_ja_removido_responde_404()
    {
        var admin = await AdminClient();
        var id = await NewBoard(admin);

        Assert.Equal(HttpStatusCode.NoContent, (await Send(admin, HttpMethod.Delete, $"/api/boards/{id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Delete, $"/api/boards/{id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Delete, $"/api/boards/{Guid.NewGuid()}")).Status);
    }
}
