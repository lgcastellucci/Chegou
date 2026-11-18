using Chegou.Api.BancoDeDados;
using Chegou.Api.Services;
using System.Text;

namespace Chegou.Api.Controllers.Encomendas
{
    public class Retirar
    {
        private readonly string _condominioCodigo;

        public class Request
        {
            /// <summary>Código de retirada de 6 dígitos.</summary>
            public string codigo { get; set; }

            /// <summary>Nome de quem retirou.</summary>
            public string retiradaPorNome { get; set; }
        }

        public class Response
        {
            public long idEncomenda { get; set; }
            public string? status { get; set; }

            public Response()
            {
                idEncomenda = 0;
                status = string.Empty;
            }
        }

        public Retirar(string condominioCodigo)
        {
            _condominioCodigo = condominioCodigo;
        }

        public async Task<(string erro, Response response)> Executar(long idEncomenda, Request request, CancellationToken ct)
        {
            var resposta = new Response();

            if (string.IsNullOrWhiteSpace(_condominioCodigo))
                return ("Condominio Codigo não informado.", resposta);

            var condominioId = await AcessoAoBancoDeDados.DevolveCondominioId(_condominioCodigo, ct);

            if (string.IsNullOrWhiteSpace(condominioId))
                return ("Condominio não encontrado", resposta);

            var sql = new StringBuilder();
            var banco = new SqlExtensions();

            sql.Clear();
            sql.Append(" SELECT Status, CodigoRetirada ");
            sql.Append(" FROM Encomenda");
            sql.Append(" WHERE Id = " + idEncomenda + " AND CondominioId = " + condominioId);
            sql.Append(" ORDER BY RecebidaEm DESC");

            var dt = await banco.SelecionarAsync(sql.ToString(), ct);
            if (dt.Rows.Count == 0)
                return ("Encomenda não encontrada", resposta);

            var status = dt.Rows[0]["Status"].ToString();
            var codigoRetirada = dt.Rows[0]["CodigoRetirada"].ToString();

            if (status is "RETIRADA" or "DEVOLVIDA")
                return ("Encomenda já retirada ou devolvida", resposta);

            if (codigoRetirada != request.codigo)
                return ("Codigo de retirada inválido", resposta);

            sql.Clear();
            sql.Append(" UPDATE Encomenda ");
            sql.Append(" SET Status = 'RETIRADA', RetiradaEm = GETDATE(), RetiradaPorNome = '" + request.retiradaPorNome + "'");
            sql.Append(" WHERE Id = " + idEncomenda + " AND CondominioId = " + condominioId);
            sql.Append("       AND Status IN ('RECEBIDA', 'NOTIFICADA') ");

            var linhasAfetadas = await banco.ExecutarAsync(sql.ToString(), ct);
            if (linhasAfetadas == 0)
                return ("Encomenda já retirada ou devolvida", resposta);

            resposta.idEncomenda = idEncomenda;
            resposta.status = "RETIRADA";

            return ("", resposta);
        }
    }
}
