
namespace Chegou.Api.Services
{
    public class Storage
    {
        public static async Task<string> SalvarAsync(string condominioId, string foto, CancellationToken ct)
        {
            var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
            string raiz = configuration.GetSection("Storage")?.GetValue<string>("Path") ?? "storage";

            var rel = Path.Combine(condominioId.ToString(), $"{Guid.NewGuid():N}.jpg");
            var full = Path.Combine(raiz, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var bytes = Convert.FromBase64String(foto);
            await File.WriteAllBytesAsync(full, bytes, ct);
            return rel.Replace('\\', '/');

        }

    }
}
