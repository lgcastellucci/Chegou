using Chegou.Api.Domain;
using Chegou.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Chegou.Api.Controllers
{
    [ApiController]
    [Route("api/encomendas")]
    [Produces("application/json")]
    public class EncomendasController : ControllerBase
    {
        [HttpGet("pendentes")]
        [EndpointSummary("Lista as encomendas na portaria")]
        [EndpointDescription("Encomendas com status Recebida ou Notificada, da mais recente para a mais antiga.")]
        [ProducesResponseType<List<Encomenda>>(StatusCodes.Status200OK)]
        public ActionResult<List<Encomenda>> Pendentes()
        {
            var encomendasPendentes = HttpContext.RequestServices.GetRequiredService<EncomendasPendentes>();
            var result = encomendasPendentes.Executar(1);
            return Ok(result);
        }

        [HttpPost("{id:long}/retirar")]
        [EndpointSummary("Registra a retirada com o código")]
        [EndpointDescription("Exige o código de retirada de 6 dígitos. 400 se o código estiver errado; 409 se a encomenda já foi finalizada.")]
        [ProducesResponseType<RetiradaResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult<RetiradaResponse> Retirada(long id)
        {
            var encomendaRetirada = HttpContext.RequestServices.GetRequiredService<EncomendaRetirada>();
            var result = encomendaRetirada.Executar(id);
            return Ok(result);
        }


    }


}
