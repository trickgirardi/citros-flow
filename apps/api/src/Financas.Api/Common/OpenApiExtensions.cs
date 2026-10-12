using Microsoft.OpenApi;

namespace Financas.Api.Common;

public static class OpenApiExtensions
{
    /// <summary>OpenAPI do projeto. decimal é publicado como string, igual ao JSON real.</summary>
    public static IServiceCollection AddCitrosOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Citros Flow API";
                return Task.CompletedTask;
            });

            options.AddSchemaTransformer((schema, context, _) =>
            {
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
                if (type == typeof(decimal))
                {
                    var nullable = (schema.Type & JsonSchemaType.Null) != 0;
                    schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
                    schema.Format = "decimal";
                    schema.Pattern = @"^-?\d+(\.\d+)?$";
                }
                return Task.CompletedTask;
            });
        });
}
