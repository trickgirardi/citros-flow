using System.Net;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class NubankImportEndpointTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private const string Header = "Data,Valor,Identificador,Descrição\n";
    private const string ValidCsv = Header
        + "01/05/2026,180.00,69f48267-ebdd-4b8f-9b4d-6185ecbdce00,Transferência recebida pelo Pix - FULANO DE TAL - NU PAGAMENTOS S.A.\n"
        + "02/05/2026,-45.90,abc,Compra no débito - MERCADO\n"
        + "03/05/2026,\"1234,56\",def,\"Depósito, com vírgula\"\n";

    private async Task<(Board Board, User User, HttpClient Client)> Setup(string role = "tesoureiro")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board, user, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    private async Task<List<Transaction>> Saved(Guid boardId)
    {
        await using var db = TestDb.Open(Cs);
        return await db.Transactions.AsNoTracking().Where(t => t.BoardId == boardId).OrderBy(t => t.Date).ToListAsync();
    }

    private static string Url(Guid boardId) => $"/api/boards/{boardId}/transactions/import/nubank";

    [Fact]
    public async Task Importa_tudo_com_categoria_fixa_created_by_da_sessao_e_valores_exatos()
    {
        var (board, user, client) = await Setup();

        var response = await client.PostFileAsync(Url(board.Id), ValidCsv.Utf8());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await response.ReadJsonAsync()).GetProperty("importedCount").GetInt32());
        var rows = await Saved(board.Id);
        Assert.Equal(["entrada", "saida", "entrada"], rows.Select(r => r.Type));
        Assert.Equal([180.00m, 45.90m, 1234.56m], rows.Select(r => r.Amount));
        Assert.Equal([new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 3)], rows.Select(r => r.Date));
        Assert.All(rows, r =>
        {
            Assert.Equal("Dados importados", r.Category);
            Assert.Equal(user.Id, r.CreatedBy);
        });
        Assert.Equal("Transferência recebida pelo Pix - FULANO DE TAL - NU PAGAMENTOS S.A.", rows[0].Description);

        // aparece nos meses certos, sem deduplicação
        var (_, summary) = await client.GetJsonAsync($"/api/boards/{board.Id}/months/2026-05");
        Assert.Equal("1368.66", summary.GetProperty("balance").GetString()); // 180 - 45.90 + 1234.56
        await client.PostFileAsync(Url(board.Id), ValidCsv.Utf8());
        Assert.Equal(6, (await Saved(board.Id)).Count);
    }

    [Fact]
    public async Task Linha_invalida_rejeita_o_arquivo_inteiro_e_nao_importa_nada()
    {
        var (board, _, client) = await Setup();
        var csv = ValidCsv + "31/02/2026,10.00,x,Data inexistente\n";

        var response = await client.PostFileAsync(Url(board.Id), csv.Utf8());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Linha 5 inválida.", (await response.ReadJsonAsync()).GetProperty("title").GetString());
        Assert.Empty(await Saved(board.Id));
    }

    [Theory]
    [InlineData("Data,Valor,Descrição,Identificador\n01/05/2026,10,id,Desc\n", "Cabeçalho CSV inválido.")]
    [InlineData("Data,Valor,Identificador,Descrição\n", "Arquivo CSV vazio.")]
    public async Task Cabecalho_errado_ou_arquivo_vazio_responde_400_com_a_mensagem(string csv, string message)
    {
        var (board, _, client) = await Setup();

        var response = await client.PostFileAsync(Url(board.Id), csv.Utf8());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, (await response.ReadJsonAsync()).GetProperty("title").GetString());
        Assert.Empty(await Saved(board.Id));
    }

    [Fact]
    public async Task Arquivo_ausente_vazio_ou_grande_demais_responde_400()
    {
        var (board, _, client) = await Setup();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostFileAsync(Url(board.Id), [])).StatusCode);
        var huge = (Header + string.Concat(Enumerable.Repeat("01/05/2026,1,id,x\n", 400_000))).Utf8(); // ~7 MB
        Assert.True(huge.Length > 5 * 1024 * 1024);
        var response = await client.PostFileAsync(Url(board.Id), huge);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("grande", (await response.ReadJsonAsync()).GetProperty("title").GetString());
        Assert.Empty(await Saved(board.Id));
    }

    [Fact]
    public async Task Corpo_que_nao_e_multipart_nao_e_aceito()
    {
        var (board, _, client) = await Setup();

        var response = await client.SendJsonAsync(HttpMethod.Post, Url(board.Id), new { file = "x" });

        Assert.InRange((int)response.StatusCode, 400, 499);
        Assert.Empty(await Saved(board.Id));
    }

    [Fact]
    public async Task Viewer_403_sem_acesso_404_sem_sessao_401_e_nada_e_importado()
    {
        var (board, _, _) = await Setup();
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var intruso = await TestDb.AddUserAsync(Cs);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await postgres.Factory.SignedInClientAsync(viewer.Id)).PostFileAsync(Url(board.Id), ValidCsv.Utf8())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await (await postgres.Factory.SignedInClientAsync(intruso.Id)).PostFileAsync(Url(board.Id), ValidCsv.Utf8())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await postgres.Factory.CreateNoRedirectClient().PostFileAsync(Url(board.Id), ValidCsv.Utf8())).StatusCode);
        Assert.Empty(await Saved(board.Id));
    }
}
