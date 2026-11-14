using Chegou.Domain.Abstractions;
using Chegou.Domain.Entities;
using Chegou.Domain.Matching;

namespace Chegou.Api.Contracts;

// ---- Requisições ----
public record ConfirmarEncomendaRequest(
    string? FotoPath,
    EtiquetaExtraida Etiqueta,
    int MoradorId,
    int? MoradorSugeridoId,
    double? ScoreMatch);

public record RetirarRequest(string Codigo, string RetiradaPorNome);

// ---- Respostas ----
public record ExtrairEtiquetaResponse(
    string FotoPath,
    EtiquetaExtraida Etiqueta,
    int? UnidadeId,
    string Nivel,
    IReadOnlyList<CandidatoMorador> Candidatos);

public record ConfirmarEncomendaResponse(long Id, string Status, string? CodigoRetirada);

public record EncomendaPendenteResponse(
    long Id,
    StatusEncomenda Status,
    string? Transportadora,
    string? Rastreio,
    DateTime RecebidaEm,
    string Morador,
    string? Bloco,
    string Numero);

public record RetiradaResponse(long Id, string Status);

public record ErroResponse(string Erro);

public record ErroLeituraResponse(string Erro, string FotoPath);
