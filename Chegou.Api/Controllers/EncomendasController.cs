using Chegou.Api.Domain;
using Chegou.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Chegou.Api.Controllers
{
    [ApiController]
    [Route("api/encomendas")]
    [Produces("application/json")]
    public class EncomendasController(
        EncomendasPendentes encomendasPendentes,
        EncomendaRetirada encomendaRetirada,
        EncomendaExtrair encomendaExtrair,
        EncomendaConfirmar encomendaConfirmar) : ControllerBase
    {
        // PROVISÓRIO: até ter login (JWT), tudo roda no condomínio 1 e usuário 1.
        private const int CondominioIdPadrao = 1;
        private const int UsuarioIdPadrao = 1;

        [HttpPost("extrair")]
        [Consumes("multipart/form-data")]
        [EndpointSummary("Lê a etiqueta de uma foto e sugere o morador")]
        [EndpointDescription("Recebe a foto (multipart, campo `foto`), extrai destinatário, bloco/apartamento e rastreio, e devolve `nivel` (Alto/Medio/Baixo) com até 5 `candidatos`. Ainda não grava a encomenda. Retorna 422 se a leitura falhar (preencher manualmente).")]
        [ProducesResponseType<ExtrairEtiquetaResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErroLeituraResponse>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ExtrairEtiquetaResponse>> Extrair([FromForm] IFormFile foto, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await foto.CopyToAsync(ms, ct);

            var result = await encomendaExtrair.ExecutarAsync(CondominioIdPadrao, ms.ToArray(), foto.ContentType, ct);
            if (result.Falha is not null) return UnprocessableEntity(result.Falha);
            return Ok(result.Sucesso);
        }

        [HttpPost]
        [EndpointSummary("Confirma o morador, registra a encomenda e notifica")]
        [EndpointDescription("Grava a encomenda, gera o código de retirada e avisa o morador. Se o porteiro escolheu um morador diferente do sugerido, o nome lido na etiqueta é guardado como apelido do morador para acertar nas próximas vezes.")]
        [ProducesResponseType<ConfirmarEncomendaResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ConfirmarEncomendaResponse>> Confirmar([FromBody] ConfirmarEncomendaRequest req, CancellationToken ct)
        {
            var result = await encomendaConfirmar.ExecutarAsync(CondominioIdPadrao, UsuarioIdPadrao, req, ct);
            if (result.Resposta is null) return NotFound(new ErroResponse("Morador não encontrado."));
            return Created($"/api/encomendas/{result.Resposta.Id}", result.Resposta);
        }

        [HttpGet("pendentes")]
        [EndpointSummary("Lista as encomendas na portaria")]
        [EndpointDescription("Encomendas com status Recebida ou Notificada, da mais recente para a mais antiga.")]
        [ProducesResponseType<List<Encomenda>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<Encomenda>>> Pendentes(CancellationToken ct)
        {
            var result = await encomendasPendentes.ExecutarAsync(CondominioIdPadrao, ct);
            return Ok(result);
        }

        [HttpPost("{id:long}/retirar")]
        [EndpointSummary("Registra a retirada com o código")]
        [EndpointDescription("Exige o código de retirada de 6 dígitos. 400 se o código estiver errado; 409 se a encomenda já foi finalizada.")]
        [ProducesResponseType<RetiradaResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RetiradaResponse>> Retirar(long id, [FromBody] RetirarRequest req, CancellationToken ct)
        {
            var result = await encomendaRetirada.ExecutarAsync(CondominioIdPadrao, id, req, ct);

            if (result.Status == RetiradaStatus.NaoEncontrada) return NotFound();
            if (result.Status == RetiradaStatus.JaFinalizada) return Conflict(new ErroResponse("Encomenda já finalizada."));
            if (result.Status == RetiradaStatus.CodigoInvalido) return BadRequest(new ErroResponse("Código de retirada inválido."));

            return Ok(result.Resposta);
        }
    }
}
