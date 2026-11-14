using System.Data;
using Chegou.Api.Domain.Integracoes;
using Chegou.Api.Domain.Matching;
using Chegou.Api.Models;
using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    public record ExtrairResultado(ExtrairEtiquetaResponse? Sucesso, ErroLeituraResponse? Falha);

    public class EncomendaExtrair(IDbConnectionFactory connectionFactory, IExtratorEtiqueta extrator, IFotoStorage storage)
    {
        // Até 2 linhas: só precisamos saber se existe exatamente uma unidade com esse número/bloco.
        private const string SqlUnidade = """
            SELECT TOP 2 Id
            FROM Unidade
            WHERE CondominioId = @CondominioId
              AND NumeroNorm = @NumeroNorm
              AND (@BlocoNorm = '' OR BlocoNorm = @BlocoNorm)
            """;

        private const string SqlMoradores = """
            SELECT Id, UnidadeId, Nome, NomeNorm
            FROM Morador
            WHERE CondominioId = @CondominioId
              AND Ativo = 1
              AND (@UnidadeId IS NULL OR UnidadeId = @UnidadeId)
            """;

        private const string SqlApelidos = """
            SELECT a.MoradorId, a.ApelidoNorm
            FROM MoradorApelido a
            JOIN Morador m ON m.Id = a.MoradorId
            WHERE m.CondominioId = @CondominioId
              AND m.Ativo = 1
              AND (@UnidadeId IS NULL OR m.UnidadeId = @UnidadeId)
            """;

        public async Task<ExtrairResultado> ExecutarAsync(int condominioId, byte[] foto, string? mediaType, CancellationToken ct)
        {
            var fotoPath = await storage.SalvarAsync(condominioId, foto, ct);

            EtiquetaExtraida etiqueta;
            try
            {
                etiqueta = await extrator.ExtrairAsync(foto, mediaType ?? "image/jpeg", ct);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                return new ExtrairResultado(null,
                    new ErroLeituraResponse("Não foi possível ler a etiqueta. Preencha manualmente.", fotoPath));
            }

            var blocoNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Bloco);
            var numeroNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Apartamento);

            await using var connection = await connectionFactory.AbrirAsync(ct);

            int? unidadeId = numeroNorm.Length > 0
                ? await BuscarUnidadeAsync(connection, condominioId, numeroNorm, blocoNorm, ct)
                : null;

            var moradores = await ListarMoradoresAsync(connection, condominioId, unidadeId, ct);
            var resultado = MatchService.Avaliar(etiqueta.Destinatario, moradores, unidadeId != null);

            return new ExtrairResultado(new ExtrairEtiquetaResponse(
                fotoPath, etiqueta, unidadeId, resultado.Nivel.ToString(), resultado.Candidatos), null);
        }

        private static async Task<int?> BuscarUnidadeAsync(
            SqlConnection connection, int condominioId, string numeroNorm, string blocoNorm, CancellationToken ct)
        {
            await using var command = new SqlCommand(SqlUnidade, connection);
            command.Par("@CondominioId", SqlDbType.Int, condominioId)
                   .Par("@NumeroNorm", SqlDbType.NVarChar, numeroNorm, 20)
                   .Par("@BlocoNorm", SqlDbType.NVarChar, blocoNorm, 20);

            await using var reader = await command.ExecuteReaderAsync(ct);
            var ids = new List<int>();
            while (await reader.ReadAsync(ct)) ids.Add(reader.GetInt32(0));
            return ids.Count == 1 ? ids[0] : null;   // ambígua ou inexistente: segue sem unidade confirmada
        }

        private static async Task<List<MoradorBusca>> ListarMoradoresAsync(
            SqlConnection connection, int condominioId, int? unidadeId, CancellationToken ct)
        {
            var moradores = new List<(int Id, int UnidadeId, string Nome, string NomeNorm)>();

            await using (var command = new SqlCommand(SqlMoradores, connection))
            {
                command.Par("@CondominioId", SqlDbType.Int, condominioId)
                       .Par("@UnidadeId", SqlDbType.Int, unidadeId);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    moradores.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));
            }

            var apelidos = new Dictionary<int, List<string>>();
            await using (var command = new SqlCommand(SqlApelidos, connection))
            {
                command.Par("@CondominioId", SqlDbType.Int, condominioId)
                       .Par("@UnidadeId", SqlDbType.Int, unidadeId);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var moradorId = reader.GetInt32(0);
                    if (!apelidos.TryGetValue(moradorId, out var lista)) apelidos[moradorId] = lista = new List<string>();
                    lista.Add(reader.GetString(1));
                }
            }

            return moradores
                .Select(m => new MoradorBusca(m.Id, m.UnidadeId, m.Nome, m.NomeNorm,
                    apelidos.TryGetValue(m.Id, out var a) ? a : new List<string>()))
                .ToList();
        }
    }
}
