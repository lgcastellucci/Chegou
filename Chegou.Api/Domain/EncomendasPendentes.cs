using Chegou.Api.Models;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Chegou.Api.Domain
{
    public class EncomendasPendentes(IDbConnectionFactory connectionFactory)
    {
        public List<Encomenda> Executar(int condominioId)
        {
            var sql = new StringBuilder();

            sql.Clear();
            sql.Append(" SELECT e.Id, e.Status, e.Transportadora, e.Rastreio, e.RecebidaEm, ");
            sql.Append("        m.Nome AS Morador, u.Bloco, u.Numero ");
            sql.Append(" FROM Encomenda e ");
            sql.Append(" LEFT JOIN Morador m ON m.Id = e.MoradorId ");
            sql.Append(" LEFT JOIN Unidade u ON u.Id = e.UnidadeId ");
            sql.Append(" WHERE e.CondominioId =  " + condominioId);
            sql.Append("       AND e.Status IN('RECEBIDA', 'NOTIFICADA') ");
            sql.Append(" ORDER BY e.RecebidaEm DESC ");

            using var connection = connectionFactory.Abrir();
            using var command = new SqlCommand(sql.ToString(), connection);
            using var reader = command.ExecuteReader();
            var result = new List<Encomenda>();

            while (reader.Read())
            {
                result.Add(new Encomenda
                {
                    Id = reader.GetInt32(0),
                    Status = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Transportadora = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Rastreio = reader.IsDBNull(3) ? null : reader.GetString(3),
                    RecebidaEm = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    Morador = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Bloco = reader.IsDBNull(6) ? null : reader.GetValue(6).ToString(),
                    Numero = reader.IsDBNull(7) ? null : reader.GetValue(7).ToString()
                });
            }

            return result;
        }
    }
}
