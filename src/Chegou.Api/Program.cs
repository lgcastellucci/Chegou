using System.Text.Json.Serialization;
using Chegou.Api;
using Chegou.Domain.Abstractions;
using Chegou.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
        {
            Title = "Chegou API",
            Version = "v1",
            Description =
                "API do Chegou: aviso automático de encomendas para condomínios.\n\n" +
                "O porteiro envia a foto da etiqueta, a IA extrai os dados, o sistema sugere o morador " +
                "e, ao confirmar, o morador é avisado.\n\n" +
                "**Autenticação provisória:** enquanto o login (JWT) não existe, o condomínio e o usuário " +
                "são informados nos headers `X-Condominio-Id` e `X-Usuario-Id` (padrão: 1)."
        };
        return Task.CompletedTask;
    });
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<ChegouDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
builder.Services.AddHttpClient<IExtratorEtiqueta, ExtratorEtiquetaClaude>(c =>
{
    c.BaseAddress = new Uri("https://api.anthropic.com/");
    c.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<IFotoStorage>(_ => new FotoStorageLocal(builder.Configuration["Storage:Path"] ?? "storage"));
builder.Services.AddScoped<INotificador, NotificadorLog>();

var app = builder.Build();

// Documentação: /scalar (interface) e /openapi/v1.json (especificação OpenAPI).
// Em produção só aparece com "Docs:Habilitar": true no appsettings.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Docs:Habilitar"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("Chegou API"));
}

app.MapGet("/", () => "Chegou API");
app.MapEncomendas();

app.Run();
