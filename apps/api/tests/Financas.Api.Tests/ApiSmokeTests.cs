using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Financas.Api.Features.Health;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class ApiSmokeTests(PostgresFixture postgres) : IDisposable
{
    private readonly ApiFactory _factory = new(postgres.ConnectionString);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Health_responde_200_com_banco_migrado()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public void Decimal_serializa_como_string_no_json_da_api()
    {
        var options = _factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        var json = JsonSerializer.Serialize(new { amount = 1234.5m, extra = (decimal?)null, zero = 0m }, options);

        Assert.Equal("""{"amount":"1234.50","extra":null,"zero":"0.00"}""", json);
        Assert.Equal(1234.56m, JsonSerializer.Deserialize<decimal>("\"1234.56\"", options));
        Assert.Equal(7.1m, JsonSerializer.Deserialize<decimal>("7.1", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>("\"abc\"", options));
    }

    [Fact]
    public async Task OpenApi_e_servido_sob_api()
    {
        var response = await _factory.CreateClient().GetAsync("/api/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/health", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Scalar_nao_e_exposto_fora_de_development()
    {
        var response = await _factory.CreateClient().GetAsync("/api/docs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
