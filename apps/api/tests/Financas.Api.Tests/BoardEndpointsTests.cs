using System.Net;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class BoardEndpointsTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;
    private ApiFactory Api => postgres.Factory;

    [Theory]
    [InlineData("/api/boards")]
    [InlineData("/api/boards/{0}")]
    [InlineData("/api/boards/{0}/months/2026-05")]
    [InlineData("/api/boards/{0}/suggestions")]
    [InlineData("/api/boards/{0}/reserves")]
    public async Task Sem_sessao_todos_os_endpoints_respondem_401(string url)
    {
        var response = await Api.CreateNoRedirectClient().GetAsync(string.Format(url, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lista_mostra_so_os_boards_com_papel_em_ordem_alfabetica_pt_br()
    {
        var social = await TestData.AddBoardAsync(Cs, "Social");
        var agua = await TestData.AddBoardAsync(Cs, "Água Viva");
        var outro = await TestData.AddBoardAsync(Cs, "Outro");
        var user = await TestData.AddUserWithRoleAsync(Cs, social.Id, "viewer");
        await TestData.AddRoleAsync(Cs, user.Id, agua.Id, "tesoureiro");
        var client = await Api.SignedInClientAsync(user.Id);

        var (status, body) = await client.GetJsonAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["Água Viva", "Social"], body.Strings("name")); // Á antes de S; "Outro" não aparece
        Assert.DoesNotContain(outro.Id.ToString(), body.Strings("id"));
    }

    [Fact]
    public async Task Sem_nenhum_papel_a_lista_e_vazia()
    {
        var user = await TestDb.AddUserAsync(Cs);
        await TestData.AddBoardAsync(Cs);

        var (status, body) = await (await Api.SignedInClientAsync(user.Id)).GetJsonAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.GetArrayLength());
    }

    [Theory]
    [InlineData("viewer", false)]
    [InlineData("tesoureiro", true)]
    [InlineData("admin", true)]
    public async Task Papel_no_board_define_canMutate(string role, bool canMutate)
    {
        var board = await TestData.AddBoardAsync(Cs, "Eventos");
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);

        var (status, body) = await (await Api.SignedInClientAsync(user.Id)).GetJsonAsync($"/api/boards/{board.Id}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Eventos", body.GetProperty("name").GetString());
        Assert.Equal(canMutate, body.GetProperty("canMutate").GetBoolean());
    }

    // O RLS antigo deixava o tesoureiro global escrever sem conseguir ler. Aqui todo papel global lê.
    [Theory]
    [InlineData("viewer", false)]
    [InlineData("tesoureiro", true)]
    [InlineData("admin", true)]
    public async Task Papel_global_ve_qualquer_board_e_define_canMutate(string role, bool canMutate)
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, null, role);
        var client = await Api.SignedInClientAsync(user.Id);

        var (status, body) = await client.GetJsonAsync($"/api/boards/{board.Id}");
        var (_, list) = await client.GetJsonAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(canMutate, body.GetProperty("canMutate").GetBoolean());
        Assert.Contains(board.Id.ToString(), list.Strings("id"));
    }

    [Fact]
    public async Task Papel_global_de_escrita_vale_mesmo_com_viewer_no_board()
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        await TestData.AddRoleAsync(Cs, user.Id, null, "tesoureiro");

        var (_, body) = await (await Api.SignedInClientAsync(user.Id)).GetJsonAsync($"/api/boards/{board.Id}");

        Assert.True(body.GetProperty("canMutate").GetBoolean());
    }

    [Fact]
    public async Task Board_sem_papel_responde_404_em_todos_os_endpoints_e_nao_vaza_de_outro_board()
    {
        var meu = await TestData.AddBoardAsync(Cs);
        var alheio = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, meu.Id, "admin"); // admin, mas só no "meu"
        var client = await Api.SignedInClientAsync(user.Id);

        foreach (var url in new[] { "", "/months/2026-05", "/suggestions", "/reserves" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/boards/{alheio.Id}{url}")).StatusCode);
    }

    [Fact]
    public async Task Board_inexistente_e_404_ate_para_admin_global_e_id_invalido_tambem()
    {
        var admin = await TestData.AddUserWithRoleAsync(Cs, null, "admin");
        var client = await Api.SignedInClientAsync(admin.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/boards/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/boards/{Guid.NewGuid()}/months/2026-05")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/boards/nao-e-um-guid")).StatusCode);
    }

    [Fact]
    public async Task Papel_de_outro_usuario_nao_conta()
    {
        var board = await TestData.AddBoardAsync(Cs);
        await TestData.AddUserWithRoleAsync(Cs, board.Id, "admin"); // outra pessoa
        var intruso = await TestDb.AddUserAsync(Cs);

        var response = await (await Api.SignedInClientAsync(intruso.Id)).GetAsync($"/api/boards/{board.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
