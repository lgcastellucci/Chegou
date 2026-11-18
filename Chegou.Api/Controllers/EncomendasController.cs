using Chegou.Api.Controllers.Encomendas;
using Chegou.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Chegou.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EncomendasController : ControllerBase
    {
        /// <summary>
        ///  Lê a etiqueta de uma foto e sugere o morador
        /// </summary>
        /// <description> Recebe em base64 a foto da etiqueta, e devolve `nivel` (Alto/Medio/Baixo) com até 5 `candidatos`. Ainda não grava a encomenda. Retorna 422 se a leitura falhar (preencher manualmente). </description>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpPost("etiqueta")]
        public async Task<ActionResult<LeituraDaEtiqueta.Response>> FuncEtiqueta([FromBody] LeituraDaEtiqueta.Request request, CancellationToken ct)
        {
            //Request Headers
            //X-Condominio-Id: DEV-CHEGOU-CONDOMINIO-TESTE-0001
            var condominioCodigo = Request.Headers["X-Condominio-Id"].ToString();

            if (string.IsNullOrWhiteSpace(condominioCodigo))
                return BadRequest("Header não encontrado");

            var leitura = new LeituraDaEtiqueta(condominioCodigo);
            var retLeitura = await leitura.Executar(request, ct);

            if (!string.IsNullOrWhiteSpace(retLeitura.erro))
                return BadRequest(retLeitura.erro);

            return Ok(retLeitura.response);
        }


        [HttpPost("registrar")]
        public async Task<ActionResult<Registrar.Response>> FuncRegistrar([FromBody] Registrar.Request req, CancellationToken ct)
        {
            //Request Headers
            //X-Condominio-Id: DEV-CHEGOU-CONDOMINIO-TESTE-0001
            var condominioCodigo = Request.Headers["X-Condominio-Id"].ToString();

            if (string.IsNullOrWhiteSpace(condominioCodigo))
                return BadRequest("Header não encontrado");

            var registrar = new Registrar(condominioCodigo);
            var retRegistrar = await registrar.Executar(req, ct);

            if (!string.IsNullOrWhiteSpace(retRegistrar.erro))
                return BadRequest(retRegistrar.erro);

            return Ok(retRegistrar.response);
        }

        /// <summary>
        /// Obtém as encomendas pendentes do condomínio informado no cabeçalho da solicitação.
        /// </summary>
        /// <remarks>A solicitação deve incluir o cabeçalho X-Condominio-Id.</remarks>
        /// <param name="ct">Token para cancelar a operação.</param>
        /// <returns>As encomendas pendentes, ou uma resposta de erro se o cabeçalho do condomínio estiver ausente ou ocorrer um
        /// erro na consulta.</returns>
        [HttpGet("pendentes")]
        public async Task<ActionResult<List<Encomenda>>> FuncPendentes(CancellationToken ct)
        {
            //Request Headers
            //X-Condominio-Id: DEV-CHEGOU-CONDOMINIO-TESTE-0001
            var condominioCodigo = Request.Headers["X-Condominio-Id"].ToString();

            if (string.IsNullOrWhiteSpace(condominioCodigo))
                return BadRequest("Header não encontrado");

            var pendentes = new Pendentes(condominioCodigo);
            var retPendentes = await pendentes.Executar(ct);

            if (!string.IsNullOrWhiteSpace(retPendentes.erro))
                return BadRequest(retPendentes.erro);

            return Ok(retPendentes.encomendas);
        }

        /// <summary>
        /// Retira a encomenda identificada no condomínio informado.
        /// </summary>
        /// <param name="id">Identificador da encomenda a retirar.</param>
        /// <param name="ct">Token para cancelamento da operação.</param>
        /// <returns>Resultado da operação: 200 com a resposta da retirada ou 400 quando o cabeçalho do condomínio estiver
        /// ausente ou ocorrer um erro.</returns>
        [HttpPost("{id:long}/retirar")]
        public async Task<ActionResult> FuncRetirar(long id, [FromBody] Retirar.Request req, CancellationToken ct)
        {
            //Request Headers
            //X-Condominio-Id: DEV-CHEGOU-CONDOMINIO-TESTE-0001
            var condominioCodigo = Request.Headers["X-Condominio-Id"].ToString();

            if (string.IsNullOrWhiteSpace(condominioCodigo))
                return BadRequest("Header não encontrado");

            var retirar = new Retirar(condominioCodigo);
            var retRetirar = await retirar.Executar(id, req, ct);

            if (!string.IsNullOrWhiteSpace(retRetirar.erro))
                return BadRequest(retRetirar.erro);

            return Ok(retRetirar.response);
        }
    }
}
