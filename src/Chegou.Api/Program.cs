using System.Text.Json.Serialization;
using Chegou.Api;
using Chegou.Domain.Abstractions;
using Chegou.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/", () => "Chegou API");
app.MapEncomendas();

app.Run();
