using System.Text.Json.Serialization;

namespace Chegou.Api.Models
{
    /// <summary>Dados lidos da foto da etiqueta pelo modelo de visão.</summary>
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
}
