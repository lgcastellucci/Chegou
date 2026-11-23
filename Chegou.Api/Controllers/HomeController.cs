using Microsoft.AspNetCore.Mvc;

namespace Chegou.Api.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult FuncHome()
        {
            var pagina = typeof(HomeController).Assembly.GetManifestResourceStream("Chegou.Api.Documentacao.index.html")
                ?? throw new InvalidOperationException("Página de documentação da API não encontrada.");

            return File(pagina, "text/html; charset=utf-8");
        }
    }
}
