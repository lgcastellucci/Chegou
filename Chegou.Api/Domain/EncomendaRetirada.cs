using System.Data;
using Chegou.Api.Models;
using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    public enum RetiradaStatus { Ok, NaoEncontrada, JaFinalizada, CodigoInvalido }

    public record RetiradaResultado(RetiradaStatus Status, RetiradaResponse? Resposta = null);

    public class EncomendaRetirada(IDbConnectionFactory connectionFactory)
    {
        private const string SqlBuscar = """
            SELECT Status, CodigoRetirada
            FROM Encomenda
            WHERE Id = @Id AND CondominioId = @CondominioId
            """;

        // O filtro por Status garante que duas retiradas simultâneas não passem as duas.
        private const string SqlRetirar = """
            UPDATE Encomenda
            SET Status = 'RETIRADA', RetiradaEm = @Quando, RetiradaPorNome = @Nome
            WHERE Id = @Id AND CondominioId = @CondominioId
              AND Status IN ('RECEBIDA', 'NOTIFICADA')
            """;

        public async Task<RetiradaResultado> ExecutarAsync(int condominioId, long id, RetirarRequest req, CancellationToken ct)
        {
            await using var connection = await connectionFactory.AbrirAsync(ct);

            string status;
            string? codigo;
            await using (var buscar = new SqlCommand(SqlBuscar, connection))
            {
                buscar.Par("@Id", SqlDbType.BigInt, id)
                      .Par("@CondominioId", SqlDbType.Int, condominioId);

                await using var reader = await buscar.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return new RetiradaResultado(RetiradaStatus.NaoEncontrada);
                status = reader.GetString(0);
                codigo = reader.GetStringOrNull(1);
            }

            if (status is "RETIRADA" or "DEVOLVIDA")
                return new RetiradaResultado(RetiradaStatus.JaFinalizada);

            if (codigo != req.Codigo)
                return new RetiradaResultado(RetiradaStatus.CodigoInvalido);

            await using var retirar = new SqlCommand(SqlRetirar, connection);
            retirar.Par("@Id", SqlDbType.BigInt, id)
                   .Par("@CondominioId", SqlDbType.Int, condominioId)
                   .Par("@Quando", SqlDbType.DateTime2, DateTime.UtcNow)
                   .Par("@Nome", SqlDbType.NVarChar, req.RetiradaPorNome, 150);

            var linhas = await retirar.ExecuteNonQueryAsync(ct);
            if (linhas == 0) return new RetiradaResultado(RetiradaStatus.JaFinalizada);

            return new RetiradaResultado(RetiradaStatus.Ok, new RetiradaResponse { Id = id, Status = "RETIRADA" });
        }
    }
}
