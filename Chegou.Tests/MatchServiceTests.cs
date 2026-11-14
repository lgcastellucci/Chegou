using Chegou.Api.Domain.Matching;
using Xunit;

namespace Chegou.Tests
{
    public class MatchServiceTests
    {
        private static MoradorBusca M(int id, string nome, params string[] apelidos) =>
            new(id, 10, nome, TextoNormalizador.Normalizar(nome),
                apelidos.Select(TextoNormalizador.Normalizar).ToList());

        [Fact]
        public void Normalizar_remove_acento_pontuacao_e_preposicoes() =>
            Assert.Equal("JOAO SILVA", TextoNormalizador.Normalizar("João da  Silva."));

        [Fact]
        public void NormalizarCodigo_deixa_so_letras_e_numeros() =>
            Assert.Equal("302B", TextoNormalizador.NormalizarCodigo("302-b"));

        [Fact]
        public void JaroWinkler_iguais_retorna_1() =>
            Assert.Equal(1.0, JaroWinkler.Calcular("MARIA", "MARIA"));

        [Fact]
        public void Nome_igual_com_unidade_confirmada_da_match_alto()
        {
            var r = MatchService.Avaliar("Maria da Silva", new[] { M(1, "Maria Silva"), M(2, "Pedro Souza") }, true);
            Assert.Equal(NivelMatch.Alto, r.Nivel);
            Assert.Equal(1, r.Candidatos[0].MoradorId);
        }

        [Fact]
        public void Sem_unidade_confirmada_nunca_e_alto()
        {
            var r = MatchService.Avaliar("Maria Silva", new[] { M(1, "Maria Silva") }, false);
            Assert.NotEqual(NivelMatch.Alto, r.Nivel);
        }

        [Fact]
        public void Nome_do_meio_abreviado_ainda_acerta()
        {
            var r = MatchService.Avaliar("Joao P Oliveira", new[] { M(1, "Joao Pedro Oliveira") }, true);
            Assert.True(r.Candidatos[0].Score >= 0.90);
        }

        [Fact]
        public void Homonimos_nao_dao_match_alto()
        {
            var r = MatchService.Avaliar("Ana Lima", new[] { M(1, "Ana Lima"), M(2, "Ana Lima") }, true);
            Assert.NotEqual(NivelMatch.Alto, r.Nivel);
        }

        [Fact]
        public void Apelido_aprendido_conta_no_match()
        {
            var r = MatchService.Avaliar("JP Oliveira", new[] { M(1, "Joao Pedro Oliveira", "JP Oliveira") }, true);
            Assert.Equal(NivelMatch.Alto, r.Nivel);
        }

        [Fact]
        public void Nome_totalmente_diferente_da_baixo()
        {
            var r = MatchService.Avaliar("Carlos Mendes", new[] { M(1, "Fernanda Rocha") }, true);
            Assert.Equal(NivelMatch.Baixo, r.Nivel);
        }
    }
}
