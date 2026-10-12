using System.Net;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class TransactionWriteTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private static object Body(
        string type = "entrada", object? amount = null, string? description = "Mensalidade",
        string? category = "Doações", string date = "2026-05-10") =>
        new { type, amount = amount ?? "10.50", description, category, date };

    private async Task<(Board Board, User User, HttpClient Client)> Setup(string role = "tesoureiro")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board, user, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    private async Task<Transaction> Load(Guid id)
    {
        await using var db = TestDb.Open(Cs);
        return await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == id);
    }

    private async Task<int> Count(Guid boardId)
    {
        await using var db = TestDb.Open(Cs);
        return await db.Transactions.CountAsync(t => t.BoardId == boardId);
    }

    private async Task<Guid> Existing(Guid boardId, Guid userId, string description = "Original")
    {
        await TestData.AddTransactionAsync(Cs, boardId, userId, "saida", 5m, "2026-05-01", "Luz", description);
        await using var db = TestDb.Open(Cs);
        return await db.Transactions.Where(t => t.BoardId == boardId && t.Description == description)
            .OrderByDescending(t => t.CreatedAt).Select(t => t.Id).FirstAsync();
    }

    // ---------- criar ----------

    [Fact]
    public async Task Criar_grava_com_created_by_da_sessao_e_aparece_no_resumo_do_mes()
    {
        var (board, user, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions",
            Body(description: "  Mensalidade  ", category: " Doações "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.ReadJsonAsync();
        Assert.Equal("10.50", created.GetProperty("amount").GetString());
        Assert.Equal("entrada", created.GetProperty("type").GetString());
        Assert.Equal("Mensalidade", created.GetProperty("description").GetString());
        Assert.Equal("2026-05-10", created.GetProperty("date").GetString());

        var saved = await Load(created.GetProperty("id").GetGuid());
        Assert.Equal(board.Id, saved.BoardId);
        Assert.Equal(user.Id, saved.CreatedBy);
        Assert.Equal("Doações", saved.Category);
        Assert.Null(saved.UpdatedBy);
        Assert.Null(saved.UpdatedAt);

        var (_, summary) = await client.GetJsonAsync($"/api/boards/{board.Id}/months/2026-05");
        Assert.Equal("10.50", summary.GetProperty("balance").GetString());
    }

    [Fact]
    public async Task Criar_ignora_created_by_enviado_no_corpo()
    {
        var (board, user, client) = await Setup();
        var outro = await TestDb.AddUserAsync(Cs);

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions",
            new { type = "entrada", amount = "1.00", description = "x", category = "y", date = "2026-05-10", createdBy = outro.Id });

        var saved = await Load((await response.ReadJsonAsync()).GetProperty("id").GetGuid());
        Assert.Equal(user.Id, saved.CreatedBy);
    }

    [Theory]
    [InlineData("10.005", "10.01")]  // metade sobe, como o Math.round do front
    [InlineData("10.004", "10.00")]
    [InlineData("0.01", "0.01")]
    [InlineData("9999999999.99", "9999999999.99")]
    public async Task Valor_e_arredondado_para_centavos(string sent, string stored)
    {
        var (board, _, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions", Body(amount: sent));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(stored, (await response.ReadJsonAsync()).GetProperty("amount").GetString());
    }

    [Fact]
    public async Task Valor_pode_vir_como_numero_json_mas_o_formato_oficial_e_string()
    {
        var (board, _, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions", Body(amount: 10.5m));

        Assert.Equal("10.50", (await response.ReadJsonAsync()).GetProperty("amount").GetString());
    }

    public static TheoryData<string, object?, string?, string?, string> CorposInvalidos => new()
    {
        { "entrada", "0", "d", "c", "2026-05-10" },                 // valor zero
        { "entrada", "0.004", "d", "c", "2026-05-10" },             // arredonda para zero
        { "entrada", "-5", "d", "c", "2026-05-10" },                // negativo
        { "entrada", "abc", "d", "c", "2026-05-10" },               // não numérico
        { "entrada", "1,50", "d", "c", "2026-05-10" },              // vírgula: o front normaliza antes
        { "entrada", "10000000000.00", "d", "c", "2026-05-10" },    // estoura numeric(12,2)
        { "entrada", "10", null, "c", "2026-05-10" },               // sem descrição
        { "entrada", "10", "   ", "c", "2026-05-10" },
        { "entrada", "10", "d", "", "2026-05-10" },                 // sem categoria
        { "entrada", "10", "d", null, "2026-05-10" },
        { "entrada", "10", "d", new string('c', 81), "2026-05-10" },    // categoria longa demais
        { "entrada", "10", new string('d', 501), "c", "2026-05-10" },   // acima do teto de segurança
        { "outro", "10", "d", "c", "2026-05-10" },                  // tipo desconhecido
        { "entrada", "10", "d", "c", "2026-02-30" },                // data inexistente
        { "entrada", "10", "d", "c", "10/05/2026" },                // formato de data errado
    };

    [Theory]
    [MemberData(nameof(CorposInvalidos))]
    public async Task Criar_com_dados_invalidos_responde_400_e_nao_grava(
        string type, object? amount, string? description, string? category, string date)
    {
        var (board, _, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions",
            Body(type, amount, description, category, date));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Count(board.Id));
    }

    [Fact]
    public async Task Tipo_numerico_nao_vale_como_enum()
    {
        var (board, _, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions",
            new { type = 1, amount = "10.00", description = "d", category = "c", date = "2026-05-10" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Count(board.Id));
    }

    [Fact]
    public async Task Descricao_de_ate_500_caracteres_e_aceita_no_criar_mesmo_acima_de_120()
    {
        var (board, _, client) = await Setup();

        foreach (var length in new[] { 121, 500 })
        {
            var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions",
                Body(description: new string('d', length)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ---------- permissões ----------

    [Fact]
    public async Task Sem_sessao_e_401()
    {
        var (board, _, _) = await Setup();

        var response = await postgres.Factory.CreateNoRedirectClient()
            .SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("viewer")]
    public async Task Viewer_nao_cria_403(string role)
    {
        var (board, _, client) = await Setup(role);

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions", Body());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await Count(board.Id));
    }

    [Fact]
    public async Task Quem_nao_ve_o_board_recebe_404_mesmo_sendo_tesoureiro_de_outro()
    {
        var (_, _, client) = await Setup();
        var alheio = await TestData.AddBoardAsync(Cs);

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{alheio.Id}/transactions", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await Count(alheio.Id));
    }

    [Theory]
    [InlineData("tesoureiro", HttpStatusCode.OK)]
    [InlineData("admin", HttpStatusCode.OK)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    public async Task Papel_global_vale_para_qualquer_board(string role, HttpStatusCode expected)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, null, role);
        var client = await postgres.Factory.SignedInClientAsync(user.Id);

        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions", Body());

        Assert.Equal(expected, response.StatusCode);
    }

    // ---------- editar ----------

    [Fact]
    public async Task Editar_muda_tudo_preenche_updated_e_preserva_created_by()
    {
        var (board, user, client) = await Setup();
        var criador = await TestDb.AddUserAsync(Cs);
        var id = await Existing(board.Id, criador.Id);

        var response = await client.SendJsonAsync(HttpMethod.Put, $"/api/boards/{board.Id}/transactions/{id}",
            Body("entrada", "99.90", " Nova ", " Outra ", "2026-06-15"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await Load(id);
        Assert.Equal(("entrada", 99.90m, "Nova", "Outra", new DateOnly(2026, 6, 15)),
            (saved.Type, saved.Amount, saved.Description, saved.Category, saved.Date));
        Assert.Equal(criador.Id, saved.CreatedBy);
        Assert.Equal(user.Id, saved.UpdatedBy);
        Assert.InRange(DateTimeOffset.UtcNow - saved.UpdatedAt!.Value, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Editar_transacao_de_outro_board_e_404_e_nao_altera_nada()
    {
        var (meu, user, client) = await Setup();
        var alheio = await TestData.AddBoardAsync(Cs);
        var id = await Existing(alheio.Id, user.Id);

        // mesmo com o id certo, o board da URL é o que vale
        var response = await client.SendJsonAsync(HttpMethod.Put, $"/api/boards/{meu.Id}/transactions/{id}", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var untouched = await Load(id);
        Assert.Equal("Original", untouched.Description);
        Assert.Null(untouched.UpdatedAt);
    }

    [Fact]
    public async Task Editar_inexistente_ou_invalido_ou_como_viewer_nao_altera()
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.SendJsonAsync(HttpMethod.Put, $"/api/boards/{board.Id}/transactions/{Guid.NewGuid()}", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.SendJsonAsync(HttpMethod.Put, $"/api/boards/{board.Id}/transactions/{id}", Body(amount: "0"))).StatusCode);

        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var viewerClient = await postgres.Factory.SignedInClientAsync(viewer.Id);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewerClient.SendJsonAsync(HttpMethod.Put, $"/api/boards/{board.Id}/transactions/{id}", Body())).StatusCode);

        Assert.Equal("Original", (await Load(id)).Description);
    }

    // ---------- descrição inline ----------

    [Fact]
    public async Task Descricao_inline_muda_so_a_descricao_e_preenche_updated()
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        var response = await client.SendJsonAsync(HttpMethod.Patch,
            $"/api/boards/{board.Id}/transactions/{id}/description", new { description = "  Nova descrição " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await Load(id);
        Assert.Equal(("Nova descrição", "saida", 5m, "Luz"), (saved.Description, saved.Type, saved.Amount, saved.Category));
        Assert.Equal(user.Id, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Theory]
    [InlineData(120, HttpStatusCode.OK)]
    [InlineData(121, HttpStatusCode.BadRequest)] // o editor inline sempre limitou a 120
    [InlineData(0, HttpStatusCode.BadRequest)]
    public async Task Descricao_inline_respeita_o_limite_de_120(int length, HttpStatusCode expected)
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        var response = await client.SendJsonAsync(HttpMethod.Patch,
            $"/api/boards/{board.Id}/transactions/{id}/description", new { description = new string('d', length) });

        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) Assert.Equal("Original", (await Load(id)).Description);
    }

    [Fact]
    public async Task Descricao_inline_de_outro_board_ou_como_viewer_nao_altera()
    {
        var (meu, user, client) = await Setup();
        var alheio = await TestData.AddBoardAsync(Cs);
        var idAlheio = await Existing(alheio.Id, user.Id);
        var idMeu = await Existing(meu.Id, user.Id, "Meu");
        var viewer = await TestData.AddUserWithRoleAsync(Cs, meu.Id, "viewer");

        Assert.Equal(HttpStatusCode.NotFound, (await client.SendJsonAsync(HttpMethod.Patch,
            $"/api/boards/{meu.Id}/transactions/{idAlheio}/description", new { description = "invadido" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await postgres.Factory.SignedInClientAsync(viewer.Id)).SendJsonAsync(
            HttpMethod.Patch, $"/api/boards/{meu.Id}/transactions/{idMeu}/description", new { description = "x" })).StatusCode);

        Assert.Equal("Original", (await Load(idAlheio)).Description);
        Assert.Equal("Meu", (await Load(idMeu)).Description);
    }

    // ---------- categoria em lote ----------

    [Fact]
    public async Task Categoria_em_lote_so_atinge_transacoes_do_board_e_dedup_de_ids()
    {
        var (board, user, client) = await Setup();
        var alheio = await TestData.AddBoardAsync(Cs);
        var a = await Existing(board.Id, user.Id, "A");
        var b = await Existing(board.Id, user.Id, "B");
        var fora = await Existing(board.Id, user.Id, "Fora da seleção");
        var doOutroBoard = await Existing(alheio.Id, user.Id, "Do outro board");

        var response = await client.SendJsonAsync(HttpMethod.Patch, $"/api/boards/{board.Id}/transactions/category",
            new { ids = new[] { a, b, a, doOutroBoard, Guid.NewGuid() }, category = "  Nova categoria  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetProperty("updatedCount").GetInt32());
        foreach (var id in new[] { a, b })
        {
            var saved = await Load(id);
            Assert.Equal("Nova categoria", saved.Category);
            Assert.Equal(user.Id, saved.UpdatedBy);
            Assert.NotNull(saved.UpdatedAt);
        }
        Assert.Equal("Luz", (await Load(fora)).Category);
        Assert.Equal("Luz", (await Load(doOutroBoard)).Category);
        Assert.Null((await Load(doOutroBoard)).UpdatedAt);
    }

    public static TheoryData<string> CategoriasInvalidas => new() { "", "   ", new string('c', 81) };

    [Theory]
    [MemberData(nameof(CategoriasInvalidas))]
    public async Task Categoria_em_lote_invalida_responde_400_e_nao_altera(string category)
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        var response = await client.SendJsonAsync(HttpMethod.Patch, $"/api/boards/{board.Id}/transactions/category",
            new { ids = new[] { id }, category });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Luz", (await Load(id)).Category);
    }

    [Fact]
    public async Task Categoria_em_lote_sem_ids_responde_400()
    {
        var (board, _, client) = await Setup();

        foreach (var body in new object[] { new { ids = Array.Empty<Guid>(), category = "x" }, new { category = "x" } })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.SendJsonAsync(HttpMethod.Patch, $"/api/boards/{board.Id}/transactions/category", body)).StatusCode);
    }

    [Fact]
    public async Task Categoria_em_lote_como_viewer_e_403_e_nao_altera()
    {
        var (board, user, _) = await Setup();
        var id = await Existing(board.Id, user.Id);
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");

        var response = await (await postgres.Factory.SignedInClientAsync(viewer.Id)).SendJsonAsync(
            HttpMethod.Patch, $"/api/boards/{board.Id}/transactions/category", new { ids = new[] { id }, category = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Luz", (await Load(id)).Category);
    }

    // ---------- remover ----------

    [Fact]
    public async Task Remover_apaga_e_a_segunda_vez_e_404()
    {
        var (board, user, client) = await Setup();
        var id = await Existing(board.Id, user.Id);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/transactions/{id}")).StatusCode);
        Assert.Equal(0, await Count(board.Id));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/transactions/{id}")).StatusCode);
    }

    [Fact]
    public async Task Remover_de_outro_board_ou_como_viewer_nao_apaga()
    {
        var (meu, user, client) = await Setup();
        var alheio = await TestData.AddBoardAsync(Cs);
        var idAlheio = await Existing(alheio.Id, user.Id);
        var idMeu = await Existing(meu.Id, user.Id, "Meu");
        var viewer = await TestData.AddUserWithRoleAsync(Cs, meu.Id, "viewer");

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.SendJsonAsync(HttpMethod.Delete, $"/api/boards/{meu.Id}/transactions/{idAlheio}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await postgres.Factory.SignedInClientAsync(viewer.Id))
                .SendJsonAsync(HttpMethod.Delete, $"/api/boards/{meu.Id}/transactions/{idMeu}")).StatusCode);

        Assert.Equal(1, await Count(alheio.Id));
        Assert.Equal(1, await Count(meu.Id));
    }
}
