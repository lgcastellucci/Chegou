using Chegou.Api.BancoDeDados;
using Chegou.Api.Models;
using Chegou.Api.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chegou.Api.Controllers.Encomendas
{
    public class Registrar
    {
        private readonly string _condominioCodigo;

        public class Request
        {
            public string? fotoPath { get; set; }
            public EtiquetaExtraida etiqueta { get; set; }
            public int moradorId { get; set; }
            public int? moradorSugeridoId { get; set; }
            public double? scoreMatch { get; set; }
        }

        public class Response
        {
            public long idEncomenda { get; set; }
            public string status { get; set; }
            public string codigoRetirada { get; set; }

            public Response()
            {
                idEncomenda = 0;
                status = string.Empty;
                codigoRetirada = string.Empty;
            }
        }

        public Registrar(string condominioCodigo)
        {
            _condominioCodigo = condominioCodigo;
        }

        public async Task<(string erro, Response response)> Executar(Request request, CancellationToken ct)
        {
            var resposta = new Response();

            if (string.IsNullOrWhiteSpace(_condominioCodigo))
                return ("Condominio Codigo não informado.", resposta);

            var condominioId = await AcessoAoBancoDeDados.DevolveCondominioId(_condominioCodigo, ct);

            if (string.IsNullOrWhiteSpace(condominioId))
                return ("Condominio não encontrado", resposta);


            var morador = await AcessoAoBancoDeDados.BuscarMorador(condominioId, request.moradorId.ToString(), ct);
            if (morador is null)
                return ("Morador não encontrado", resposta);

            // O porteiro escolheu alguém diferente do sugerido? Então aprendemos o nome da etiqueta.
            var manual = request.moradorSugeridoId != request.moradorId;
            string? novoApelido = null;
            if (manual)
            {
                var apelido = TextoNormalizador.Normalizar(request.etiqueta.Destinatario);
                if (apelido.Length > 0 && apelido != morador.NomeNorm && !morador.ApelidosNorm.Contains(apelido))
                    novoApelido = apelido;
            }

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            long idEncomenda = 0;


            var sql = new StringBuilder();
            var banco = new SqlExtensions();
            var lTransacao = await banco.BeginTransactionAsync(ct);
            if (!lTransacao)
                return ("Não foi possível iniciar a transação", resposta);

            sql.Clear();
            sql.Append(" INSERT INTO Encomenda ");
            sql.Append(" (CondominioId, UnidadeId, MoradorId, FotoPath, DadosExtraidosJson, ");
            sql.Append("  DestinatarioLido, BlocoLido, NumeroLido, Transportadora, Rastreio,");
            sql.Append("  Confianca, ScoreMatch, MatchManual, Status, RecebidaEm, CodigoRetirada)");
            sql.Append(" VALUES ");
            sql.Append(" ( ");
            sql.Append(condominioId + ", ");
            sql.Append(morador.UnidadeId + ", ");
            sql.Append(morador.Id + ", ");
            sql.Append("'" + request.fotoPath + "', ");
            sql.Append("'" + JsonSerializer.Serialize(request.etiqueta) + "', ");
            sql.Append("'" + request.etiqueta.Destinatario + "', ");
            sql.Append("'" + request.etiqueta.Bloco + "', ");
            sql.Append("'" + request.etiqueta.Apartamento + "', ");
            sql.Append("'" + request.etiqueta.Transportadora + "', ");
            sql.Append("'" + request.etiqueta.Rastreio + "', ");
            sql.Append(request.etiqueta.Confianca.ToString("F2").Replace(",", ".") + ", "); // 3 casas, 2 decimais
            sql.Append((request.scoreMatch is null ? "NULL" : request.scoreMatch.Value.ToString("F2").Replace(",", ".")) + ", "); // 3 casas, 2 decimais
            sql.Append((manual ? 1 : 0) + ", ");
            sql.Append("'RECEBIDA'" + ", ");
            sql.Append("GETDATE()" + ", ");
            sql.Append("'" + codigo + "'");
            sql.Append(" ) ");

            await banco.ExecutartransactionAsync(sql.ToString(), ct);

            // Recupera o ID da encomenda recém inserida
            sql.Clear();
            sql.Append(" SELECT SCOPE_IDENTITY() ");
            var dtIdentity = await banco.SelecionartransactionAsync(sql.ToString(), ct);
            if (dtIdentity.Rows.Count > 0)
            {
                idEncomenda = Convert.ToInt64(dtIdentity.Rows[0][0]);
            }

            if (novoApelido is not null)
            {
                sql.Clear();
                sql.Append(" IF NOT EXISTS(SELECT 1 FROM MoradorApelido WHERE MoradorId = " + morador.Id + " AND ApelidoNorm = '" + novoApelido + "') ");
                sql.Append("   INSERT INTO MoradorApelido(MoradorId, ApelidoNorm, Origem) VALUES(@" + morador.Id + ", '" + novoApelido + "', 'CORRECAO') ");

                await banco.ExecutartransactionAsync(sql.ToString(), ct);
            }
            banco.CommitAsync(ct);


            var status = "RECEBIDA";
            if (morador.AceitaNotificacao)
            {
                //Envia o push notification para o morador
                //await notificador.NotificarAsync(morador, id, codigo, ct);

                sql.Clear();
                sql.Append(" UPDATE Encomenda SET Status = 'NOTIFICADA', NotificadaEm = GETDATE() WHERE Id = " + idEncomenda);
                await banco.ExecutarAsync(sql.ToString(), ct);
                status = "NOTIFICADA";
            }

            resposta.idEncomenda = idEncomenda;
            resposta.status = status;
            resposta.codigoRetirada = codigo;

            return ("", resposta);
        }
    }
}
