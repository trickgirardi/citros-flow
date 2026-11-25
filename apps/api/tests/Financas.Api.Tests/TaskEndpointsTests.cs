using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class TaskEndpointsTests(PostgresFixture postgres)
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

    private static object Body(string name = "Tarefa", string status = "aFazer", string? description = null,
        string[]? tags = null, Guid[]? assignees = null) =>
        new { name, description = description ?? "", status, tags = tags ?? [], assigneeIds = assignees ?? [] };

    private static Task<(HttpStatusCode Status, JsonElement Body)> Create(
        HttpClient client, Guid board, object body) => Send(client, HttpMethod.Post, $"/api/boards/{board}/tasks", body);

    private async Task<Guid> NewTask(HttpClient admin, Guid board, string name = "Tarefa", string status = "aFazer")
    {
        var (code, body) = await Create(admin, board, Body(name, status));
        Assert.Equal(HttpStatusCode.OK, code);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> Tasks(HttpClient client, Guid board)
    {
        var (code, body) = await client.GetJsonAsync($"/api/boards/{board}/tasks");
        Assert.Equal(HttpStatusCode.OK, code);
        return body.EnumerateArray().ToList();
    }

    private static string[] Column(List<JsonElement> tasks, string status) =>
        tasks.Where(t => t.GetProperty("status").GetString() == status)
            .OrderBy(t => t.GetProperty("position").GetInt32())
            .Select(t => t.GetProperty("name").GetString()!).ToArray();

    private static Task<(HttpStatusCode Status, JsonElement Body)> Move(
        HttpClient client, Guid board, Guid task, string status, int index) =>
        Send(client, HttpMethod.Patch, $"/api/boards/{board}/tasks/{task}/move", new { status, index });

    // ---- permissões ----

    [Theory]
    [InlineData("admin")]
    [InlineData("junta_administrativa")]
    [InlineData("admin", "tesoureiro")]
    [InlineData("tesoureiro", "junta_administrativa")] // os papéis se somam
    [InlineData("viewer", "junta_administrativa")]
    public async Task Admin_e_junta_no_board_escrevem_em_tarefas(params string[] roles)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var client = await ClientWith(board.Id, roles);

        var (created, body) = await Create(client, board.Id, Body("Nova"));
        Assert.Equal(HttpStatusCode.OK, created);
        var id = body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/api/boards/{board.Id}/tasks/{id}", Body("Editada"))).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await Move(client, board.Id, id, "concluida", 0)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{id}")).Status);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("junta_administrativa")]
    public async Task Papel_global_de_admin_ou_junta_escreve_em_qualquer_board(string role)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var client = await ClientWith(null, role);

        Assert.Equal(HttpStatusCode.OK, (await Create(client, board.Id, Body())).Status);
    }

    [Theory]
    [InlineData("tesoureiro")]
    [InlineData("viewer")]
    [InlineData("tesoureiro", "viewer")]
    public async Task Tesoureiro_e_viewer_leem_mas_nao_escrevem(params string[] roles)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var task = await NewTask(admin, board.Id);
        var client = await ClientWith(board.Id, roles);

        Assert.Single(await Tasks(client, board.Id)); // leitura liberada
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(client, board.Id, Body())).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Put, $"/api/boards/{board.Id}/tasks/{task}", Body("X"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Move(client, board.Id, task, "concluida", 0)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{task}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/boards/{board.Id}/task-assignees")).StatusCode);
        Assert.Single(await Tasks(admin, board.Id)); // nada mudou
    }

    [Fact]
    public async Task Papel_so_vale_no_proprio_board_e_sem_papel_ou_sessao_nao_acessa()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var juntaDoOutro = await ClientWith(outro.Id, "junta_administrativa");
        var semPapel = await ClientWith(null);

        Assert.Equal(HttpStatusCode.NotFound, (await Create(juntaDoOutro, board.Id, Body())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await juntaDoOutro.GetAsync($"/api/boards/{board.Id}/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await semPapel.GetAsync($"/api/boards/{board.Id}/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await postgres.Factory.CreateNoRedirectClient().GetAsync($"/api/boards/{board.Id}/tasks")).StatusCode);
    }

    [Fact]
    public async Task Detalhe_do_board_informa_se_pode_gerenciar_tarefas()
    {
        var board = await TestData.AddBoardAsync(Cs);

        foreach (var (roles, tasks, finance) in new[]
                 {
                     (new[] { "admin" }, true, true),
                     (new[] { "junta_administrativa" }, true, false),
                     (new[] { "tesoureiro" }, false, true),
                     (new[] { "viewer" }, false, false),
                     (new[] { "tesoureiro", "junta_administrativa" }, true, true),
                 })
        {
            var (_, detail) = await (await ClientWith(board.Id, roles)).GetJsonAsync($"/api/boards/{board.Id}");
            Assert.Equal(tasks, detail.GetProperty("canManageTasks").GetBoolean());
            Assert.Equal(finance, detail.GetProperty("canMutate").GetBoolean());
        }
    }

    [Fact]
    public async Task Junta_nao_escreve_no_financeiro_e_tesoureiro_nao_escreve_em_tarefas()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var junta = await ClientWith(board.Id, "junta_administrativa");

        var response = await junta.SendJsonAsync(HttpMethod.Post, $"/api/boards/{board.Id}/transactions",
            new { type = "entrada", amount = "10.00", description = "x", category = "y", date = "2026-05-01" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // ...mas vê o fluxo de caixa em leitura, como um viewer
        Assert.Equal(HttpStatusCode.OK, (await junta.GetAsync($"/api/boards/{board.Id}/months/2026-05")).StatusCode);
    }

    // ---- criar e validar ----

    [Fact]
    public async Task Cria_tarefa_com_todos_os_campos_normalizados()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var pessoa = await TestDb.AddUserAsync(Cs, displayName: "Fulana");
        await TestData.AddRoleAsync(Cs, pessoa.Id, board.Id, "viewer");

        var (status, body) = await Create(admin, board.Id,
            Body("  Comprar tinta  ", "emAndamento", "  pintar a sala ", ["Urgente", " urgente ", "", "Obra"], [pessoa.Id]));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Comprar tinta", body.GetProperty("name").GetString());
        Assert.Equal("pintar a sala", body.GetProperty("description").GetString());
        Assert.Equal("emAndamento", body.GetProperty("status").GetString());
        Assert.Equal(["Urgente", "Obra"], body.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!)); // sem vazias nem repetidas
        var assignee = Assert.Single(body.GetProperty("assignees").EnumerateArray());
        Assert.Equal("Fulana", assignee.GetProperty("name").GetString());
        Assert.Equal(pessoa.Id, assignee.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Descricao_tags_e_responsaveis_sao_opcionais()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");

        var (status, body) = await Send(admin, HttpMethod.Post, $"/api/boards/{board.Id}/tasks", new { name = "Só o essencial", status = "concluida" });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("", body.GetProperty("description").GetString());
        Assert.Equal(0, body.GetProperty("tags").GetArrayLength());
        Assert.Equal(0, body.GetProperty("assignees").GetArrayLength());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nome_e_obrigatorio(string? name)
    {
        var board = await TestData.AddBoardAsync(Cs);

        var (status, body) = await Create(await ClientWith(board.Id, "admin"), board.Id,
            new { name, status = "aFazer" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("Nome", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Status_e_obrigatorio()
    {
        var board = await TestData.AddBoardAsync(Cs);

        var (status, body) = await Create(await ClientWith(board.Id, "admin"), board.Id, new { name = "Sem status" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("Status", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Status_desconhecido_responde_400()
    {
        var board = await TestData.AddBoardAsync(Cs);

        var (status, _) = await Create(await ClientWith(board.Id, "admin"), board.Id, Body(status: "feito"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Limites_de_tamanho()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");

        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(new string('a', 120)))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(new string('a', 121)))).Status);
        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(description: new string('d', 2000)))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(description: new string('d', 2001)))).Status);
        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(tags: [new string('t', 30)]))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(tags: [new string('t', 31)]))).Status);
        Assert.Equal(HttpStatusCode.OK,
            (await Create(admin, board.Id, Body(tags: Enumerable.Range(1, 10).Select(i => $"t{i}").ToArray()))).Status);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Create(admin, board.Id, Body(tags: Enumerable.Range(1, 11).Select(i => $"t{i}").ToArray()))).Status);
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

        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(assignees: [doBoard.Id, global.Id]))).Status);
        foreach (var invalid in new[] { doOutro.Id, semPapel.Id, inativo.Id, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(assignees: [doBoard.Id, invalid]))).Status);
        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(assignees: [doBoard.Id, doBoard.Id]))).Status); // repetido vale um
    }

    [Fact]
    public async Task No_maximo_10_responsaveis()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var people = new List<Guid>();
        for (var i = 0; i < 11; i++) people.Add((await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer")).Id);

        Assert.Equal(HttpStatusCode.OK, (await Create(admin, board.Id, Body(assignees: people.Take(10).ToArray()))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(admin, board.Id, Body(assignees: people.ToArray()))).Status);
    }

    // ---- listar e ordem ----

    [Fact]
    public async Task Tarefa_nova_vai_para_o_fim_da_coluna_e_a_lista_vem_por_coluna_e_posicao()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        await NewTask(admin, board.Id, "C1", "concluida");
        await NewTask(admin, board.Id, "F1", "aFazer");
        await NewTask(admin, board.Id, "A1", "emAndamento");
        await NewTask(admin, board.Id, "F2", "aFazer");

        var tasks = await Tasks(admin, board.Id);

        Assert.Equal(["F1", "F2", "A1", "C1"], tasks.Select(t => t.GetProperty("name").GetString()!));
        Assert.Equal(["F1", "F2"], Column(tasks, "aFazer"));
        Assert.Equal([0, 1], tasks.Take(2).Select(t => t.GetProperty("position").GetInt32()));
    }

    [Fact]
    public async Task Tarefas_de_outro_board_nao_aparecem()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        await NewTask(admin, board.Id, "Daqui");
        await NewTask(admin, outro.Id, "De lá");

        Assert.Equal(["Daqui"], (await Tasks(admin, board.Id)).Select(t => t.GetProperty("name").GetString()!));
    }

    [Fact]
    public async Task Responsaveis_vem_em_ordem_pt_br()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var ids = new List<Guid>();
        foreach (var name in new[] { "Zélia", "Ângela", "Bruno" })
        {
            var user = await TestDb.AddUserAsync(Cs, displayName: name);
            await TestData.AddRoleAsync(Cs, user.Id, board.Id, "viewer");
            ids.Add(user.Id);
        }
        await Create(admin, board.Id, Body(assignees: ids.ToArray()));

        var task = Assert.Single(await Tasks(admin, board.Id));

        Assert.Equal(["Ângela", "Bruno", "Zélia"], task.GetProperty("assignees").Strings("name"));
    }

    [Fact]
    public async Task Pessoa_sem_nome_aparece_pelo_email()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var user = await TestDb.AddUserAsync(Cs, email: $"{Guid.NewGuid():N}@teste.org");
        await TestData.AddRoleAsync(Cs, user.Id, board.Id, "viewer");
        await Create(admin, board.Id, Body(assignees: [user.Id]));

        var task = Assert.Single(await Tasks(admin, board.Id));

        Assert.Equal(user.Email, task.GetProperty("assignees")[0].GetProperty("name").GetString());
    }

    // ---- candidatos a responsável ----

    [Fact]
    public async Task Lista_de_responsaveis_traz_ativos_com_papel_no_board_ou_global()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var junta = await ClientWith(board.Id, "junta_administrativa");
        var doBoard = await TestData.AddUserWithRoleAsync(Cs, board.Id, "tesoureiro");
        var global = await TestData.AddUserWithRoleAsync(Cs, null, "viewer");
        var doOutro = await TestData.AddUserWithRoleAsync(Cs, outro.Id, "admin");
        var inativo = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        await using (var db = TestDb.Open(Cs))
            await db.Users.Where(u => u.Id == inativo.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        var (status, body) = await junta.GetJsonAsync($"/api/boards/{board.Id}/task-assignees");
        var ids = body.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(doBoard.Id, ids);
        Assert.Contains(global.Id, ids);
        Assert.DoesNotContain(doOutro.Id, ids);
        Assert.DoesNotContain(inativo.Id, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count()); // quem tem dois papéis aparece uma vez
    }

    [Fact]
    public async Task Pessoa_com_dois_papeis_aparece_uma_vez_na_lista_de_responsaveis()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var dupla = await TestData.AddUserWithRoleAsync(Cs, board.Id, "tesoureiro");
        await TestData.AddRoleAsync(Cs, dupla.Id, board.Id, "junta_administrativa");
        await TestData.AddRoleAsync(Cs, dupla.Id, null, "viewer");

        var (_, body) = await admin.GetJsonAsync($"/api/boards/{board.Id}/task-assignees");

        Assert.Single(body.EnumerateArray(), p => p.GetProperty("id").GetGuid() == dupla.Id);
    }

    // ---- editar ----

    [Fact]
    public async Task Edita_campos_e_troca_responsaveis_e_tags()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var a = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var b = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var (_, created) = await Create(admin, board.Id, Body("Antes", tags: ["velha"], assignees: [a.Id]));
        var id = created.GetProperty("id").GetGuid();

        var (status, body) = await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/tasks/{id}",
            Body("Depois", "aFazer", "nova descrição", ["nova"], [b.Id]));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Depois", body.GetProperty("name").GetString());
        Assert.Equal("nova descrição", body.GetProperty("description").GetString());
        Assert.Equal(["nova"], body.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!));
        Assert.Equal([b.Id], body.GetProperty("assignees").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()));
        await using var db = TestDb.Open(Cs);
        var saved = await db.Tasks.AsNoTracking().SingleAsync(t => t.Id == id);
        Assert.NotNull(saved.UpdatedAt);
        Assert.NotNull(saved.UpdatedBy);
    }

    [Fact]
    public async Task Mudar_o_status_pelo_editar_manda_a_tarefa_para_o_fim_da_nova_coluna()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var a = await NewTask(admin, board.Id, "A", "aFazer");
        await NewTask(admin, board.Id, "B", "concluida");
        await NewTask(admin, board.Id, "C", "concluida");

        await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/tasks/{a}", Body("A", "concluida"));

        Assert.Equal(["B", "C", "A"], Column(await Tasks(admin, board.Id), "concluida"));
    }

    [Fact]
    public async Task Editar_valida_e_tarefa_de_outro_board_ou_inexistente_e_404()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        var id = await NewTask(admin, board.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/tasks/{id}", Body(""))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Put, $"/api/boards/{outro.Id}/tasks/{id}", Body("X"))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Put, $"/api/boards/{board.Id}/tasks/{Guid.NewGuid()}", Body("X"))).Status);
    }

    // ---- mover ----

    [Fact]
    public async Task Reordena_dentro_da_coluna()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var a = await NewTask(admin, board.Id, "A");
        await NewTask(admin, board.Id, "B");
        var c = await NewTask(admin, board.Id, "C");

        Assert.Equal(HttpStatusCode.NoContent, (await Move(admin, board.Id, c, "aFazer", 0)).Status);
        Assert.Equal(["C", "A", "B"], Column(await Tasks(admin, board.Id), "aFazer"));

        await Move(admin, board.Id, a, "aFazer", 2);
        Assert.Equal(["C", "B", "A"], Column(await Tasks(admin, board.Id), "aFazer"));
    }

    [Fact]
    public async Task Move_entre_colunas_insere_na_posicao_e_fecha_o_buraco_da_origem()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        await NewTask(admin, board.Id, "F1");
        var f2 = await NewTask(admin, board.Id, "F2");
        await NewTask(admin, board.Id, "F3");
        await NewTask(admin, board.Id, "D1", "concluida");
        await NewTask(admin, board.Id, "D2", "concluida");

        await Move(admin, board.Id, f2, "concluida", 1);

        var tasks = await Tasks(admin, board.Id);
        Assert.Equal(["D1", "F2", "D2"], Column(tasks, "concluida"));
        Assert.Equal(["F1", "F3"], Column(tasks, "aFazer"));
        // posições contíguas nas duas colunas
        Assert.Equal([0, 1], tasks.Where(t => t.GetProperty("status").GetString() == "aFazer").Select(t => t.GetProperty("position").GetInt32()).Order());
        Assert.Equal([0, 1, 2], tasks.Where(t => t.GetProperty("status").GetString() == "concluida").Select(t => t.GetProperty("position").GetInt32()).Order());
    }

    [Fact]
    public async Task Indice_alem_do_fim_vai_para_o_fim_e_coluna_vazia_aceita()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var a = await NewTask(admin, board.Id, "A");
        await NewTask(admin, board.Id, "B");

        Assert.Equal(HttpStatusCode.NoContent, (await Move(admin, board.Id, a, "aFazer", 99)).Status);
        Assert.Equal(["B", "A"], Column(await Tasks(admin, board.Id), "aFazer"));

        await Move(admin, board.Id, a, "emAndamento", 5); // coluna vazia
        Assert.Equal(["A"], Column(await Tasks(admin, board.Id), "emAndamento"));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("concluida", null)]
    [InlineData("concluida", -1)]
    [InlineData("feito", 0)]
    public async Task Mover_com_corpo_invalido_responde_400(string? status, int? index)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var id = await NewTask(admin, board.Id);

        var (code, _) = await Send(admin, HttpMethod.Patch, $"/api/boards/{board.Id}/tasks/{id}/move", new { status, index });

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Mover_tarefa_de_outro_board_ou_inexistente_e_404()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        var id = await NewTask(admin, board.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await Move(admin, outro.Id, id, "concluida", 0)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Move(admin, board.Id, Guid.NewGuid(), "concluida", 0)).Status);
    }

    // ---- remover ----

    [Fact]
    public async Task Remove_a_tarefa_e_os_responsaveis_sem_afetar_as_outras()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(board.Id, "admin");
        var pessoa = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var (_, created) = await Create(admin, board.Id, Body("Some", assignees: [pessoa.Id]));
        var id = created.GetProperty("id").GetGuid();
        await NewTask(admin, board.Id, "Fica");

        Assert.Equal(HttpStatusCode.NoContent, (await Send(admin, HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{id}")).Status);

        Assert.Equal(["Fica"], (await Tasks(admin, board.Id)).Select(t => t.GetProperty("name").GetString()!));
        await using var db = TestDb.Open(Cs);
        Assert.False(await db.TaskAssignees.AnyAsync(a => a.TaskId == id));
        Assert.True(await db.Users.AnyAsync(u => u.Id == pessoa.Id)); // a pessoa continua
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{id}")).Status);
    }

    [Fact]
    public async Task Remover_tarefa_de_outro_board_e_404_e_nao_apaga()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var outro = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        var id = await NewTask(admin, board.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Delete, $"/api/boards/{outro.Id}/tasks/{id}")).Status);
        Assert.Single(await Tasks(admin, board.Id));
    }

    [Fact]
    public async Task Remover_o_board_apaga_as_tarefas()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var admin = await ClientWith(null, "admin");
        await NewTask(admin, board.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await Send(admin, HttpMethod.Delete, $"/api/boards/{board.Id}")).Status);

        await using var db = TestDb.Open(Cs);
        Assert.False(await db.Tasks.AnyAsync(t => t.BoardId == board.Id));
    }
}
