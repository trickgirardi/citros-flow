using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Financas.Api.Tests;

/// <summary>API real apontando para o Postgres do Testcontainers, com Google "configurado" e /test/signin.</summary>
public class ApiFactory(
    string connectionString, Dictionary<string, string?>? settings = null, TimeProvider? clock = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Google:ClientId", "test-client-id");
        builder.UseSetting("Google:ClientSecret", "test-client-secret");
        foreach (var (key, value) in settings ?? [])
            builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestSignInStartupFilter>();
            // Relógio do cookie de sessão (expiração e renovação) controlável pelo teste.
            if (clock is not null)
                services.PostConfigure<CookieAuthenticationOptions>(
                    CookieAuthenticationDefaults.AuthenticationScheme, o => o.TimeProvider = clock);
        });
    }

    public HttpClient CreateNoRedirectClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
