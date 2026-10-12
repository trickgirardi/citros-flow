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
    public async Task Inteiros_sao_integer_puro_nao_integer_ou_string()
    {
        var schemas = (await Doc()).GetProperty("components").GetProperty("schemas");

        foreach (var (schema, property) in new[]
                 { ("MonthSummaryResponse", "transactionCount"), ("BulkCategoryResponse", "updatedCount"),
                   ("ImportNubankResponse", "importedCount") })
        {
            var count = schemas.GetProperty(schema).GetProperty("properties").GetProperty(property);
            Assert.Equal(JsonValueKind.String, count.GetProperty("type").ValueKind); // "integer", não ["integer","string"]
            Assert.Equal("integer", count.GetProperty("type").GetString());
            Assert.False(count.TryGetProperty("pattern", out _));
        }
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

    [Fact]
    public async Task Rotas_de_escrita_import_e_compartilhamento_estao_no_contrato()
    {
        var doc = await Doc();
        var paths = doc.GetProperty("paths");
        var board = "/api/boards/{boardId}";

        foreach (var (path, method) in new[]
                 { ($"{board}/transactions", "post"), ($"{board}/transactions/{{id}}", "put"),
                   ($"{board}/transactions/{{id}}", "delete"), ($"{board}/transactions/{{id}}/description", "patch"),
                   ($"{board}/transactions/category", "patch"), ($"{board}/transactions/import/nubank", "post"),
                   ($"{board}/reserves", "post"), ($"{board}/reserves/{{id}}", "put"), ($"{board}/reserves/{{id}}", "delete"),
                   ($"{board}/share-links", "post"), ("/api/share/{token}/months/{month}", "get") })
            Assert.True(paths.GetProperty(path).TryGetProperty(method, out _), $"{method} {path}");

        // erros documentados: o cliente TS gerado tipa 400 e 403
        var create = paths.GetProperty($"{board}/transactions").GetProperty("post").GetProperty("responses");
        foreach (var status in new[] { "200", "400", "403", "404" })
            Assert.True(create.TryGetProperty(status, out _), $"POST transactions {status}");

        // dinheiro de entrada também é string decimal
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        foreach (var (schema, property) in new[] { ("TransactionRequest", "amount"), ("ReserveRequest", "amount") })
        {
            var money = schemas.GetProperty(schema).GetProperty("properties").GetProperty(property);
            Assert.Equal("string", money.GetProperty("type").GetString());
            Assert.Equal("decimal", money.GetProperty("format").GetString());
        }

        // import é multipart; a leitura pública não exige autenticação
        var import = paths.GetProperty($"{board}/transactions/import/nubank").GetProperty("post");
        Assert.True(import.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
    }
}
