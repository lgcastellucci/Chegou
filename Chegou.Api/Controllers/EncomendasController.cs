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
        public async Task<ActionResult> FuncRegistrar( CancellationToken ct)
        {
          // var registrar = new RegistrarEncomenda(request.CondominioCodigo, request.MoradorId, request.UnidadeId, request.Transportadora, request.Rastreio);
          // var retRegistrar = await registrar.Executar(ct);
          // if (!string.IsNullOrWhiteSpace(retRegistrar.Erro))
          //     return BadRequest(retRegistrar.Erro);
            return Ok();
        }

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

        [HttpPost("{id:long}/retirar")]
        public async Task<ActionResult> FuncRetirar(long id, CancellationToken ct)
        {
          //  var retirar = new RetirarEncomenda(id, request.CondominioCodigo);
          //  var retRetirar = await retirar.Executar(ct);
          //  if (!string.IsNullOrWhiteSpace(retRetirar.Erro))
          //      return BadRequest(retRetirar.Erro);
            return Ok();
        }
    }
}
