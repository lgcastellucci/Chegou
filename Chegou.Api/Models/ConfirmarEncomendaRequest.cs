namespace Chegou.Api.Models
{
    public record ConfirmarEncomendaRequest(
        string? FotoPath,
        EtiquetaExtraida Etiqueta,
        int MoradorId,
        int? MoradorSugeridoId,
        double? ScoreMatch);
}
