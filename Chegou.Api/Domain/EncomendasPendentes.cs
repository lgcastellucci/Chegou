using System.Data;
using Chegou.Api.Models;
using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    public class EncomendasPendentes(IDbConnectionFactory connectionFactory)
    {
        private const string Sql = """
            SELECT e.Id, e.Status, e.Transportadora, e.Rastreio, e.RecebidaEm,
                   m.Nome AS Morador, u.Bloco, u.Numero
            FROM Encomenda e
            LEFT JOIN Morador m ON m.Id = e.MoradorId
            LEFT JOIN Unidade u ON u.Id = e.UnidadeId
            WHERE e.CondominioId = @CondominioId
              AND e.Status IN ('RECEBIDA', 'NOTIFICADA')
            ORDER BY e.RecebidaEm DESC
            """;

        public async Task<List<Encomenda>> ExecutarAsync(int condominioId, CancellationToken ct)
        {
            await using var connection = await connectionFactory.AbrirAsync(ct);
            await using var command = new SqlCommand(Sql, connection);
            command.Par("@CondominioId", SqlDbType.Int, condominioId);

            await using var reader = await command.ExecuteReaderAsync(ct);
            var result = new List<Encomenda>();

            while (await reader.ReadAsync(ct))
            {
                result.Add(new Encomenda
                {
                    Id = reader.GetInt64(0),
                    Status = reader.GetStringOrNull(1),
                    Transportadora = reader.GetStringOrNull(2),
                    Rastreio = reader.GetStringOrNull(3),
                    RecebidaEm = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    Morador = reader.GetStringOrNull(5),
                    Bloco = reader.GetStringOrNull(6),
                    Numero = reader.GetStringOrNull(7)
                });
            }

            return result;
        }
    }
}
