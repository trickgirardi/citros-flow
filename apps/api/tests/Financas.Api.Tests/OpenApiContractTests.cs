using System.Text.Json;

namespace Financas.Api.Tests;

[Collection(PostgresCollection.Name)]
public class OpenApiContractTests(PostgresFixture postgres)
{
    private async Task<JsonElement> Doc() =>
        JsonDocument.Parse(await postgres.Factory.CreateClient().GetStringAsync("/api/openapi/v1.json")).RootElement;

    [Fact]
    public async Task Dinheiro_e_string_decimal_nao_nula_e_tipo_da_transacao_e_enum()
    {
        var schemas = (await Doc()).GetProperty("components").GetProperty("schemas");

        // type como string simples (não ["null","string"]): o cliente TS gerado precisa de `string`, não `string | null`.
        foreach (var (schema, property) in new[]
                 { ("MonthSummaryResponse", "balance"), ("MonthSummaryResponse", "previousBalance"),
                   ("TransactionDto", "amount"), ("CategoryGroupDto", "total"), ("ReserveDto", "amount") })
        {
            var money = schemas.GetProperty(schema).GetProperty("properties").GetProperty(property);
            Assert.Equal(JsonValueKind.String, money.GetProperty("type").ValueKind);
            Assert.Equal("string", money.GetProperty("type").GetString());
            Assert.Equal("decimal", money.GetProperty("format").GetString());
        }

        Assert.Equal(["entrada", "saida"],
            schemas.GetProperty("TransactionType").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Rotas_de_leitura_estao_no_contrato()
    {
        var paths = (await Doc()).GetProperty("paths");

        foreach (var path in new[]
                 { "/api/boards", "/api/boards/{boardId}", "/api/boards/{boardId}/months/{month}",
                   "/api/boards/{boardId}/suggestions", "/api/boards/{boardId}/reserves" })
            Assert.True(paths.TryGetProperty(path, out _), path);

        var months = paths.GetProperty("/api/boards/{boardId}/months/{month}").GetProperty("get").GetProperty("responses");
        Assert.True(months.TryGetProperty("400", out _) && months.TryGetProperty("404", out _));
    }
}
