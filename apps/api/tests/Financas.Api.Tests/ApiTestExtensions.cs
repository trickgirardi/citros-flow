using System.Net;
using System.Net.Http.Json;
using System.Text;
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

    /// <summary>Envia JSON (camelCase) com o método pedido: POST, PUT, PATCH ou DELETE.</summary>
    public static Task<HttpResponseMessage> SendJsonAsync(
        this HttpClient client, HttpMethod method, string url, object? body = null) =>
        client.SendAsync(new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : JsonContent.Create(body),
        });

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>Upload multipart com o campo "file", como o front vai enviar.</summary>
    public static Task<HttpResponseMessage> PostFileAsync(
        this HttpClient client, string url, byte[] content, string fileName = "extrato.csv")
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } };
        return client.PostAsync(url, form);
    }

    public static byte[] Utf8(this string text) => Encoding.UTF8.GetBytes(text);
}
