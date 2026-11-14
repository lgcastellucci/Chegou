using System.Security.Cryptography;
using System.Text.Json;
using Chegou.Api.Contracts;
using Chegou.Domain.Abstractions;
using Chegou.Domain.Entities;
using Chegou.Domain.Matching;
using Chegou.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chegou.Api.Controllers;

[ApiController]
[Route("api/encomendas")]
[Produces("application/json")]
public class EncomendasController(
    ChegouDbContext db,
    IExtratorEtiqueta extrator,
    IFotoStorage storage,
    INotificador notificador) : ControllerBase
{
    // PROVISÓRIO: até ter login (JWT), condomínio e usuário vêm por header (padrão 1).
    private const string HeaderCondominio = "X-Condominio-Id";
    private const string HeaderUsuario = "X-Usuario-Id";
    private const int PadraoDev = 1;

    [HttpPost("extrair")]
    [Consumes("multipart/form-data")]
    [EndpointSummary("Lê a etiqueta de uma foto e sugere o morador")]
    [EndpointDescription("Recebe a foto (multipart, campo `foto`), extrai destinatário, bloco/apartamento e rastreio, " +
                         "e devolve `nivel` (Alto/Medio/Baixo) com até 5 `candidatos`. Ainda não grava a encomenda. " +
                         "Retorna 422 se a leitura falhar (preencher manualmente).")]
    [ProducesResponseType<ExtrairEtiquetaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroLeituraResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExtrairEtiquetaResponse>> Extrair(
        [FromForm] IFormFile foto,
        [FromHeader(Name = HeaderCondominio)] int? condominioId,
        CancellationToken ct)
    {
        var cid = condominioId ?? PadraoDev;

        using var ms = new MemoryStream();
        await foto.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var fotoPath = await storage.SalvarAsync(cid, bytes, ct);

        EtiquetaExtraida etiqueta;
        try
        {
            etiqueta = await extrator.ExtrairAsync(bytes, foto.ContentType ?? "image/jpeg", ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return UnprocessableEntity(new ErroLeituraResponse(
                "Não foi possível ler a etiqueta. Preencha manualmente.", fotoPath));
        }

        var blocoNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Bloco);
        var numNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Apartamento);

        Unidade? unidade = null;
        if (numNorm.Length > 0)
        {
            var achadas = await db.Unidades
                .Where(u => u.CondominioId == cid && u.NumeroNorm == numNorm
                            && (blocoNorm == "" || u.BlocoNorm == blocoNorm))
                .ToListAsync(ct);
            if (achadas.Count == 1) unidade = achadas[0];
        }

        var q = db.Moradores.Include(m => m.Apelidos).Where(m => m.CondominioId == cid && m.Ativo);
        if (unidade != null) q = q.Where(m => m.UnidadeId == unidade.Id);
        var moradores = await q.ToListAsync(ct);

        var buscas = moradores
            .Select(m => new MoradorBusca(m.Id, m.UnidadeId, m.Nome, m.NomeNorm,
                m.Apelidos.Select(a => a.ApelidoNorm).ToList()))
            .ToList();

        var resultado = MatchService.Avaliar(etiqueta.Destinatario, buscas, unidade != null);

        return Ok(new ExtrairEtiquetaResponse(
            fotoPath, etiqueta, unidade?.Id, resultado.Nivel.ToString(), resultado.Candidatos));
    }

    [HttpPost]
    [EndpointSummary("Confirma o morador, registra a encomenda e notifica")]
    [EndpointDescription("Grava a encomenda, gera o código de retirada e avisa o morador. " +
                         "Se o porteiro escolheu um morador diferente do sugerido, o nome lido na etiqueta " +
                         "é guardado como apelido do morador para acertar nas próximas vezes.")]
    [ProducesResponseType<ConfirmarEncomendaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfirmarEncomendaResponse>> Confirmar(
        [FromBody] ConfirmarEncomendaRequest req,
        [FromHeader(Name = HeaderCondominio)] int? condominioId,
        [FromHeader(Name = HeaderUsuario)] int? usuarioId,
        CancellationToken ct)
    {
        var cid = condominioId ?? PadraoDev;

        var morador = await db.Moradores.Include(m => m.Apelidos)
            .FirstOrDefaultAsync(m => m.Id == req.MoradorId && m.CondominioId == cid, ct);
        if (morador is null) return NotFound(new ErroResponse("Morador não encontrado."));

        var manual = req.MoradorSugeridoId != req.MoradorId;
        var enc = new Encomenda
        {
            CondominioId = cid,
            UnidadeId = morador.UnidadeId,
            MoradorId = morador.Id,
            FotoPath = req.FotoPath,
            DadosExtraidosJson = JsonSerializer.Serialize(req.Etiqueta),
            DestinatarioLido = req.Etiqueta.Destinatario,
            BlocoLido = req.Etiqueta.Bloco,
            NumeroLido = req.Etiqueta.Apartamento,
            Transportadora = req.Etiqueta.Transportadora,
            Rastreio = req.Etiqueta.Rastreio,
            Confianca = (decimal)req.Etiqueta.Confianca,
            ScoreMatch = req.ScoreMatch is null ? null : (decimal)req.ScoreMatch.Value,
            MatchManual = manual,
            RecebidaPorUsuarioId = usuarioId ?? PadraoDev,
            CodigoRetirada = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")
        };

        // aprendizado: o nome da etiqueta vira apelido do morador escolhido
        if (manual)
        {
            var apelido = TextoNormalizador.Normalizar(req.Etiqueta.Destinatario);
            if (apelido.Length > 0 && apelido != morador.NomeNorm
                && !morador.Apelidos.Any(a => a.ApelidoNorm == apelido))
                morador.Apelidos.Add(new MoradorApelido { ApelidoNorm = apelido });
        }

        db.Encomendas.Add(enc);
        await db.SaveChangesAsync(ct);

        if (morador.AceitaNotificacao)
        {
            await notificador.NotificarAsync(morador, enc, ct);
            enc.Status = StatusEncomenda.Notificada;
            enc.NotificadaEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Created($"/api/encomendas/{enc.Id}",
            new ConfirmarEncomendaResponse(enc.Id, enc.Status.ToString(), enc.CodigoRetirada));
    }

    [HttpGet("pendentes")]
    [EndpointSummary("Lista as encomendas na portaria")]
    [EndpointDescription("Encomendas com status Recebida ou Notificada, da mais recente para a mais antiga.")]
    [ProducesResponseType<List<EncomendaPendenteResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EncomendaPendenteResponse>>> Pendentes(
        [FromHeader(Name = HeaderCondominio)] int? condominioId,
        CancellationToken ct)
    {
        var cid = condominioId ?? PadraoDev;

        var lista = await db.Encomendas
            .Where(e => e.CondominioId == cid
                        && (e.Status == StatusEncomenda.Recebida || e.Status == StatusEncomenda.Notificada))
            .OrderByDescending(e => e.RecebidaEm)
            .Select(e => new EncomendaPendenteResponse(
                e.Id, e.Status, e.Transportadora, e.Rastreio, e.RecebidaEm,
                e.Morador!.Nome, e.Unidade!.Bloco, e.Unidade!.Numero))
            .ToListAsync(ct);

        return Ok(lista);
    }

    [HttpPost("{id:long}/retirar")]
    [EndpointSummary("Registra a retirada com o código")]
    [EndpointDescription("Exige o código de retirada de 6 dígitos. 400 se o código estiver errado; " +
                         "409 se a encomenda já foi finalizada.")]
    [ProducesResponseType<RetiradaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RetiradaResponse>> Retirar(
        long id,
        [FromBody] RetirarRequest req,
        [FromHeader(Name = HeaderCondominio)] int? condominioId,
        CancellationToken ct)
    {
        var cid = condominioId ?? PadraoDev;

        var enc = await db.Encomendas.FirstOrDefaultAsync(e => e.Id == id && e.CondominioId == cid, ct);
        if (enc is null) return NotFound();
        if (enc.Status is StatusEncomenda.Retirada or StatusEncomenda.Devolvida)
            return Conflict(new ErroResponse("Encomenda já finalizada."));
        if (enc.CodigoRetirada != req.Codigo)
            return BadRequest(new ErroResponse("Código de retirada inválido."));

        enc.Status = StatusEncomenda.Retirada;
        enc.RetiradaEm = DateTime.UtcNow;
        enc.RetiradaPorNome = req.RetiradaPorNome;
        await db.SaveChangesAsync(ct);

        return Ok(new RetiradaResponse(enc.Id, enc.Status.ToString()));
    }
}
