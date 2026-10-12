using System.Net;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class OriginCheckTests(PostgresFixture postgres) : IDisposable
{
    private readonly ApiFactory _factory = new(postgres.ConnectionString);

    public void Dispose() => _factory.Dispose();

    private static HttpRequestMessage Post(string? origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        if (origin is not null) request.Headers.Add("Origin", origin);
        return request;
    }

    [Fact]
    public async Task Post_com_origin_de_outro_site_e_recusado()
    {
        var response = await _factory.CreateNoRedirectClient().SendAsync(Post("http://evil.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData(null)]
    public async Task Post_do_proprio_origin_ou_sem_origin_passa(string? origin)
    {
        var response = await _factory.CreateNoRedirectClient().SendAsync(Post(origin));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Get_nao_e_afetado_pelo_origin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "http://evil.com");

        var response = await _factory.CreateNoRedirectClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Com_origin_publico_configurado_so_ele_e_aceito()
    {
        using var factory = new ApiFactory(postgres.ConnectionString,
            new() { ["App:PublicOrigin"] = "http://localhost:3000" });
        var client = factory.CreateNoRedirectClient();

        Assert.Equal(HttpStatusCode.Redirect, (await client.SendAsync(Post("http://localhost:3000"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Post("http://localhost"))).StatusCode);
    }
}
