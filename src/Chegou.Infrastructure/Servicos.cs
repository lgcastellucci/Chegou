using Chegou.Domain.Abstractions;
using Chegou.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Chegou.Infrastructure;

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

/// <summary>Notificador provisório: só registra no log. Trocar por FCM (push) depois.</summary>
public class NotificadorLog(ILogger<NotificadorLog> log) : INotificador
{
    public Task NotificarAsync(Morador morador, Encomenda encomenda, CancellationToken ct)
    {
        log.LogInformation("Chegou! Encomenda {Id} para {Morador} (código de retirada {Codigo})",
            encomenda.Id, morador.Nome, encomenda.CodigoRetirada);
        return Task.CompletedTask;
    }
}
