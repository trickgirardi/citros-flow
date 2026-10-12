using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public partial class ShareLinkTests(PostgresFixture postgres)
{
    private string Cs => postgres.ConnectionString;

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex Base64Url43();

    private async Task<(Board Board, User User, HttpClient Client)> Setup(string role = "tesoureiro")
    {
        var board = await TestData.AddBoardAsync(Cs);
        var user = await TestData.AddUserWithRoleAsync(Cs, board.Id, role);
        return (board, user, await postgres.Factory.SignedInClientAsync(user.Id));
    }

    private static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private async Task<string> CreateToken(HttpClient client, Guid boardId)
    {
        var response = await client.SendJsonAsync(HttpMethod.Post, $"/api/boards/{boardId}/share-links");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("token").GetString()!;
    }

    private HttpClient Anonymous() => postgres.Factory.CreateNoRedirectClient();

    // ---------- criar ----------

    [Fact]
    public async Task Criar_devolve_token_de_32_bytes_e_o_banco_guarda_so_o_hash_sha256()
    {
        var (board, user, client) = await Setup();

        var token = await CreateToken(client, board.Id);
        var outro = await CreateToken(client, board.Id);

        Assert.Matches(Base64Url43(), token);
        Assert.NotEqual(token, outro);
        await using var db = TestDb.Open(Cs);
        var links = await db.BoardShareLinks.AsNoTracking().Where(l => l.BoardId == board.Id).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.TokenHash == Sha256Hex(token));
        Assert.DoesNotContain(links, l => l.TokenHash == token); // o token cru nunca é salvo
        Assert.All(links, l =>
        {
            Assert.Equal(user.Id, l.CreatedBy);
            Assert.Null(l.RevokedAt);
            Assert.Null(l.ExpiresAt);
            Assert.Matches("^[0-9a-f]{64}$", l.TokenHash);
        });
    }

    [Fact]
    public async Task Viewer_403_sem_acesso_404_sem_sessao_401_e_nada_e_criado()
    {
        var (board, _, _) = await Setup();
        var viewer = await TestData.AddUserWithRoleAsync(Cs, board.Id, "viewer");
        var intruso = await TestDb.AddUserAsync(Cs);
        var url = $"/api/boards/{board.Id}/share-links";

        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await postgres.Factory.SignedInClientAsync(viewer.Id)).SendJsonAsync(HttpMethod.Post, url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await (await postgres.Factory.SignedInClientAsync(intruso.Id)).SendJsonAsync(HttpMethod.Post, url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().SendJsonAsync(HttpMethod.Post, url)).StatusCode);
        await using var db = TestDb.Open(Cs);
        Assert.Equal(0, await db.BoardShareLinks.CountAsync(l => l.BoardId == board.Id));
    }

    // ---------- leitura pública ----------

    [Fact]
    public async Task Publico_le_sem_sessao_e_ve_exatamente_o_resumo_do_mes_do_board()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "entrada", 100m, "2026-04-10", "Doações");
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "entrada", 50m, "2026-05-10", "Doações");
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "saida", 20.25m, "2026-05-11", "Luz");
        await TestData.AddReserveAsync(Cs, board.Id, user.Id, "Caixinha", 70m);
        var token = await CreateToken(client, board.Id);

        var (status, shared) = await Anonymous().GetJsonAsync($"/api/share/{token}/months/2026-05");
        var (_, authenticated) = await client.GetJsonAsync($"/api/boards/{board.Id}/months/2026-05");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(board.Name, shared.GetProperty("boardName").GetString());
        Assert.Equal(authenticated.GetRawText(), shared.GetProperty("summary").GetRawText());
        Assert.Equal("129.75", shared.GetProperty("summary").GetProperty("balance").GetString()); // 100 + 50 - 20.25
        Assert.Equal("70.00", shared.GetProperty("summary").GetProperty("reservesTotal").GetString());
    }

    [Fact]
    public async Task Publico_nao_expoe_sugestoes_nem_lista_de_reservas_nem_ids_do_board()
    {
        var (board, user, client) = await Setup();
        await TestData.AddReserveAsync(Cs, board.Id, user.Id, "Reserva secreta", 70m);
        var token = await CreateToken(client, board.Id);

        var (_, shared) = await Anonymous().GetJsonAsync($"/api/share/{token}/months/2026-05");

        Assert.Equal(["boardName", "summary"], shared.EnumerateObject().Select(p => p.Name));
        var raw = shared.GetRawText();
        Assert.DoesNotContain("Reserva secreta", raw);
        Assert.DoesNotContain(board.Id.ToString(), raw);
        Assert.DoesNotContain(user.Id.ToString(), raw);
    }

    [Fact]
    public async Task Publico_responde_com_no_store_e_navega_por_qualquer_mes()
    {
        var (board, user, client) = await Setup();
        await TestData.AddTransactionAsync(Cs, board.Id, user.Id, "entrada", 10m, "2025-12-31");
        var token = await CreateToken(client, board.Id);

        var response = await Anonymous().GetAsync($"/api/share/{token}/months/2026-01");
        var summary = (await response.ReadJsonAsync()).GetProperty("summary");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("10.00", summary.GetProperty("previousBalance").GetString());
    }

    [Fact]
    public async Task Token_inexistente_ou_o_proprio_hash_gravado_nao_abre_nada()
    {
        var (board, _, client) = await Setup();
        var token = await CreateToken(client, board.Id);

        foreach (var bad in new[] { "token-que-nao-existe", token + "x", token[..^1], Sha256Hex(token), token.ToUpperInvariant() })
            Assert.Equal(HttpStatusCode.NotFound, (await Anonymous().GetAsync($"/api/share/{bad}/months/2026-05")).StatusCode);
    }

    [Fact]
    public async Task Link_revogado_ou_expirado_responde_404_e_o_com_validade_futura_funciona()
    {
        var (board, _, client) = await Setup();
        var revogado = await CreateToken(client, board.Id);
        var expirado = await CreateToken(client, board.Id);
        var valido = await CreateToken(client, board.Id);
        await using (var db = TestDb.Open(Cs))
        {
            await db.BoardShareLinks.Where(l => l.TokenHash == Sha256Hex(revogado))
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.RevokedAt, (DateTimeOffset?)DateTimeOffset.UtcNow));
            await db.BoardShareLinks.Where(l => l.TokenHash == Sha256Hex(expirado))
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, (DateTimeOffset?)DateTimeOffset.UtcNow.AddMinutes(-1)));
            await db.BoardShareLinks.Where(l => l.TokenHash == Sha256Hex(valido))
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(1)));
        }

        Assert.Equal(HttpStatusCode.NotFound, (await Anonymous().GetAsync($"/api/share/{revogado}/months/2026-05")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Anonymous().GetAsync($"/api/share/{expirado}/months/2026-05")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Anonymous().GetAsync($"/api/share/{valido}/months/2026-05")).StatusCode);
    }

    [Fact]
    public async Task Link_de_um_board_nao_mostra_dados_de_outro()
    {
        var (a, user, clientA) = await Setup();
        var b = await TestData.AddBoardAsync(Cs);
        await TestData.AddTransactionAsync(Cs, a.Id, user.Id, "entrada", 1m, "2026-05-10", "Só do A");
        await TestData.AddTransactionAsync(Cs, b.Id, user.Id, "entrada", 999m, "2026-05-10", "Só do B");
        var token = await CreateToken(clientA, a.Id);

        var (_, shared) = await Anonymous().GetJsonAsync($"/api/share/{token}/months/2026-05");

        Assert.Equal("1.00", shared.GetProperty("summary").GetProperty("balance").GetString());
        Assert.DoesNotContain("Só do B", shared.GetRawText());
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("abc")]
    [InlineData("2026-1")]
    public async Task Mes_invalido_responde_400(string month)
    {
        var (board, _, client) = await Setup();
        var token = await CreateToken(client, board.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Anonymous().GetAsync($"/api/share/{token}/months/{month}")).StatusCode);
    }
}
