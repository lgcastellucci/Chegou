using System.Text.Json.Serialization;
using Chegou.Domain.Entities;

namespace Chegou.Domain.Abstractions;

public record EtiquetaExtraida
{
    [JsonPropertyName("transportadora")] public string? Transportadora { get; init; }
    [JsonPropertyName("destinatario")] public string? Destinatario { get; init; }
    [JsonPropertyName("bloco")] public string? Bloco { get; init; }
    [JsonPropertyName("apartamento")] public string? Apartamento { get; init; }
    [JsonPropertyName("endereco_linha")] public string? EnderecoLinha { get; init; }
    [JsonPropertyName("remetente")] public string? Remetente { get; init; }
    [JsonPropertyName("rastreio")] public string? Rastreio { get; init; }
    [JsonPropertyName("confianca")] public double Confianca { get; init; }
    [JsonPropertyName("problemas")] public List<string> Problemas { get; init; } = new();
}

public interface IExtratorEtiqueta
{
    Task<EtiquetaExtraida> ExtrairAsync(byte[] foto, string mediaType, CancellationToken ct);
}

public interface IFotoStorage
{
    /// <summary>Salva a foto e devolve o caminho relativo.</summary>
    Task<string> SalvarAsync(int condominioId, byte[] foto, CancellationToken ct);
}

public interface INotificador
{
    Task NotificarAsync(Morador morador, Encomenda encomenda, CancellationToken ct);
}
