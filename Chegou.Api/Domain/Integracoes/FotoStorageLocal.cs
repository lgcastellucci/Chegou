namespace Chegou.Api.Domain.Integracoes
{
    public interface IFotoStorage
    {
        /// <summary>Salva a foto e devolve o caminho relativo.</summary>
        Task<string> SalvarAsync(int condominioId, byte[] foto, CancellationToken ct);
    }

    public class FotoStorageLocal(string raiz) : IFotoStorage
    {
        public async Task<string> SalvarAsync(int condominioId, byte[] foto, CancellationToken ct)
        {
            var rel = Path.Combine(condominioId.ToString(), $"{Guid.NewGuid():N}.jpg");
            var full = Path.Combine(raiz, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllBytesAsync(full, foto, ct);
            return rel.Replace('\\', '/');
        }
    }
}
