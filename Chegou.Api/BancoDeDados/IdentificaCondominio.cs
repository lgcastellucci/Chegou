using Chegou.Api.Services;
using System.Text;

namespace Chegou.Api.BancoDeDados
{
    public partial class AcessoAoBancoDeDados
    {
        public static async Task<string> DevolveCondominioId(string condominioCodigo, CancellationToken ct)
        {
            condominioCodigo = TextoNormalizador.NormalizarCondominioCodigo(condominioCodigo);
            if (string.IsNullOrWhiteSpace(condominioCodigo))
                return "";

            var sql = new StringBuilder();

            sql.Clear();
            sql.Append(" SELECT Id ");
            sql.Append(" FROM Condominio ");
            sql.Append(" WHERE Codigo = '" + condominioCodigo + "'");

            //Seleciona no banco de dados as encomendas pendentes e retorna para o front-end
            var banco = new SqlExtensions();
            var dt = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dt.Rows.Count == 0)
                return "";

            return dt.Rows[0]["Id"].ToString();

        }

    }
}
