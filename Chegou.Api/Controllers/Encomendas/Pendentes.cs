using Chegou.Api.BancoDeDados;
using Chegou.Api.Models;
using Chegou.Api.Services;
using System.Data;
using System.Text;

namespace Chegou.Api.Controllers.Encomendas
{
    public class Pendentes
    {
        private readonly string _condominioCodigo;

        public Pendentes(string condominioCodigo)
        {
            _condominioCodigo = condominioCodigo;
        }

        public async Task<(string erro , List<Encomenda> encomendas)> Executar(CancellationToken ct)
        {
            var encomendasPendentes = new List<Encomenda>();

            if (string.IsNullOrWhiteSpace(_condominioCodigo))
                return ("Condominio Codigo não informado.", encomendasPendentes);

            var condominioId = await AcessoAoBancoDeDados.DevolveCondominioId(_condominioCodigo, ct);

            if (string.IsNullOrWhiteSpace(condominioId))
                return ("Condominio não encontrado", encomendasPendentes);

            var sql = new StringBuilder();
            sql.Append(" SELECT e.Id, e.Status, e.Transportadora, e.Rastreio, e.RecebidaEm, ");
            sql.Append("        m.Nome AS Morador, u.Bloco, u.Numero ");
            sql.Append(" FROM Encomenda e ");
            sql.Append(" LEFT JOIN Morador m ON m.Id = e.MoradorId ");
            sql.Append(" LEFT JOIN Unidade u ON u.Id = e.UnidadeId ");
            sql.Append(" WHERE e.CondominioId = " + condominioId);
            sql.Append("       AND e.Status IN ('RECEBIDA', 'NOTIFICADA')");
            sql.Append(" ORDER BY e.RecebidaEm DESC");


            //Seleciona no banco de dados as encomendas pendentes e retorna para o front-end
            var banco = new SqlExtensions();
            var dataTablePendentes = await banco.SelecionarAsync(sql.ToString(), ct);

            foreach (DataRow row in dataTablePendentes.Rows)
            {
                var encomenda = new Encomenda
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Status = row["Status"].ToString(),
                    Transportadora = row["Transportadora"].ToString(),
                    Rastreio = row["Rastreio"].ToString(),
                    RecebidaEm = Convert.ToDateTime(row["RecebidaEm"]),
                    Morador = row["Morador"].ToString(),
                    Bloco = row["Bloco"].ToString(),
                    Numero = row["Numero"].ToString()
                };
                encomendasPendentes.Add(encomenda);
            }

            return ("", encomendasPendentes);
        }
    }
}
