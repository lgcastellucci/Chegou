
namespace Chegou.Api.Services
{
    public enum NivelMatch { Alto, Medio, Baixo }

    public record MoradorBusca(int Id, int UnidadeId, string Nome, string NomeNorm, IReadOnlyList<string> ApelidosNorm);
    public record CandidatoMorador(int MoradorId, string Nome, int UnidadeId, double Score);
    public record ResultadoMatch(NivelMatch Nivel, IReadOnlyList<CandidatoMorador> Candidatos);

    public static class MatchService
    {
        /// <summary>Similaridade entre dois nomes JÁ normalizados (0 a 1).</summary>
        public static double Similaridade(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return 0;
            var completo = JaroWinkler.Calcular(a, b);

            var ta = a.Split(' ');
            var tb = b.Split(' ');
            if (ta.Length < 2 || tb.Length < 2) return completo;

            // primeiro + último nome (etiquetas costumam abreviar o meio)
            var pl = JaroWinkler.Calcular($"{ta[0]} {ta[^1]}", $"{tb[0]} {tb[^1]}");
            return Math.Max(completo, pl * 0.97);
        }

        /// <summary>
        /// Alto  = unidade confirmada, score >= 0,90 e sem empate (pré-seleciona; porteiro só confirma).
        /// Médio = score >= 0,70 (mostra candidatos). Baixo = seleção manual.
        /// </summary>
        public static ResultadoMatch Avaliar(string? destinatarioLido,
            IReadOnlyList<MoradorBusca> moradores, bool unidadeConfirmada)
        {
            var alvo = TextoNormalizador.Normalizar(destinatarioLido);

            var candidatos = moradores.Select(m =>
            {
                var melhor = Similaridade(alvo, m.NomeNorm);
                foreach (var ap in m.ApelidosNorm) melhor = Math.Max(melhor, Similaridade(alvo, ap));
                return new CandidatoMorador(m.Id, m.Nome, m.UnidadeId, Math.Round(melhor, 2));
            })
            .OrderByDescending(c => c.Score)
            .Take(5)
            .ToList();

            if (candidatos.Count == 0) return new ResultadoMatch(NivelMatch.Baixo, candidatos);

            var topo = candidatos[0].Score;
            var empate = candidatos.Count > 1 && topo - candidatos[1].Score < 0.02; // homônimos
            var nivel = topo >= 0.90 && unidadeConfirmada && !empate ? NivelMatch.Alto
                      : topo >= 0.70 ? NivelMatch.Medio
                      : NivelMatch.Baixo;

            return new ResultadoMatch(nivel, candidatos);
        }
    }
}
