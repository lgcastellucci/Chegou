using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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

        public static async Task<string> LerAsync(string rel, CancellationToken ct)
        {
            string raiz = await Configuracao.ObterAsync("Storage:Path", "storage", ct);
            var full = Path.Combine(raiz, rel);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Arquivo não encontrado: {full}");
            var bytes = await File.ReadAllBytesAsync(full, ct);
            return Convert.ToBase64String(bytes);
        }

        public static async Task<string> ReduzirFotoSeNecessarioAsync(string imgBase64, int lengthInMB, CancellationToken ct)
        {
            var bytes = Convert.FromBase64String(imgBase64);
            if (bytes.Length <= lengthInMB * 1024 * 1024) // lengthInMB MB
                return imgBase64;

            using var inputStream = new MemoryStream(bytes);
            using var image = await Image.LoadAsync(inputStream, ct);
            var scale = Math.Sqrt((lengthInMB * 1024 * 1024) / (double)bytes.Length);
            var width = (int)(image.Width * scale);
            var height = (int)(image.Height * scale);
            image.Mutate(x => x.Resize(width, height));
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, ct);
            return Convert.ToBase64String(outputStream.ToArray());
        }
    }
}
