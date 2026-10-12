using Microsoft.OpenApi;

namespace Financas.Api.Common;

public static class OpenApiExtensions
{
    private static readonly HashSet<Type> IntegerTypes =
        [typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort)];

    /// <summary>
    /// OpenAPI do projeto, com os tipos do JSON real: decimal é string e inteiro é número.
    /// O gerador padrão do .NET descreve a tolerância da leitura (número como texto), o que viraria
    /// "number | string" no cliente TypeScript.
    /// </summary>
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
                var isDecimal = type == typeof(decimal);
                if (!isDecimal && !IntegerTypes.Contains(type)) return Task.CompletedTask;

                // Type pode ser null (conversor customizado): sem o `is`, null & Null != 0 daria true.
                var nullable = schema.Type is { } current && (current & JsonSchemaType.Null) != 0;
                var scalar = isDecimal ? JsonSchemaType.String : JsonSchemaType.Integer;
                schema.Type = nullable ? scalar | JsonSchemaType.Null : scalar;

                if (isDecimal)
                {
                    schema.Format = "decimal";
                    schema.Pattern = @"^-?\d+(\.\d+)?$";
                }
                else schema.Pattern = null; // o padrão ^-?\d+$ só existe para a leitura aceitar texto
                return Task.CompletedTask;
            });
        });
}
