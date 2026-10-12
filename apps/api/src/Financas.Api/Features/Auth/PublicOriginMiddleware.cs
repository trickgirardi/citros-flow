namespace Financas.Api.Features.Auth;

public static class PublicOriginMiddleware
{
    /// <summary>
    /// A API fica atrás de um proxy (rewrite do Next em dev, reverse proxy depois) que não repassa
    /// scheme/host de forma confiável. Com App:PublicOrigin configurado, scheme e host de todo request
    /// passam a ser os do origin público, de modo que o redirect_uri do Google e os redirects
    /// absolutos saem corretos sem depender de headers.
    /// </summary>
    public static IApplicationBuilder UsePublicOrigin(this WebApplication app)
    {
        if (app.Configuration["App:PublicOrigin"] is not { Length: > 0 } origin)
            return app;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.PathAndQuery != "/")
            throw new InvalidOperationException($"App:PublicOrigin inválido: '{origin}'. Use algo como http://localhost:3000.");

        return app.Use((context, next) =>
        {
            context.Request.Scheme = uri.Scheme;
            context.Request.Host = new HostString(uri.Authority);
            return next(context);
        });
    }
}
