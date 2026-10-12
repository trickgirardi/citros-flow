using Financas.Api.Common;
using Financas.Api.Data;
using Financas.Api.Features.Health;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// A connection string é lida só quando o DbContext é resolvido, para o build
// (que gera o openapi.json) funcionar sem banco e sem configuração.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new DecimalStringConverter()));
builder.Services.AddCitrosOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// A geração do openapi.json no build e o `dotnet ef` executam este Program sem subir o servidor
// e não devem mexer no banco (que pode nem existir).
var designTime = Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider" or "ef" or "dotnet-ef";

if (!designTime && app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapOpenApi("/api/openapi/{documentName}.json");
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference("/api/docs", options =>
        options.WithOpenApiRoutePattern("/api/openapi/{documentName}.json"));
}

app.MapHealth();

app.Run();

public partial class Program;
