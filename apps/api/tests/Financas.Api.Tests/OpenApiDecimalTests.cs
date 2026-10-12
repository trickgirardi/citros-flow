using System.Text.Json;
using Financas.Api.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Financas.Api.Tests;

public class OpenApiDecimalTests
{
    private record Price(decimal Amount, decimal? Optional);

    [Fact]
    public async Task Decimal_e_publicado_como_string_decimal_no_openapi()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddCitrosOpenApi();
        await using var app = builder.Build();
        app.MapOpenApi();
        app.MapGet("/price", () => new Price(1.5m, null));
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync("/openapi/v1.json");

        var props = JsonDocument.Parse(json).RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("Price").GetProperty("properties");
        var amount = props.GetProperty("amount");
        Assert.Equal("string", amount.GetProperty("type").GetString());
        Assert.Equal("decimal", amount.GetProperty("format").GetString());
        // decimal? mantém a nulidade: type vira ["string","null"]
        var optionalTypes = props.GetProperty("optional").GetProperty("type").EnumerateArray().Select(e => e.GetString());
        Assert.Contains("string", optionalTypes);
        Assert.Contains("null", optionalTypes);
        Assert.Equal("decimal", props.GetProperty("optional").GetProperty("format").GetString());
    }
}
