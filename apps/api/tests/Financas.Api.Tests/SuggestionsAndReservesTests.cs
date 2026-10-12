using System.Net;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class SuggestionsAndReservesTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    [Fact]
    public async Task Sugestoes_sao_unicas_sem_espacos_nas_pontas_sem_vazias_em_ordem_pt_br()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "entrada", 1m, "2026-05-01", " Doações ", "Mensalidade");
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "entrada", 1m, "2026-04-01", "Doações", "mensalidade ");
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "saida", 1m, "2026-03-01", "Água", "Conta de água");
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "saida", 1m, "2026-02-01", "   ", "  ");
        await TestData.AddTransactionAsync(Cs, outro.Id, user.Id, "saida", 1m, "2026-02-01", "Só no outro board", "Idem");

        var (status, body) = await (await postgres.Factory.SignedInClientAsync(user.Id))
            .GetJsonAsync($"/api/boards/{board.Id}/suggestions");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["Água", "Doações"], body.GetProperty("categories").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["Conta de água", "mensalidade", "Mensalidade"],
            body.GetProperty("descriptions").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Reservas_listadas_em_ordem_de_criacao_com_valor_como_string()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, "tesoureiro");
        await TestData.AddReserveAsync(Cs, board.Id, user.Id, "Poupança", 50.5m);
        await TestData.AddReserveAsync(Cs, board.Id, user.Id, "Caixinha", 100m);

        var (status, body) = await (await postgres.Factory.SignedInClientAsync(user.Id))
            .GetJsonAsync($"/api/boards/{board.Id}/reserves");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["Poupança", "Caixinha"], body.Strings("name"));
        Assert.Equal(["50.50", "100.00"], body.Strings("amount"));
    }

    [Fact]
    public async Task Viewer_nao_lista_reservas_so_ve_o_total_no_resumo()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        await TestData.AddReserveAsync(Cs, board.Id, viewer.Id, "Caixinha", 100m);
        var client = await postgres.Factory.SignedInClientAsync(viewer.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/boards/{board.Id}/reserves")).StatusCode);
        var (_, summary) = await client.GetJsonAsync($"/api/boards/{board.Id}/months/2026-05");
        Assert.Equal("100.00", summary.GetProperty("reservesTotal").GetString());
    }

    [Fact]
    public async Task Papel_global_de_escrita_lista_reservas_de_qualquer_board()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await TestData.AddUserWithRoleAsync(Cs, null, "admin");
        await TestData.AddReserveAsync(Cs, board.Id, admin.Id, "Caixinha", 10m);

        var (status, body) = await (await postgres.Factory.SignedInClientAsync(admin.Id))
            .GetJsonAsync($"/api/boards/{board.Id}/reserves");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["Caixinha"], body.Strings("name"));
    }
}
