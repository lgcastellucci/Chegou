namespace Chegou.Api.Models
{
    public class Morador
    {
        public int Id { get; set; }
        public int UnidadeId { get; set; }
        public string Nome { get; set; } = "";
        public string NomeNorm { get; set; } = "";
        public bool AceitaNotificacao { get; set; }
        public List<string> ApelidosNorm { get; set; } = new();

        public Morador()
        {
            Id = 0;
            UnidadeId = 0;
            Nome = string.Empty;
            NomeNorm = string.Empty;
            AceitaNotificacao = false;
            ApelidosNorm = new List<string>();
        }
    }
}
