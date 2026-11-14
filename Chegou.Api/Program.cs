
using Chegou.Api.Domain;

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


            // Acesso a dados: Microsoft.Data.SqlClient
            builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
                new SqlConnectionFactory(builder.Configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.")));
            builder.Services.AddScoped<EncomendasPendentes>();
            builder.Services.AddScoped<EncomendaRetirada>();

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
