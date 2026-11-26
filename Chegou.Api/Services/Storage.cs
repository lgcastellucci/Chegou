using SkiaSharp;

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

        public static Task<string> ReduzirFotoSeNecessarioAsync(string imgBase64, int lengthInMB, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthInMB);
            ct.ThrowIfCancellationRequested();

            var bytes = Convert.FromBase64String(imgBase64);
            long maxBytes = (long)lengthInMB * 1024 * 1024;
            if (bytes.Length <= maxBytes)
                return Task.FromResult(imgBase64);

            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data)
                ?? throw new InvalidOperationException("Não foi possível decodificar a imagem.");
            using var original = SKBitmap.Decode(codec)
                ?? throw new InvalidOperationException("Não foi possível decodificar a imagem.");
            int width = original.Width;
            int height = original.Height;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                using var resized = new SKBitmap(width, height);
                if (!original.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear)))
                    throw new InvalidOperationException("Não foi possível redimensionar a imagem.");

                using var image = SKImage.FromBitmap(resized);
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85)
                    ?? throw new InvalidOperationException("Não foi possível codificar a imagem em JPEG.");
                ct.ThrowIfCancellationRequested();
                if (encoded.Size <= maxBytes)
                    return Task.FromResult(Convert.ToBase64String(encoded.ToArray()));

                if (width == 1 && height == 1)
                    throw new InvalidOperationException("Não foi possível reduzir a imagem ao tamanho solicitado.");

                double scale = Math.Min(0.85, Math.Sqrt(maxBytes / (double)encoded.Size) * 0.9);
                width = Math.Max(1, (int)(width * scale));
                height = Math.Max(1, (int)(height * scale));
            }
        }
    }
}
