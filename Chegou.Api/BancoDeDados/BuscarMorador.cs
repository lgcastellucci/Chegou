using Chegou.Api.Models;
using Chegou.Api.Services;
using System.Text;

namespace Chegou.Api.BancoDeDados
{
    public partial class AcessoAoBancoDeDados
    {
        public static async Task<Morador?> BuscarMorador(string condominioId, string modadorId, CancellationToken ct)
        {
            var retorno = new Morador();

            condominioId = TextoNormalizador.NormalizarNumero(condominioId);
            if (string.IsNullOrWhiteSpace(condominioId))
                return null;

            modadorId = TextoNormalizador.NormalizarNumero(modadorId);
            if (string.IsNullOrWhiteSpace(modadorId))
                return null;

            var sql = new StringBuilder();

            sql.Clear();
            sql.Append(" SELECT Id, UnidadeId, Nome, NomeNorm, AceitaNotificacao ");
            sql.Append(" FROM Morador ");
            sql.Append(" WHERE CondominioId = " + condominioId);
            sql.Append("       AND Id = " + modadorId);

            var banco = new SqlExtensions();
            var dtMorador = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dtMorador.Rows.Count > 0)
            {
                var row = dtMorador.Rows[0];

                retorno.Id = Convert.ToInt32(row["Id"]);
                retorno.UnidadeId = Convert.ToInt32(row["UnidadeId"]);
                retorno.Nome = row["Nome"].ToString() ?? string.Empty;
                retorno.NomeNorm = row["NomeNorm"].ToString() ?? string.Empty;
                retorno.AceitaNotificacao = Convert.ToBoolean(row["AceitaNotificacao"]);
            }
            else
            {
                sql.Clear();
                sql.Append(" SELECT ApelidoNorm ");
                sql.Append(" FROM MoradorApelido ");
                sql.Append(" WHERE MoradorId = " + modadorId);

                var dtMoradorApelido = await banco.SelecionarAsync(sql.ToString(), ct);
                if (dtMoradorApelido.Rows.Count > 0)
                {
                    var row = dtMoradorApelido.Rows[0];

                    retorno.ApelidosNorm.Add(row["ApelidoNorm"].ToString() ?? string.Empty);
                }
                else
                {
                    return null;
                }
            }

            return retorno;
        }

    }
}
