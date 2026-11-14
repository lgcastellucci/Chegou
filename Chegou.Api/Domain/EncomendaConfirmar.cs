using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Chegou.Api.Domain.Integracoes;
using Chegou.Api.Domain.Matching;
using Chegou.Api.Models;
using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    /// <summary>Resposta null = morador não encontrado.</summary>
    public record ConfirmarResultado(ConfirmarEncomendaResponse? Resposta);

    public class EncomendaConfirmar(IDbConnectionFactory connectionFactory, INotificador notificador)
    {
        private const string SqlMorador = """
            SELECT Id, UnidadeId, Nome, NomeNorm, AceitaNotificacao
            FROM Morador
            WHERE Id = @Id AND CondominioId = @CondominioId
            """;

        private const string SqlApelidosDoMorador = """
            SELECT ApelidoNorm FROM MoradorApelido WHERE MoradorId = @MoradorId
            """;

        private const string SqlInserir = """
            INSERT INTO Encomenda
                (CondominioId, UnidadeId, MoradorId, FotoPath, DadosExtraidosJson,
                 DestinatarioLido, BlocoLido, NumeroLido, Transportadora, Rastreio,
                 Confianca, ScoreMatch, MatchManual, Status, RecebidaEm,
                 RecebidaPorUsuarioId, CodigoRetirada)
            OUTPUT INSERTED.Id
            VALUES
                (@CondominioId, @UnidadeId, @MoradorId, @FotoPath, @DadosExtraidosJson,
                 @DestinatarioLido, @BlocoLido, @NumeroLido, @Transportadora, @Rastreio,
                 @Confianca, @ScoreMatch, @MatchManual, 'RECEBIDA', @RecebidaEm,
                 @RecebidaPorUsuarioId, @CodigoRetirada)
            """;

        private const string SqlNovoApelido = """
            IF NOT EXISTS (SELECT 1 FROM MoradorApelido WHERE MoradorId = @MoradorId AND ApelidoNorm = @Apelido)
                INSERT INTO MoradorApelido (MoradorId, ApelidoNorm, Origem) VALUES (@MoradorId, @Apelido, 'CORRECAO')
            """;

        private const string SqlNotificada = """
            UPDATE Encomenda SET Status = 'NOTIFICADA', NotificadaEm = @Quando WHERE Id = @Id
            """;

        public async Task<ConfirmarResultado> ExecutarAsync(
            int condominioId, int usuarioId, ConfirmarEncomendaRequest req, CancellationToken ct)
        {
            await using var connection = await connectionFactory.AbrirAsync(ct);

            var morador = await ObterMoradorAsync(connection, condominioId, req.MoradorId, ct);
            if (morador is null) return new ConfirmarResultado(null);

            // O porteiro escolheu alguém diferente do sugerido? Então aprendemos o nome da etiqueta.
            var manual = req.MoradorSugeridoId != req.MoradorId;
            string? novoApelido = null;
            if (manual)
            {
                var apelido = TextoNormalizador.Normalizar(req.Etiqueta.Destinatario);
                if (apelido.Length > 0 && apelido != morador.NomeNorm && !morador.ApelidosNorm.Contains(apelido))
                    novoApelido = apelido;
            }

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            long id;

            // Encomenda + apelido aprendido: tudo ou nada.
            await using (var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct))
            {
                await using (var inserir = new SqlCommand(SqlInserir, connection, tx))
                {
                    inserir.Par("@CondominioId", SqlDbType.Int, condominioId)
                           .Par("@UnidadeId", SqlDbType.Int, morador.UnidadeId)
                           .Par("@MoradorId", SqlDbType.Int, morador.Id)
                           .Par("@FotoPath", SqlDbType.NVarChar, req.FotoPath, 300)
                           .Par("@DadosExtraidosJson", SqlDbType.NVarChar, JsonSerializer.Serialize(req.Etiqueta), -1)
                           .Par("@DestinatarioLido", SqlDbType.NVarChar, req.Etiqueta.Destinatario, 150)
                           .Par("@BlocoLido", SqlDbType.NVarChar, req.Etiqueta.Bloco, 20)
                           .Par("@NumeroLido", SqlDbType.NVarChar, req.Etiqueta.Apartamento, 20)
                           .Par("@Transportadora", SqlDbType.NVarChar, req.Etiqueta.Transportadora, 50)
                           .Par("@Rastreio", SqlDbType.NVarChar, req.Etiqueta.Rastreio, 60)
                           .ParDecimal("@Confianca", 3, 2, (decimal)req.Etiqueta.Confianca)
                           .ParDecimal("@ScoreMatch", 3, 2, req.ScoreMatch is null ? null : (decimal)req.ScoreMatch.Value)
                           .Par("@MatchManual", SqlDbType.Bit, manual)
                           .Par("@RecebidaEm", SqlDbType.DateTime2, DateTime.UtcNow)
                           .Par("@RecebidaPorUsuarioId", SqlDbType.Int, usuarioId)
                           .Par("@CodigoRetirada", SqlDbType.Char, codigo, 6);

                    id = (long)(await inserir.ExecuteScalarAsync(ct))!;
                }

                if (novoApelido is not null)
                {
                    await using var apelidoCmd = new SqlCommand(SqlNovoApelido, connection, tx);
                    apelidoCmd.Par("@MoradorId", SqlDbType.Int, morador.Id)
                              .Par("@Apelido", SqlDbType.NVarChar, novoApelido, 150);
                    await apelidoCmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }

            var status = "RECEBIDA";
            if (morador.AceitaNotificacao)
            {
                await notificador.NotificarAsync(morador, id, codigo, ct);

                await using var notificada = new SqlCommand(SqlNotificada, connection);
                notificada.Par("@Id", SqlDbType.BigInt, id)
                          .Par("@Quando", SqlDbType.DateTime2, DateTime.UtcNow);
                await notificada.ExecuteNonQueryAsync(ct);
                status = "NOTIFICADA";
            }

            return new ConfirmarResultado(new ConfirmarEncomendaResponse(id, status, codigo));
        }

        private static async Task<Morador?> ObterMoradorAsync(
            SqlConnection connection, int condominioId, int moradorId, CancellationToken ct)
        {
            Morador morador;

            await using (var command = new SqlCommand(SqlMorador, connection))
            {
                command.Par("@Id", SqlDbType.Int, moradorId)
                       .Par("@CondominioId", SqlDbType.Int, condominioId);

                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return null;

                morador = new Morador
                {
                    Id = reader.GetInt32(0),
                    UnidadeId = reader.GetInt32(1),
                    Nome = reader.GetString(2),
                    NomeNorm = reader.GetString(3),
                    AceitaNotificacao = reader.GetBoolean(4)
                };
            }

            await using (var command = new SqlCommand(SqlApelidosDoMorador, connection))
            {
                command.Par("@MoradorId", SqlDbType.Int, moradorId);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) morador.ApelidosNorm.Add(reader.GetString(0));
            }

            return morador;
        }
    }
}
