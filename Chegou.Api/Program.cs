using Chegou.Api.Domain;
using Chegou.Api.Domain.Integracoes;

namespace Chegou.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();


            // Acesso a dados: Microsoft.Data.SqlClient (ADO.NET)
            builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
                new SqlConnectionFactory(builder.Configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.")));

            // Casos de uso (um por classe)
            builder.Services.AddScoped<EncomendasPendentes>();
            builder.Services.AddScoped<EncomendaRetirada>();
            builder.Services.AddScoped<EncomendaExtrair>();
            builder.Services.AddScoped<EncomendaConfirmar>();

            // Integrações: leitura da etiqueta (Claude), fotos e notificação
            builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
            builder.Services.AddHttpClient<IExtratorEtiqueta, ExtratorEtiquetaClaude>(c =>
            {
                c.BaseAddress = new Uri("https://api.anthropic.com/");
                c.Timeout = TimeSpan.FromSeconds(60);
            });
            builder.Services.AddSingleton<IFotoStorage>(_ =>
                new FotoStorageLocal(builder.Configuration["Storage:Path"] ?? "storage"));
            builder.Services.AddScoped<INotificador, NotificadorLog>();

            var app = builder.Build();

            // Documentação: /swagger (Swagger UI) e /openapi/v1.json (especificação OpenAPI).
            // Em produção só aparece com "Docs:Habilitar": true no appsettings.
            if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Docs:Habilitar"))
            {
                app.MapOpenApi();
                app.UseSwaggerUI(o =>
                {
                    o.SwaggerEndpoint("/openapi/v1.json", "Chegou API v1");
                    o.DocumentTitle = "Chegou API";
                });
            }

            app.UseAuthorization();

            app.MapGet("/", () => "Chegou API");
            app.MapControllers();

            app.Run();
        }
    }
}
