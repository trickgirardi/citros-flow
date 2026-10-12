namespace Financas.Api.Features.Auth;

public static class ReturnUrl
{
    public const string Default = "/board";

    /// <summary>
    /// Só aceita caminho relativo do próprio site ("/board?month=2026-05").
    /// Qualquer outra coisa (URL absoluta, "//host", "/\host", caracteres de controle) vira o padrão.
    /// </summary>
    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || returnUrl[0] != '/') return Default;
        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')) return Default;
        if (returnUrl.Any(char.IsControl)) return Default;
        return returnUrl;
    }
}
