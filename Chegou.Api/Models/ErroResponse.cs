namespace Chegou.Api.Models
{
    public record ErroResponse(string Erro);

    public record ErroLeituraResponse(string Erro, string FotoPath);
}
