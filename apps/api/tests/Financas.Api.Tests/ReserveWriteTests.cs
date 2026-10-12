using System.Net;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class ReserveWriteTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private async Task<(Board Board, User User, HttpClient Client)> Setup(string role = "tesoureiro")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board, user, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    private async Task<List<FinancialReserve>> Reserves(Guid boardId)
    {
        await using var db = TestDb.Open(Cs);
        return await db.FinancialReserves.AsNoTracking().Where(r => r.BoardId == boardId).OrderBy(r => r.CreatedAt).ToListAsync();
    }

    private async Task<Guid> Existing(Guid boardId, Guid userId, string name = "Caixinha", decimal amount = 100m)
    {
        await TestData.AddReserveAsync(Cs, boardId, userId, name, amount);
        return (await Reserves(boardId)).Last().Id;
    }

    [Fact]
    public async Task Criar_grava_com_created_by_da_sessao_e_entra_no_total_do_resumo()
    {
        var (board, user, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/reserves",
            new { name = "  Caixinha  ", amount = "100.005" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.ReadJsonAsync();
        Assert.Equal("Caixinha", created.GetProperty("name").GetString());
        Assert.Equal("100.01", created.GetProperty("amount").GetString());
        var saved = Assert.Single(await Reserves(board.Id));
        Assert.Equal(user.Id, saved.CreatedBy);

        var (_, summary) = await client.GetJsonAsync($"/api/boards/{board.Id}/months/2026-05");
        Assert.Equal("100.01", summary.GetProperty("reservesTotal").GetString());
    }

    public static TheoryData<string?, string> CorposInvalidos => new()
    {
        { "", "10" }, { "   ", "10" }, { null, "10" }, { "Nome", "0" }, { "Nome", "-1" }, { "Nome", "0.004" },
        { "Nome", "abc" }, { "Nome", "10000000000.00" },
    };

    [Theory]
    [MemberData(nameof(CorposInvalidos))]
    public async Task Criar_invalido_responde_400_e_nao_grava(string? name, string amount)
    {
        var (board, _, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/reserves", new { name, amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Reserves(board.Id));
    }

    [Fact]
    public async Task Nome_de_ate_80_caracteres_passa_e_81_nao()
    {
        var (board, _, client) = await Setup();

        Assert.Equal(HttpStatusCode.OK, (await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/reserves",
            new { name = new string('n', 80), amount = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/reserves",
            new { name = new string('n', 81), amount = "1" })).StatusCode);
    }

    [Fact]
    public async Task Editar_muda_nome_e_valor()
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        var response = await client.SendJsonAsync(HttpMethod.Put, $"/api/boards/{board.Id}/reserves/{id}",
            new { name = "Poupança", amount = "250.50" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = Assert.Single(await Reserves(board.Id));
        Assert.Equal(("Poupança", 250.50m), (saved.Name, saved.Amount));
        Assert.Equal(user.Id, saved.CreatedBy); // quem criou não muda
    }

    [Fact]
    public async Task Editar_e_remover_reserva_de_outro_board_e_404_e_nao_altera()
    {
        var (meu, user, client) = await Setup();
        var alheio = await TestData.AddBoardAsync(Cs);
        var id = await Existing(alheio.Id, user.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.SendJsonAsync(HttpMethod.Put,
            $"/api/boards/{meu.Id}/reserves/{id}", new { name = "invadida", amount = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{meu.Id}/reserves/{id}")).StatusCode);

        var saved = Assert.Single(await Reserves(alheio.Id));
        Assert.Equal(("Caixinha", 100m), (saved.Name, saved.Amount));
    }

    [Fact]
    public async Task Remover_apaga_e_a_segunda_vez_e_404()
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/reserves/{id}")).StatusCode);
        Assert.Empty(await Reserves(board.Id));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/reserves/{id}")).StatusCode);
    }

    [Fact]
    public async Task Viewer_nao_cria_edita_nem_remove()
    {
        var (board, owner, _) = await Setup();
        var id = await Existing(board.Id, owner.Id);
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var client = await postgres.Factory.SignedInClientAsync(viewer.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendJsonAsync(HttpMethod.Post,
            $"/api/boards/{board.Id}/reserves", new { name = "x", amount = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendJsonAsync(HttpMethod.Put,
            $"/api/boards/{board.Id}/reserves/{id}", new { name = "x", amount = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/reserves/{id}")).StatusCode);

        var saved = Assert.Single(await Reserves(board.Id));
        Assert.Equal("Caixinha", saved.Name);
    }

    [Fact]
    public async Task Sem_sessao_401_e_sem_acesso_ao_board_404()
    {
        var (board, _, _) = await Setup();
        var intruso = await TestDb.AddUserAsync(Cs);

        Assert.Equal(HttpStatusCode.Unauthorized, (await postgres.Factory.CreateNoRedirectClient().SendJsonAsync(
            HttpMethod.Post, $"/api/boards/{board.Id}/reserves", new { name = "x", amount = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await postgres.Factory.SignedInClientAsync(intruso.Id)).SendJsonAsync(
            HttpMethod.Post, $"/api/boards/{board.Id}/reserves", new { name = "x", amount = "1" })).StatusCode);
        Assert.Empty(await Reserves(board.Id));
    }
}
