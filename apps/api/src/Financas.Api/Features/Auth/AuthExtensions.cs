using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace Financas.Api.Features.Auth;

public static class AuthExtensions
{
    public const string GoogleCallbackPath = "/api/auth/signin-google";

    public static bool GoogleConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Google:ClientId"])
        && !string.IsNullOrWhiteSpace(config["Google:ClientSecret"]);

    /// <summary>
    /// Cookie de sessão + login Google. O Google só é registrado se houver ClientId/ClientSecret:
    /// o handler valida as opções em todo request e quebraria a API inteira sem elas.
    /// </summary>
    public static IServiceCollection AddCitrosAuth(this IServiceCollection services, IConfiguration config)
    {
        // As chaves protegem o cookie de sessão. Sem KeysPath usa o padrão do perfil do usuário (dev).
        var dataProtection = services.AddDataProtection().SetApplicationName("citros-flow");
        if (config["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        var auth = services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(SessionCookie.Configure);

        if (GoogleConfigured(config))
        {
            auth.AddGoogle(options =>
            {
                options.ClientId = config["Google:ClientId"]!;
                options.ClientSecret = config["Google:ClientSecret"]!;
                options.CallbackPath = GoogleCallbackPath;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                // O handler não mapeia esse claim por padrão; a allowlist exige e-mail verificado.
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");
                // Padrão do ASP.NET é SameSite=None + Secure, frágil em http://localhost.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Events.OnTicketReceived = GoogleSignIn.OnTicketReceivedAsync;
                options.Events.OnRemoteFailure = GoogleSignIn.OnRemoteFailureAsync;
            });
        }

        services.AddAuthorization();
        return services;
    }
}
