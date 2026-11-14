namespace Chegou.Domain.Entities;

public enum StatusEncomenda { Recebida, Notificada, Retirada, Devolvida }

public class Condominio
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int RetencaoFotoDias { get; set; } = 7;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public class Unidade
{
    public int Id { get; set; }
    public int CondominioId { get; set; }
    public string? Bloco { get; set; }
    public string Numero { get; set; } = "";
    public string? BlocoNorm { get; set; }
    public string NumeroNorm { get; set; } = "";
}

public class Morador
{
    public int Id { get; set; }
    public int CondominioId { get; set; }
    public int UnidadeId { get; set; }
    public string Nome { get; set; } = "";
    public string NomeNorm { get; set; } = "";
    public string? Telefone { get; set; }
    public string? PushToken { get; set; }
    public bool AceitaNotificacao { get; set; } = true;
    public bool Ativo { get; set; } = true;
    public List<MoradorApelido> Apelidos { get; set; } = new();
}

public class MoradorApelido
{
    public int Id { get; set; }
    public int MoradorId { get; set; }
    public string ApelidoNorm { get; set; } = "";
    public string Origem { get; set; } = "CORRECAO";
}

public class Usuario
{
    public int Id { get; set; }
    public int CondominioId { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public string Perfil { get; set; } = "PORTEIRO";
    public bool Ativo { get; set; } = true;
}

public class Encomenda
{
    public long Id { get; set; }
    public int CondominioId { get; set; }
    public int? UnidadeId { get; set; }
    public int? MoradorId { get; set; }
    public Unidade? Unidade { get; set; }
    public Morador? Morador { get; set; }

    public string? FotoPath { get; set; }
    public string? DadosExtraidosJson { get; set; }
    public string? DestinatarioLido { get; set; }
    public string? BlocoLido { get; set; }
    public string? NumeroLido { get; set; }
    public string? Transportadora { get; set; }
    public string? Rastreio { get; set; }
    public decimal? Confianca { get; set; }
    public decimal? ScoreMatch { get; set; }
    public bool MatchManual { get; set; }

    public StatusEncomenda Status { get; set; } = StatusEncomenda.Recebida;
    public DateTime RecebidaEm { get; set; } = DateTime.UtcNow;
    public int RecebidaPorUsuarioId { get; set; }
    public DateTime? NotificadaEm { get; set; }
    public string? CodigoRetirada { get; set; }
    public DateTime? RetiradaEm { get; set; }
    public string? RetiradaPorNome { get; set; }
    public string? Observacao { get; set; }
}
