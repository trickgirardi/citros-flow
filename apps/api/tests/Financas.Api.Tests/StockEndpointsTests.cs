using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class StockEndpointsTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    // ---- montagem ----

    private async Task<HttpClient> ClientWith(Guid? boardId, params string[] roles)
    {
        var user = await TestDb.AddUserAsync(Cs, displayName: $"Pessoa {Guid.NewGuid():N}"[..14]);
        foreach (var role in roles) await TestData.AddRoleAsync(Cs, user.Id, boardId, role);
        return await postgres.Factory.SignedInClientAsync(user.Id);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Send(
        HttpClient client, HttpMethod method, string url, object? body = null)
    {
        var response = await client.SendJsonAsync(method, url, body);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length > 0 && response.Content.Headers.ContentType?.MediaType?.Contains("json") == true
            ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    private static object Body(string name = "Papel A4", string status = "comprar", Guid? assignee = null,
        string? price = null, string? details = null) =>
        new { name, status, assigneeId = assignee, price, details };

    private static Task<(HttpStatusCode Status, JsonElement Body)> Create(HttpClient client, Guid board, object body) =>
        Send(client, HttpMethod.Post, $"/api/boards/{board}/stock-items", body);

    private async Task<Guid> NewItem(HttpClient admin, Guid board, string name = "Item", string status = "comprar")
    {
        var (code, body) = await Create(admin, board, Body(name, status));
        Assert.Equal(HttpStatusCode.OK, code);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> Items(HttpClient client, Guid board)
    {
        var (code, body) = await client.GetJsonAsync($"/api/boards/{board}/stock-items");
        Assert.Equal(HttpStatusCode.OK, code);
        return body.EnumerateArray().ToList();
    }

    // ---- permissões ----

    [Theory]
    [InlineData("admin")]
    [InlineData("junta_administrativa")]
    [InlineData("tesoureiro", "junta_administrativa")] // os papéis se somam
    public async Task Admin_e_junta_escrevem_no_estoque(params string[] roles)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var client = await ClientWith(board.Id, roles);

        var (created, body) = await Create(client, board.Id, Body("Cadeira"));
        Assert.Equal(HttpStatusCode.OK, created);
        var id = body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/api/boards/{board.Id}/stock-items/{id}", Body("Cadeira nova"))).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Patch, $"/api/boards/{board.Id}/stock-items/{id}/status", new { status = "emEstoque" })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Delete, $"/api/boards/{board.Id}/stock-items/{id}")).Status);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("junta_administrativa")]
    public async Task Papel_global_de_admin_ou_junta_escreve_em_qualquer_board(string role)
    {
        var board = await TestData.AddBoardAsync(Cs);

        Assert.Equal(HttpStatusCode.OK, (await Create(await ClientWith(null, role), board.Id, Body())).Status);
    }

    [Theory]
    [InlineData("tesoureiro")]
    [InlineData("viewer")]
    [InlineData("tesoureiro", "viewer")]
    public async Task Tesoureiro_e_viewer_leem_mas_nao_escrevem(params string[] roles)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var id = await NewItem(admin, board.Id);
        var client = await ClientWith(board.Id, roles);

        Assert.Single(await Items(client, board.Id)); // leitura liberada
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(client, board.Id, Body())).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Put, $"/api/boards/{board.Id}/stock-items/{id}", Body("X"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Patch, $"/api/boards/{board.Id}/stock-items/{id}/status", new { status = "emEstoque" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Delete, $"/api/boards/{board.Id}/stock-items/{id}")).Status);
        var item = Assert.Single(await Items(admin, board.Id));
        Assert.Equal("comprar", item.GetProperty("status").GetString()); // nada mudou
    }

    [Fact]
    public async Task Papel_so_vale_no_proprio_board_e_sem_papel_ou_sessao_nao_acessa()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var juntaDoOutro = await ClientWith(outro.Id, "junta_administrativa");

        Assert.Equal(HttpStatusCode.NotFound, (await Create(juntaDoOutro, board.Id, Body())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await juntaDoOutro.GetAsync($"/api/boards/{board.Id}/stock-items")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await ClientWith(null)).GetAsync($"/api/boards/{board.Id}/stock-items")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await postgres.Factory.CreateNoRedirectClient().GetAsync($"/api/boards/{board.Id}/stock-items")).StatusCode);
    }

    // ---- criar e validar ----

    [Fact]
    public async Task Cria_item_com_todos_os_campos_normalizados()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var pessoa = await TestDb.AddUserAsync(Cs, displayName: "Fulana");
        await TestData.AddRoleAsync(Cs, pessoa.Id, board.Id, "viewer");

        var (status, body) = await Create(admin, board.Id,
            Body("  Resma de papel  ", "aCaminho", pessoa.Id, "12.345", "  Comprar da marca X  "));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Resma de papel", body.GetProperty("name").GetString());
        Assert.Equal("aCaminho", body.GetProperty("status").GetString());
        Assert.Equal("12.35", body.GetProperty("price").GetString()); // metade para longe do zero, string decimal
        Assert.Equal("Comprar da marca X", body.GetProperty("details").GetString());
        Assert.Equal("Fulana", body.GetProperty("assignee").GetProperty("name").GetString());
        Assert.Equal(pessoa.Id, body.GetProperty("assignee").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task So_item_e_status_sao_obrigatorios()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");

        var (status, body) = await Send(admin, HttpMethod.Post, $"/api/boards/{board.Id}/stock-items", new { name = "Só o essencial", status = "emEstoque" });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("price").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("assignee").ValueKind);
        Assert.Equal("", body.GetProperty("details").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Item_e_obrigatorio(string? name)
    {
        var board = await TestData.AddBoardAsync(Cs);

        var (status, body) = await Create(await ClientWith(board.Id, "admin"), board.Id, new { name, status = "comprar" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("Item", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Status_e_obrigatorio_e_desconhecido_responde_400()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");

        var (semStatus, body) = await Create(admin, board.Id, new { name = "Sem status" });
        Assert.Equal(HttpStatusCode.BadRequest, semStatus);
        Assert.Contains("Status", body.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(status: "pendente"))).Status);
    }

    [Fact]
    public async Task Limites_de_tamanho()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");

        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(new string('a', 120)))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(new string('a', 121)))).Status);
        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(details: new string('d', 2000)))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(details: new string('d', 2001)))).Status);
    }

    [Theory]
    [InlineData("0", "0.00")]
    [InlineData("0.00", "0.00")]
    [InlineData("10", "10.00")]
    [InlineData("1234.5", "1234.50")]
    [InlineData("0.005", "0.01")]
    [InlineData("0.004", "0.00")]
    [InlineData("9999999999.99", "9999999999.99")]
    public async Task Preco_valido_vira_centavos_exatos(string sent, string saved)
    {
        var board = await TestData.AddBoardAsync(Cs);

        var (status, body) = await Create(await ClientWith(board.Id, "admin"), board.Id, Body(price: sent));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(saved, body.GetProperty("price").GetString());
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-5")]
    [InlineData("10000000000")]
    [InlineData("abc")]
    public async Task Preco_invalido_responde_400(string sent)
    {
        var board = await TestData.AddBoardAsync(Cs);

        var (status, _) = await Create(await ClientWith(board.Id, "admin"), board.Id, Body(price: sent));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Responsavel_precisa_ser_pessoa_ativa_com_acesso_ao_board()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var doBoard = await TestData.AddUserWithRoleAsync(Cs, board.Id, "tesoureiro");
        var global = await TestData.AddUserWithRoleAsync(Cs, null, "viewer");
        var doOutro = await TestData.AddUserWithRoleAsync(Cs, outro.Id, "admin");
        var semPapel = await TestDb.AddUserAsync(Cs);
        var inativo = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        await using (var db = TestDb.Open(Cs))
            await db.Users.Where(u => u.Id == inativo.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(assignee: doBoard.Id))).Status);
        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(assignee: global.Id))).Status);
        foreach (var invalid in new[] { doOutro.Id, semPapel.Id, inativo.Id, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(assignee: invalid))).Status);
    }

    // ---- listar ----

    [Fact]
    public async Task Lista_so_os_itens_do_board_em_ordem_pt_br()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        foreach (var name in new[] { "Zinco", "Água", "Bandeira" }) await NewItem(admin, board.Id, name);
        await NewItem(admin, outro.Id, "De outro board");

        var items = await Items(admin, board.Id);

        Assert.Equal(["Água", "Bandeira", "Zinco"], items.Select(i => i.GetProperty("name").GetString()!));
    }

    [Fact]
    public async Task Responsavel_sem_nome_aparece_pelo_email()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var user = await TestDb.AddUserAsync(Cs, email: $"{Guid.NewGuid():N}@teste.org");
        await TestData.AddRoleAsync(Cs, user.Id, board.Id, "viewer");
        await Create(admin, board.Id, Body(assignee: user.Id));

        var item = Assert.Single(await Items(admin, board.Id));

        Assert.Equal(user.Email, item.GetProperty("assignee").GetProperty("name").GetString());
    }

    // ---- editar ----

    [Fact]
    public async Task Edita_todos_os_campos_e_pode_limpar_responsavel_e_preco()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var pessoa = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var (_, created) = await Create(admin, board.Id, Body("Antes", "comprar", pessoa.Id, "10.00", "velho"));
        var id = created.GetProperty("id").GetGuid();

        var (status, body) = await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/stock-items/{id}",
            Body("Depois", "emEstoque", null, null, "novo"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Depois", body.GetProperty("name").GetString());
        Assert.Equal("emEstoque", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("price").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("assignee").ValueKind);
        await using var db = TestDb.Open(Cs);
        var saved = await db.StockItems.AsNoTracking().SingleAsync(i => i.Id == id);
        Assert.NotNull(saved.UpdatedAt);
        Assert.NotNull(saved.UpdatedBy);
    }

    [Fact]
    public async Task Editar_valida_e_item_de_outro_board_ou_inexistente_e_404()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        var id = await NewItem(admin, board.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/stock-items/{id}", Body(""))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Put, $"/api/boards/{outro.Id}/stock-items/{id}", Body("X"))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/stock-items/{Guid.NewGuid()}", Body("X"))).Status);
    }

    // ---- status ----

    [Theory]
    [InlineData("emEstoque")]
    [InlineData("aCaminho")]
    [InlineData("comprar")]
    [InlineData("comprarComUrgencia")]
    public async Task Troca_so_o_status_e_mantem_o_resto(string status)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var pessoa = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var (_, created) = await Create(admin, board.Id, Body("Lâmpada", "emEstoque", pessoa.Id, "7.50", "LED"));
        var id = created.GetProperty("id").GetGuid();

        var (code, _) = await Send(admin, HttpMethod.Patch, $"/api/boards/{board.Id}/stock-items/{id}/status", new { status });

        Assert.Equal(HttpStatusCode.NoContent, code);
        var item = Assert.Single(await Items(admin, board.Id));
        Assert.Equal(status, item.GetProperty("status").GetString());
        Assert.Equal("Lâmpada", item.GetProperty("name").GetString());
        Assert.Equal("7.50", item.GetProperty("price").GetString());
        Assert.Equal(pessoa.Id, item.GetProperty("assignee").GetProperty("id").GetGuid());
        Assert.Equal("LED", item.GetProperty("details").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("pendente")]
    public async Task Status_invalido_na_troca_responde_400(string? status)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var id = await NewItem(admin, board.Id);

        var (code, _) = await Send(admin, HttpMethod.Patch, $"/api/boards/{board.Id}/stock-items/{id}/status", new { status });

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Trocar_status_de_item_de_outro_board_ou_inexistente_e_404()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        var id = await NewItem(admin, board.Id);

        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(admin, HttpMethod.Patch, $"/api/boards/{outro.Id}/stock-items/{id}/status", new { status = "emEstoque" })).Status);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(admin, HttpMethod.Patch, $"/api/boards/{board.Id}/stock-items/{Guid.NewGuid()}/status", new { status = "emEstoque" })).Status);
    }

    // ---- remover ----

    [Fact]
    public async Task Remove_o_item_sem_afetar_os_outros_nem_a_pessoa()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var pessoa = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var (_, created) = await Create(admin, board.Id, Body("Some", assignee: pessoa.Id));
        var id = created.GetProperty("id").GetGuid();
        await NewItem(admin, board.Id, "Fica");

        Assert.Equal(HttpStatusCode.NoContent, (await Send(admin, HttpMethod.Delete, $"/api/boards/{board.Id}/stock-items/{id}")).Status);

        Assert.Equal(["Fica"], (await Items(admin, board.Id)).Select(i => i.GetProperty("name").GetString()!));
        await using var db = TestDb.Open(Cs);
        Assert.True(await db.Users.AnyAsync(u => u.Id == pessoa.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Delete, $"/api/boards/{board.Id}/stock-items/{id}")).Status);
    }

    [Fact]
    public async Task Remover_item_de_outro_board_e_404_e_nao_apaga()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        var id = await NewItem(admin, board.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Delete, $"/api/boards/{outro.Id}/stock-items/{id}")).Status);
        Assert.Single(await Items(admin, board.Id));
    }

    [Fact]
    public async Task Remover_o_board_apaga_os_itens()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        await NewItem(admin, board.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await Send(admin, HttpMethod.Delete, $"/api/boards/{board.Id}")).Status);

        await using var db = TestDb.Open(Cs);
        Assert.False(await db.StockItems.AnyAsync(i => i.BoardId == board.Id));
    }
}
