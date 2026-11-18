using Chegou.Api.Services;
using System.Data;
using System.Text;

namespace Chegou.Api.BancoDeDados
{
    public partial class AcessoAoBancoDeDados
    {
        public static async Task<int?> BuscarUnidade(string condominioId, string numeroNorm, string blocoNorm, CancellationToken ct)
        {
            condominioId = TextoNormalizador.NormalizarNumero(condominioId);
            if (string.IsNullOrWhiteSpace(condominioId))
                return null;

            numeroNorm = TextoNormalizador.NormalizarCodigo(numeroNorm);
            if (string.IsNullOrWhiteSpace(numeroNorm))
                return null;

            // Não checar => vazio = não tem bloco (ou não informado)   
            blocoNorm = TextoNormalizador.NormalizarCodigo(blocoNorm);

            var sql = new StringBuilder();

            sql.Clear();
            sql.Append(" SELECT TOP 2 Id ");
            sql.Append(" FROM Unidade ");
            sql.Append(" WHERE CondominioId = " + condominioId);
            sql.Append("       AND NumeroNorm =  '" + numeroNorm + "'");
            if (!string.IsNullOrWhiteSpace(blocoNorm))
                sql.Append("       AND BlocoNorm = '" + blocoNorm + "' ");

            var banco = new SqlExtensions();
            var dt = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dt.Rows.Count == 0)
                return null;

            var ids = new List<int>();

            foreach (DataRow row in dt.Rows)
                ids.Add(Convert.ToInt32(row["Id"]));

            return ids.Count == 1 ? ids[0] : null;   // ambígua ou inexistente: segue sem unidade confirmada

        }

    }
}
