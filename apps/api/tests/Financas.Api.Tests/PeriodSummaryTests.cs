using System.Net;
using System.Text.Json;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class PeriodSummaryTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private async Task<(Guid BoardId, Guid UserId, HttpClient Client)> Setup(string role = "tesoureiro")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board.Id, user.Id, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    private static async Task<JsonElement> Period(HttpClient client, Guid boardId, string start, string end)
    {
        var (status, body) = await client.GetJsonAsync($"/api/boards/{boardId}/periods?start={start}&end={end}");
        Assert.Equal(HttpStatusCode.OK, status);
        return body;
    }

    private static string Money(JsonElement summary, string property) => summary.GetProperty(property).GetString()!;

    [Fact]
    public async Task Fechamento_de_outubro_vai_ate_o_pagamento_no_inicio_de_novembro()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 1000m, "2026-09-15"); // saldo anterior
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 100m, "2026-10-01");    // antes do início: saldo anterior
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 50m, "2026-10-02");   // primeiro dia do período
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 200m, "2026-10-20", "Aluguel");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 30m, "2026-11-01", "Luz"); // último dia: entra
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 999m, "2026-11-02");       // depois do fim: fica de fora

        var summary = await Period(client, board, "2026-10-02", "2026-11-01");

        Assert.Equal("2026-10-02", summary.GetProperty("start").GetString());
        Assert.Equal("2026-11-01", summary.GetProperty("end").GetString());
        Assert.Equal("900.00", Money(summary, "previousBalance")); // 1000 - 100
        Assert.Equal("50.00", Money(summary, "entradasTotal"));
        Assert.Equal("230.00", Money(summary, "saidasTotal"));
        Assert.Equal("720.00", Money(summary, "balance")); // 900 + 50 - 230
        Assert.Equal(3, summary.GetProperty("transactionCount").GetInt32());
        Assert.Equal(["Aluguel", "Luz"], summary.GetProperty("saidas").Strings("category"));
    }

    [Fact]
    public async Task Periodos_encadeados_batem_o_saldo_final_com_o_saldo_anterior_do_seguinte()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 500m, "2026-10-10");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 120m, "2026-11-01");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 80m, "2026-11-20");

        var outubro = await Period(client, board, "2026-10-01", "2026-11-01");
        var novembro = await Period(client, board, "2026-11-02", "2026-12-02");

        Assert.Equal("380.00", Money(outubro, "balance"));
        Assert.Equal(Money(outubro, "balance"), Money(novembro, "previousBalance"));
        Assert.Equal("300.00", Money(novembro, "balance"));
    }

    [Fact]
    public async Task Periodo_de_um_dia_e_o_mes_inteiro_igual_ao_resumo_mensal()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 10m, "2026-04-30");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 0.10m, "2026-05-01");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 0.05m, "2026-05-31");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 7m, "2026-06-01");
        await TestData.AddReserveAsync(Cs, board, user, "Caixinha", 25m);

        var day = await Period(client, board, "2026-05-31", "2026-05-31");
        Assert.Equal("0.05", Money(day, "saidasTotal"));
        Assert.Equal(1, day.GetProperty("transactionCount").GetInt32());

        var period = await Period(client, board, "2026-05-01", "2026-05-31");
        var (_, month) = await client.GetJsonAsync($"/api/boards/{board}/months/2026-05");
        foreach (var property in new[] { "previousBalance", "entradasTotal", "saidasTotal", "balance", "reservesTotal" })
            Assert.Equal(Money(month, property), Money(period, property));
        Assert.Equal(month.GetProperty("transactionCount").GetInt32(), period.GetProperty("transactionCount").GetInt32());
    }

    [Fact]
    public async Task Outros_boards_nao_entram_e_viewer_pode_ler()
    {
        var (board, user, _) = await Setup();
        var outro = await TestData.AddBoardAsync(Cs);
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board, "viewer");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 10m, "2026-05-05");
        await TestData.AddTransactionAsync(Cs, outro.Id, user, "entrada", 9999m, "2026-05-05");

        var summary = await Period(await postgres.Factory.SignedInClientAsync(viewer.Id), board, "2026-05-01", "2026-05-31");

        Assert.Equal("10.00", Money(summary, "balance"));
    }

    [Fact]
    public async Task Sem_acesso_ao_board_responde_404()
    {
        var (_, _, client) = await Setup();
        var outro = await TestData.AddBoardAsync(Cs);

        var response = await client.GetAsync($"/api/boards/{outro.Id}/periods?start=2026-05-01&end=2026-05-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sem_sessao_responde_401()
    {
        var board = await TestData.AddBoardAsync(Cs);

        var response = await postgres.Factory.CreateNoRedirectClient()
            .GetAsync($"/api/boards/{board.Id}/periods?start=2026-05-01&end=2026-05-31");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("2026-05-01", "")]
    [InlineData("", "2026-05-31")]
    [InlineData("2026-5-1", "2026-05-31")]
    [InlineData("01/05/2026", "31/05/2026")]
    [InlineData("2026-05-32", "2026-06-01")]
    [InlineData("2026-02-30", "2026-03-01")]
    [InlineData("2026-05", "2026-05-31")]
    [InlineData("abc", "def")]
    [InlineData("2026-05-31", "2026-05-01")] // fim antes do início
    [InlineData("2025-01-01", "2026-01-02")] // 367 dias
    [InlineData("2026-05-01", "9999-12-31")]
    public async Task Periodo_invalido_responde_400_com_mensagem(string start, string end)
    {
        var (board, _, client) = await Setup();

        var (status, body) = await client.GetJsonAsync($"/api/boards/{board}/periods?start={start}&end={end}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Parametros_ausentes_respondem_400()
    {
        var (board, _, client) = await Setup();

        var response = await client.GetAsync($"/api/boards/{board}/periods");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Periodo_de_366_dias_e_aceito()
    {
        var (board, _, client) = await Setup();

        await Period(client, board, "2024-01-01", "2024-12-31"); // 2024 é bissexto: 366 dias
    }
}
