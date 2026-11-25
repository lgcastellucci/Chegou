using Chegou.Api.Models;
using Chegou.Api.Services;
using System.Data;
using System.Text;

namespace Chegou.Api.BancoDeDados
{
    public partial class AcessoAoBancoDeDados
    {
        public static async Task<Morador?> BuscarMorador(string condominioId, string moradorId, CancellationToken ct)
        {
            condominioId = TextoNormalizador.NormalizarNumero(condominioId);
            if (string.IsNullOrWhiteSpace(condominioId))
                return null;

            moradorId = TextoNormalizador.NormalizarNumero(moradorId);
            if (string.IsNullOrWhiteSpace(moradorId))
                return null;

            var sql = new StringBuilder();

            sql.Clear();
            sql.Append(" SELECT Id, UnidadeId, Nome, NomeNorm, AceitaNotificacao ");
            sql.Append(" FROM Morador ");
            sql.Append(" WHERE CondominioId = " + condominioId);
            sql.Append("       AND Id = " + moradorId);

            var banco = new SqlExtensions();
            var dtMorador = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dtMorador.Rows.Count == 0)
                return null;   // morador inexistente (ou de outro condomínio)

            var row = dtMorador.Rows[0];

            var retorno = new Morador
            {
                Id = Convert.ToInt32(row["Id"]),
                UnidadeId = Convert.ToInt32(row["UnidadeId"]),
                Nome = row["Nome"].ToString() ?? string.Empty,
                NomeNorm = row["NomeNorm"].ToString() ?? string.Empty,
                AceitaNotificacao = Convert.ToBoolean(row["AceitaNotificacao"])
            };

            // Apelidos já aprendidos: o Registrar usa a lista para não gravar o mesmo apelido de novo
            sql.Clear();
            sql.Append(" SELECT ApelidoNorm ");
            sql.Append(" FROM MoradorApelido ");
            sql.Append(" WHERE MoradorId = " + retorno.Id);

            var dtApelidos = await banco.SelecionarAsync(sql.ToString(), ct);
            foreach (DataRow linha in dtApelidos.Rows)
                retorno.ApelidosNorm.Add(linha["ApelidoNorm"].ToString() ?? string.Empty);

            return retorno;
        }

    }
}
