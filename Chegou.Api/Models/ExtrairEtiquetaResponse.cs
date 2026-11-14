using Chegou.Api.Domain.Matching;

namespace Chegou.Api.Models
{
    public record ExtrairEtiquetaResponse(
        string FotoPath,
        EtiquetaExtraida Etiqueta,
        int? UnidadeId,
        string Nivel,
        IReadOnlyList<CandidatoMorador> Candidatos);
}
