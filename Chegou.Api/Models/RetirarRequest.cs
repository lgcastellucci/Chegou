namespace Chegou.Api.Models
{
    public class RetirarRequest
    {
        /// <summary>Código de retirada de 6 dígitos.</summary>
        public string Codigo { get; set; } = "";

        /// <summary>Nome de quem retirou.</summary>
        public string RetiradaPorNome { get; set; } = "";
    }
}
