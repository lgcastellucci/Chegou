using Chegou.Api.Models;

namespace Chegou.Api.Domain.Integracoes
{
    public interface INotificador
    {
        Task NotificarAsync(Morador morador, long encomendaId, string codigoRetirada, CancellationToken ct);
    }

    /// <summary>Notificador provisório: só registra no log. Trocar por FCM (push) depois.</summary>
    public class NotificadorLog(ILogger<NotificadorLog> log) : INotificador
    {
        public Task NotificarAsync(Morador morador, long encomendaId, string codigoRetirada, CancellationToken ct)
        {
            log.LogInformation("Chegou! Encomenda {Id} para {Morador} (código de retirada {Codigo})",
                encomendaId, morador.Nome, codigoRetirada);
            return Task.CompletedTask;
        }
    }
}
