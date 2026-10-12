using System.Net;
using System.Text.Json;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class MonthSummaryTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private async Task<(Guid BoardId, Guid UserId, HttpClient Client)> Setup(string role = "tesoureiro")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board.Id, user.Id, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    private static async Task<JsonElement> Month(HttpClient client, Guid boardId, string month)
    {
        var (status, body) = await client.GetJsonAsync($"/api/boards/{boardId}/months/{month}");
        Assert.Equal(HttpStatusCode.OK, status);
        return body;
    }

    private static string Money(JsonElement summary, string property) => summary.GetProperty(property).GetString()!;

    [Fact]
    public async Task Soma_exata_em_centavos_e_dinheiro_como_string_no_json()
    {
        var (board, user, client) = await Setup();
        // Em float, 0.1 + 0.2 daria 0.30000000000000004.
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 0.10m, "2026-05-02", "Doações");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 0.20m, "2026-05-03", "Doações");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 0.05m, "2026-05-04", "Água");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 100.10m, "2026-04-10"); // mês anterior
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 0.07m, "2026-04-11");

        var summary = await Month(client, board, "2026-05");

        Assert.Equal("2026-05", summary.GetProperty("month").GetString());
        Assert.Equal("100.03", Money(summary, "previousBalance"));
        Assert.Equal("0.30", Money(summary, "entradasTotal"));
        Assert.Equal("0.05", Money(summary, "saidasTotal"));
        Assert.Equal("100.28", Money(summary, "balance")); // 100.03 + 0.30 - 0.05
        Assert.Equal(3, summary.GetProperty("transactionCount").GetInt32());
    }

    [Fact]
    public async Task Saldo_anterior_nao_e_truncado_com_mais_de_mil_transacoes()
    {
        var (board, user, client) = await Setup();
        await TestData.AddManyTransactionsAsync(Cs, board, user, "entrada", 1.00m, "2020-01-01", 1200);
        await TestData.AddManyTransactionsAsync(Cs, board, user, "saida", 0.50m, "2020-02-01", 300);

        var summary = await Month(client, board, "2026-05");

        Assert.Equal("1050.00", Money(summary, "previousBalance")); // 1200 - 150, no PostgREST ficaria em ~1000 linhas
        Assert.Equal("1050.00", Money(summary, "balance"));
    }

    [Fact]
    public async Task Mes_vazio_zera_tudo_e_traz_listas_vazias()
    {
        var (board, _, client) = await Setup();

        var summary = await Month(client, board, "2026-05");

        foreach (var property in new[] { "previousBalance", "entradasTotal", "saidasTotal", "balance", "reservesTotal" })
            Assert.Equal("0.00", Money(summary, property));
        Assert.Equal(0, summary.GetProperty("transactionCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("entradas").GetArrayLength());
        Assert.Equal(0, summary.GetProperty("saidas").GetArrayLength());
    }

    [Fact]
    public async Task Limites_do_mes_e_virada_de_ano_usam_intervalo_fechado_aberto()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 10m, "2025-12-31");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 1m, "2026-01-01");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 0.50m, "2026-01-31");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 100m, "2026-02-01");

        var dezembro = await Month(client, board, "2025-12");
        var janeiro = await Month(client, board, "2026-01");
        var fevereiro = await Month(client, board, "2026-02");

        // dezembro: 31/12 entra; 01/01 fica de fora
        Assert.Equal("0.00", Money(dezembro, "previousBalance"));
        Assert.Equal("10.00", Money(dezembro, "entradasTotal"));
        Assert.Equal(1, dezembro.GetProperty("transactionCount").GetInt32());
        // janeiro: 01/01 e 31/01 entram; 31/12 vira saldo anterior; 01/02 fica de fora
        Assert.Equal("10.00", Money(janeiro, "previousBalance"));
        Assert.Equal("1.00", Money(janeiro, "entradasTotal"));
        Assert.Equal("0.50", Money(janeiro, "saidasTotal"));
        Assert.Equal("10.50", Money(janeiro, "balance"));
        Assert.Equal(2, janeiro.GetProperty("transactionCount").GetInt32());
        // fevereiro: tudo de janeiro e dezembro acumula
        Assert.Equal("10.50", Money(fevereiro, "previousBalance"));
        Assert.Equal("110.50", Money(fevereiro, "balance"));
    }

    [Fact]
    public async Task Grupos_por_categoria_exata_em_ordem_pt_br_com_totais_e_transacoes_por_data_desc()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 5m, "2026-05-10", "Zebra");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 7.50m, "2026-05-02", "Água", "conta velha");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 2.25m, "2026-05-20", "Água", "conta nova");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 1m, "2026-05-20", "Água", "criada depois"); // mesma data
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 3m, "2026-05-11", "Aluguel");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 4m, "2026-05-12", "aluguel"); // outra categoria: maiúscula conta

        var saidas = (await Month(client, board, "2026-05")).GetProperty("saidas");

        // "Água" vem antes de "Aluguel" (Á não vai para o fim, como na ordem por código)
        Assert.Equal(["Água", "aluguel", "Aluguel", "Zebra"], saidas.Strings("category"));
        var agua = saidas[0];
        Assert.Equal("10.75", agua.GetProperty("total").GetString());
        Assert.Equal(["criada depois", "conta nova", "conta velha"], agua.GetProperty("transactions").Strings("description"));
        var primeira = agua.GetProperty("transactions")[0];
        Assert.Equal("1.00", primeira.GetProperty("amount").GetString());
        Assert.Equal("saida", primeira.GetProperty("type").GetString());
        Assert.Equal("2026-05-20", primeira.GetProperty("date").GetString());
    }

    [Fact]
    public async Task Entradas_e_saidas_ficam_separadas_e_outros_boards_nao_entram()
    {
        var (board, user, client) = await Setup();
        var outro = await TestData.AddBoardAsync(Cs);
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 50m, "2026-05-05", "Doações");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 20m, "2026-05-06", "Luz");
        await TestData.AddTransactionAsync(Cs, outro.Id, user, "entrada", 9999m, "2026-05-05", "Doações");
        await TestData.AddTransactionAsync(Cs, outro.Id, user, "entrada", 9999m, "2026-04-05");

        var summary = await Month(client, board, "2026-05");

        Assert.Equal(["Doações"], summary.GetProperty("entradas").Strings("category"));
        Assert.Equal(["Luz"], summary.GetProperty("saidas").Strings("category"));
        Assert.Equal("entrada", summary.GetProperty("entradas")[0].GetProperty("transactions")[0].GetProperty("type").GetString());
        Assert.Equal("0.00", Money(summary, "previousBalance"));
        Assert.Equal("30.00", Money(summary, "balance"));
    }

    [Fact]
    public async Task Total_de_reservas_aparece_mas_nao_entra_no_saldo()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 10m, "2026-05-05");
        await TestData.AddReserveAsync(Cs, board, user, "Caixinha", 100m);
        await TestData.AddReserveAsync(Cs, board, user, "Poupança", 50.50m);

        foreach (var month in new[] { "2026-05", "2030-01" }) // reserva não tem filtro por mês
        {
            var summary = await Month(client, board, month);
            Assert.Equal("150.50", Money(summary, "reservesTotal"));
        }
        Assert.Equal("10.00", Money(await Month(client, board, "2026-05"), "balance"));
    }

    [Fact]
    public async Task Viewer_e_papel_global_veem_o_resumo()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var global = await TestData.AddUserWithRoleAsync(Cs, null, "viewer");
        await TestData.AddTransactionAsync(Cs, board.Id, viewer.Id, "entrada", 10m, "2026-05-05");

        foreach (var user in new[] { viewer, global })
        {
            var summary = await Month(await postgres.Factory.SignedInClientAsync(user.Id), board.Id, "2026-05");
            Assert.Equal("10.00", Money(summary, "balance"));
        }
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("2026-1")]
    [InlineData("26-05")]
    [InlineData("0000-01")]
    [InlineData("9999-12")]
    [InlineData("abc")]
    [InlineData("2026-05-01")]
    public async Task Mes_invalido_responde_400(string month)
    {
        var (board, _, client) = await Setup();

        var response = await client.GetAsync($"/api/boards/{board}/months/{month}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
