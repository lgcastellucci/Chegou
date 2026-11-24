
namespace Chegou.Api.Services
{
    public class Storage
    {
        public static async Task<string> SalvarAsync(string condominioId, string foto, CancellationToken ct)
        {
            string raiz = await Configuracao.ObterAsync("Storage:Path", "storage", ct);

            var rel = Path.Combine(condominioId.ToString(), $"{Guid.NewGuid():N}.jpg");
            var full = Path.Combine(raiz, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var bytes = Convert.FromBase64String(foto);
            await File.WriteAllBytesAsync(full, bytes, ct);
            return rel.Replace('\\', '/');

        }

    }
}
