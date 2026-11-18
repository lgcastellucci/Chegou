using Chegou.Api.Services;
using System.Data;
using System.Text;

namespace Chegou.Api.BancoDeDados
{
    public partial class AcessoAoBancoDeDados
    {
        public static async Task<List<MoradorBusca>> BuscarMoradores(string condominioId, string unidadeId, CancellationToken ct)
        {
            var retorno = new List<MoradorBusca>();

            condominioId = TextoNormalizador.NormalizarNumero(condominioId);
            if (string.IsNullOrWhiteSpace(condominioId))
                return retorno;

            // Não checar => vazio = todos os moradores do condomínio
            unidadeId = TextoNormalizador.NormalizarNumero(unidadeId);   

            var sql = new StringBuilder();

            sql.Clear();
            sql.Append(" SELECT Id, UnidadeId, Nome, NomeNorm ");
            sql.Append(" FROM Morador ");
            sql.Append(" WHERE CondominioId = " + condominioId);
            sql.Append("       AND Ativo = 1 ");
            if (!string.IsNullOrWhiteSpace(unidadeId))
                sql.Append("      AND  UnidadeId = " + unidadeId);

            var moradores = new List<(int Id, int UnidadeId, string Nome, string NomeNorm)>();
            var apelidos = new Dictionary<int, List<string>>();

            var banco = new SqlExtensions();
            var dt = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dt.Rows.Count > 0)
                foreach (DataRow row in dt.Rows)
                    moradores.Add((Convert.ToInt32(row["Id"]), Convert.ToInt32(row["UnidadeId"]), row["Nome"].ToString(), row["NomeNorm"].ToString()));


            sql.Clear();
            sql.Append(" SELECT a.MoradorId, a.ApelidoNorm ");
            sql.Append(" FROM MoradorApelido a ");
            sql.Append(" JOIN Morador m ON m.Id = a.MoradorId ");
            sql.Append(" WHERE m.CondominioId = " + condominioId);
            sql.Append(" AND m.Ativo = 1");
            if (!string.IsNullOrWhiteSpace(unidadeId))
                sql.Append(" AND m.UnidadeId = " + unidadeId);

            var dtApelidos = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dtApelidos.Rows.Count > 0)
            {
                foreach (DataRow row in dtApelidos.Rows)
                {
                    var moradorId = Convert.ToInt32(row["MoradorId"]);
                    if (!apelidos.TryGetValue(moradorId, out var lista)) apelidos[moradorId] = lista = new List<string>();
                    lista.Add(row["ApelidoNorm"].ToString());
                }
            }

            retorno = moradores.Select(m => new MoradorBusca(m.Id, m.UnidadeId, m.Nome, m.NomeNorm, apelidos.TryGetValue(m.Id, out var a) ? a : new List<string>())).ToList();

            return retorno;
        }

    }
}
