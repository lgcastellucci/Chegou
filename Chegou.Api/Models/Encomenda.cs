namespace Chegou.Api.Models
{
    /// <summary>Encomenda na portaria (item da lista de pendentes).</summary>
    public class Encomenda
    {
        public long Id { get; set; }
        public string? Status { get; set; }
        public string? Transportadora { get; set; }
        public string? Rastreio { get; set; }
        public DateTime? RecebidaEm { get; set; }
        public string? Morador { get; set; }
        public string? Bloco { get; set; }
        public string? Numero { get; set; }
    }
}
