using System.Net;
using System.Text.Json;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class DashboardTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    private static string Money(JsonElement e, string property) => e.GetProperty(property).GetString()!;

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Dash(HttpClient client, Guid board, string month = "2026-11") =>
        await client.GetJsonAsync($"/api/boards/{board}/dashboard?month={month}");

    private async Task<(Guid Board, Guid UserId, HttpClient Client)> Setup(string role = "admin")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board.Id, user.Id, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    // ---- acesso ----

    [Theory]
    [InlineData("admin")]
    [InlineData("tesoureiro")]
    [InlineData("junta_administrativa")]
    [InlineData("viewer")]
    public async Task Qualquer_papel_no_board_le_o_dashboard(string role)
    {
        var (board, _, client) = await Setup(role);

        var (status, body) = await Dash(client, board);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("2026-11", body.GetProperty("month").GetString());
    }

    [Fact]
    public async Task Papel_global_le_e_sem_papel_ou_de_outro_board_e_404_e_sem_sessao_401()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var global = await postgres.Factory.SignedInClientAsync((await TestData.AddUserWithRoleAsync(Cs, null, "viewer")).Id);
        var doOutro = await postgres.Factory.SignedInClientAsync((await TestData.AddUserWithRoleAsync(Cs, outro.Id, "admin")).Id);
        var semPapel = await postgres.Factory.SignedInClientAsync((await TestDb.AddUserAsync(Cs)).Id);

        Assert.Equal(HttpStatusCode.OK, (await Dash(global, board.Id)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Dash(doOutro, board.Id)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Dash(semPapel, board.Id)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Dash(postgres.Factory.CreateNoRedirectClient(), board.Id)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("abc")]
    [InlineData("9999-12")]
    public async Task Mes_invalido_responde_400(string month)
    {
        var (board, _, client) = await Setup();

        Assert.Equal(HttpStatusCode.BadRequest, (await Dash(client, board, month)).Status);
    }

    [Fact]
    public async Task Sem_o_parametro_mes_responde_400()
    {
        var (board, _, client) = await Setup();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/boards/{board}/dashboard")).StatusCode);
    }

    // ---- caixa ----

    [Fact]
    public async Task Caixa_tem_os_mesmos_numeros_do_resumo_mensal()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 1000.10m, "2026-09-15");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 200.05m, "2026-10-31");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 500.20m, "2026-11-01");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 0.10m, "2026-11-30");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 120.33m, "2026-11-10");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 9999m, "2026-12-01"); // depois do mês
        await TestData.AddReserveAsync(Cs, board, user, "Caixinha", 300.50m);

        var (_, dashboard) = await Dash(client, board);
        var (_, month) = await client.GetJsonAsync($"/api/boards/{board}/months/2026-11");
        var cash = dashboard.GetProperty("cash");

        foreach (var property in new[] { "previousBalance", "entradasTotal", "saidasTotal", "balance", "reservesTotal" })
            Assert.Equal(Money(month, property), Money(cash, property));
        Assert.Equal(month.GetProperty("transactionCount").GetInt32(), cash.GetProperty("transactionCount").GetInt32());
        Assert.Equal("800.05", Money(cash, "previousBalance")); // 1000.10 - 200.05
        Assert.Equal("500.30", Money(cash, "entradasTotal"));
        Assert.Equal("120.33", Money(cash, "saidasTotal"));
        Assert.Equal("1180.02", Money(cash, "balance"));
        Assert.Equal("300.50", Money(cash, "reservesTotal"));
        Assert.Equal(3, cash.GetProperty("transactionCount").GetInt32());
    }

    [Fact]
    public async Task Historico_traz_6_meses_em_ordem_com_zeros_e_so_do_board()
    {
        var (board, user, client) = await Setup();
        var outro = await TestData.AddBoardAsync(Cs);
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 100m, "2026-06-30"); // fora (7 meses antes do fim)
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 10m, "2026-07-01");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 4m, "2026-07-31");
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 20m, "2026-09-10");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 5m, "2026-09-11");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 1.5m, "2026-12-31");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 999m, "2027-01-01"); // depois do mês
        await TestData.AddTransactionAsync(Cs, outro.Id, user, "entrada", 777m, "2026-09-10"); // outro board

        var (_, body) = await Dash(client, board, "2026-12");
        var history = body.GetProperty("history").EnumerateArray().ToList();

        Assert.Equal(["2026-07", "2026-08", "2026-09", "2026-10", "2026-11", "2026-12"],
            history.Select(h => h.GetProperty("month").GetString()!));
        Assert.Equal(["10.00", "0.00", "20.00", "0.00", "0.00", "0.00"], history.Select(h => Money(h, "entradas")));
        Assert.Equal(["4.00", "0.00", "5.00", "0.00", "0.00", "1.50"], history.Select(h => Money(h, "saidas")));
    }

    [Fact]
    public async Task Historico_atravessa_a_virada_do_ano()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board, user, "entrada", 3m, "2025-12-15");
        await TestData.AddTransactionAsync(Cs, board, user, "saida", 2m, "2026-02-01");

        var (_, body) = await Dash(client, board, "2026-02");
        var history = body.GetProperty("history").EnumerateArray().ToList();

        Assert.Equal(["2025-09", "2025-10", "2025-11", "2025-12", "2026-01", "2026-02"],
            history.Select(h => h.GetProperty("month").GetString()!));
        Assert.Equal("3.00", Money(history[3], "entradas"));
        Assert.Equal("2.00", Money(history[5], "saidas"));
    }

    [Fact]
    public async Task Board_novo_mostra_zeros_e_listas_vazias()
    {
        var (board, _, client) = await Setup();

        var (status, body) = await Dash(client, board);

        Assert.Equal(HttpStatusCode.OK, status);
        var cash = body.GetProperty("cash");
        foreach (var property in new[] { "previousBalance", "entradasTotal", "saidasTotal", "balance", "reservesTotal" })
            Assert.Equal("0.00", Money(cash, property));
        Assert.Equal(6, body.GetProperty("history").GetArrayLength());
        var tasks = body.GetProperty("tasks");
        Assert.Equal(0, tasks.GetProperty("aFazer").GetInt32() + tasks.GetProperty("emAndamento").GetInt32() + tasks.GetProperty("concluida").GetInt32());
        Assert.Equal(0, tasks.GetProperty("mine").GetArrayLength());
        Assert.Equal(0, tasks.GetProperty("mineTotal").GetInt32());
        var stock = body.GetProperty("stock");
        Assert.Equal(0, stock.GetProperty("attention").GetArrayLength());
        Assert.Equal(0, stock.GetProperty("attentionTotal").GetInt32());
    }

    // ---- tarefas ----

    private async Task AddTask(HttpClient admin, Guid board, string name, string status, params Guid[] assignees) =>
        await AddTask(admin, board, name, status, null, assignees);

    private async Task AddTask(HttpClient admin, Guid board, string name, string status, string? priority, params Guid[] assignees)
    {
        var response = await admin.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board}/tasks",
            new { name, status, priority, tags = Array.Empty<string>(), assigneeIds = assignees });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tarefas_contam_por_coluna_e_minhas_so_as_minhas_em_aberto()
    {
        var (board, me, client) = await Setup();
        var other = await TestData.AddUserWithRoleAsync(Cs, board, "viewer");
        await AddTask(client, board, "A1", "aFazer", me);
        await AddTask(client, board, "A2", "aFazer");
        await AddTask(client, board, "E1", "emAndamento", me, other.Id);
        await AddTask(client, board, "E2", "emAndamento", other.Id);
        await AddTask(client, board, "C1", "concluida", me);
        await AddTask(client, board, "C2", "concluida");

        var (_, body) = await Dash(client, board);
        var tasks = body.GetProperty("tasks");

        Assert.Equal(2, tasks.GetProperty("aFazer").GetInt32());
        Assert.Equal(2, tasks.GetProperty("emAndamento").GetInt32());
        Assert.Equal(2, tasks.GetProperty("concluida").GetInt32());
        // minhas em aberto: em andamento primeiro; a concluída e as dos outros ficam de fora
        Assert.Equal(["E1", "A1"], tasks.GetProperty("mine").EnumerateArray().Select(t => t.GetProperty("name").GetString()!));
        Assert.Equal("emAndamento", tasks.GetProperty("mine")[0].GetProperty("status").GetString());
        Assert.Equal(2, tasks.GetProperty("mineTotal").GetInt32());
    }

    [Fact]
    public async Task Minhas_tarefas_vem_com_a_prioridade_e_as_mais_prioritarias_primeiro()
    {
        var (board, me, client) = await Setup();
        await AddTask(client, board, "Sem", "emAndamento", null, me);
        await AddTask(client, board, "Baixa", "emAndamento", "baixa", me);
        await AddTask(client, board, "Alta a fazer", "aFazer", "alta", me);
        await AddTask(client, board, "Urgente", "aFazer", "urgente", me);
        await AddTask(client, board, "Alta em andamento", "emAndamento", "alta", me);
        await AddTask(client, board, "Média", "aFazer", "media", me);

        var (_, body) = await Dash(client, board);
        var mine = body.GetProperty("tasks").GetProperty("mine").EnumerateArray().ToList();

        // prioridade primeiro; empate: em andamento antes de a fazer
        Assert.Equal(["Urgente", "Alta em andamento", "Alta a fazer", "Média", "Baixa"],
            mine.Select(t => t.GetProperty("name").GetString()!));
        Assert.Equal(["urgente", "alta", "alta", "media", "baixa"], mine.Select(t => t.GetProperty("priority").GetString()!));
        Assert.Equal(6, body.GetProperty("tasks").GetProperty("mineTotal").GetInt32()); // a "Sem" ficou fora só do limite de 5
    }

    [Fact]
    public async Task Minhas_tarefas_sem_prioridade_vem_com_prioridade_nula_e_por_ultimo()
    {
        var (board, me, client) = await Setup();
        await AddTask(client, board, "Sem", "emAndamento", null, me);
        await AddTask(client, board, "Baixa", "aFazer", "baixa", me);

        var (_, body) = await Dash(client, board);
        var mine = body.GetProperty("tasks").GetProperty("mine").EnumerateArray().ToList();

        Assert.Equal(["Baixa", "Sem"], mine.Select(t => t.GetProperty("name").GetString()!));
        Assert.Equal(JsonValueKind.Null, mine[1].GetProperty("priority").ValueKind);
    }

    [Fact]
    public async Task Minhas_tarefas_vem_limitada_a_5_com_o_total_certo()
    {
        var (board, me, client) = await Setup();
        for (var i = 1; i <= 7; i++) await AddTask(client, board, $"T{i}", "aFazer", me);

        var (_, body) = await Dash(client, board);
        var tasks = body.GetProperty("tasks");

        Assert.Equal(5, tasks.GetProperty("mine").GetArrayLength());
        Assert.Equal(7, tasks.GetProperty("mineTotal").GetInt32());
    }

    [Fact]
    public async Task Minhas_tarefas_e_de_quem_esta_logado_e_tarefas_de_outro_board_nao_contam()
    {
        var (board, me, mine) = await Setup();
        var other = await TestData.AddUserWithRoleAsync(Cs, board, "admin");
        var otherClient = await postgres.Factory.SignedInClientAsync(other.Id);
        var outro = await TestData.AddBoardAsync(Cs);
        await TestData.AddRoleAsync(Cs, me, outro.Id, "admin");
        await AddTask(mine, board, "Minha", "aFazer", me);
        await AddTask(mine, board, "Dele", "aFazer", other.Id);
        await AddTask(mine, outro.Id, "Em outro board", "aFazer", me);

        var (_, forMe) = await Dash(mine, board);
        var (_, forOther) = await Dash(otherClient, board);

        Assert.Equal(["Minha"], forMe.GetProperty("tasks").GetProperty("mine").EnumerateArray().Select(t => t.GetProperty("name").GetString()!));
        Assert.Equal(["Dele"], forOther.GetProperty("tasks").GetProperty("mine").EnumerateArray().Select(t => t.GetProperty("name").GetString()!));
        Assert.Equal(2, forMe.GetProperty("tasks").GetProperty("aFazer").GetInt32()); // contagem é do board
    }

    // ---- estoque ----

    private async Task AddStock(HttpClient admin, Guid board, string name, string status, Guid? assignee = null)
    {
        var response = await admin.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board}/stock-items",
            new { name, status, assigneeId = assignee });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Estoque_conta_por_status_e_lista_urgentes_primeiro_depois_por_nome_pt_br()
    {
        var (board, me, client) = await Setup();
        await AddStock(client, board, "Zinco", "comprar");
        await AddStock(client, board, "Água", "comprar", me);
        await AddStock(client, board, "Lâmpada", "comprarComUrgencia");
        await AddStock(client, board, "Bandeira", "comprarComUrgencia");
        await AddStock(client, board, "Cadeira", "aCaminho");
        await AddStock(client, board, "Projetor", "emEstoque");
        await AddStock(client, board, "Toalha", "emEstoque");

        var (_, body) = await Dash(client, board);
        var stock = body.GetProperty("stock");

        Assert.Equal(2, stock.GetProperty("emEstoque").GetInt32());
        Assert.Equal(1, stock.GetProperty("aCaminho").GetInt32());
        Assert.Equal(2, stock.GetProperty("comprar").GetInt32());
        Assert.Equal(2, stock.GetProperty("comprarComUrgencia").GetInt32());
        var attention = stock.GetProperty("attention").EnumerateArray().ToList();
        Assert.Equal(["Bandeira", "Lâmpada", "Água", "Zinco"], attention.Select(i => i.GetProperty("name").GetString()!));
        Assert.Equal(["comprarComUrgencia", "comprarComUrgencia", "comprar", "comprar"], attention.Select(i => i.GetProperty("status").GetString()!));
        Assert.Equal(4, stock.GetProperty("attentionTotal").GetInt32());
        Assert.Equal(JsonValueKind.Null, attention[0].GetProperty("assignee").ValueKind);
        Assert.Equal(me, attention[2].GetProperty("assignee").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Precisa_de_acao_vem_limitada_a_5_com_o_total_e_so_do_board()
    {
        var (board, _, client) = await Setup();
        var outro = await TestData.AddBoardAsync(Cs);
        var global = await postgres.Factory.SignedInClientAsync((await TestData.AddUserWithRoleAsync(Cs, null, "admin")).Id);
        for (var i = 1; i <= 7; i++) await AddStock(client, board, $"Item {i}", "comprar");
        await AddStock(global, outro.Id, "De outro board", "comprarComUrgencia");

        var (_, body) = await Dash(client, board);
        var stock = body.GetProperty("stock");

        Assert.Equal(5, stock.GetProperty("attention").GetArrayLength());
        Assert.Equal(7, stock.GetProperty("attentionTotal").GetInt32());
        Assert.Equal(0, stock.GetProperty("comprarComUrgencia").GetInt32());
    }
}
