using Financas.Api.Features.Auth;

namespace Financas.Api.Tests;

public class ReturnUrlTests
{
    [Theory]
    [InlineData("/board", "/board")]
    [InlineData("/board/abc?month=2026-05", "/board/abc?month=2026-05")]
    [InlineData("/", "/")]
    public void Caminho_relativo_do_proprio_site_e_aceito(string input, string expected) =>
        Assert.Equal(expected, ReturnUrl.Sanitize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("board")]
    [InlineData("https://evil.com")]
    [InlineData("http://evil.com/board")]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/board")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/board\r\nSet-Cookie: x=1")]
    public void Qualquer_outra_coisa_vira_o_padrao(string? input) =>
        Assert.Equal(ReturnUrl.Default, ReturnUrl.Sanitize(input));
}
