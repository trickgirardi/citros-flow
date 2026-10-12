using System.Net;
using System.Text.Json;

namespace Financas.Api.Tests;

public static class ApiTestExtensions
{
    /// <summary>Cliente com a sessão (cookie real) do usuário, sem seguir redirects.</summary>
    public static async Task<HttpClient> SignedInClientAsync(this ApiFactory factory, Guid userId)
    {
        var client = factory.CreateNoRedirectClient();
        (await client.GetAsync($"/test/signin?userId={userId}")).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Status e corpo JSON cru: os testes conferem o formato real do fio (ex.: dinheiro como string).</summary>
    public static async Task<(HttpStatusCode Status, JsonElement Body)> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        var body = text.Length > 0 && response.Content.Headers.ContentType?.MediaType?.Contains("json") == true
            ? JsonDocument.Parse(text).RootElement.Clone()
            : default;
        return (response.StatusCode, body);
    }

    public static IEnumerable<string> Strings(this JsonElement array, string property) =>
        array.EnumerateArray().Select(e => e.GetProperty(property).GetString()!);
}
