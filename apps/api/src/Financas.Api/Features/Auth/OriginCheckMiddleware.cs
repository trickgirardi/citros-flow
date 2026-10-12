namespace Financas.Api.Features.Auth;

public static class OriginCheckMiddleware
{
    /// <summary>
    /// Proteção CSRF além do SameSite=Lax: métodos que alteram estado com header Origin diferente
    /// do origin público são recusados. Chamadas server-side do Next não mandam Origin e passam.
    /// </summary>
    public static IApplicationBuilder UseOriginCheck(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var changesState = !HttpMethods.IsGet(request.Method)
                && !HttpMethods.IsHead(request.Method)
                && !HttpMethods.IsOptions(request.Method);

            if (changesState
                && request.Headers.Origin.ToString() is { Length: > 0 } origin
                && !string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase))
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Origem não permitida.")
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
}
