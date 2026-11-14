using System.Data;
using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    /// <summary>Atalhos para ADO.NET: parâmetros tipados (nunca concatenar valores no SQL) e leitura segura de nulos.</summary>
    public static class SqlExtensions
    {
        /// <summary>Adiciona um parâmetro. tamanho: tamanho do nvarchar (-1 = MAX). valor null vira DBNull.</summary>
        public static SqlCommand Par(this SqlCommand cmd, string nome, SqlDbType tipo, object? valor, int tamanho = 0)
        {
            var p = tamanho != 0 ? cmd.Parameters.Add(nome, tipo, tamanho) : cmd.Parameters.Add(nome, tipo);
            p.Value = valor ?? DBNull.Value;
            return cmd;
        }

        public static SqlCommand ParDecimal(this SqlCommand cmd, string nome, byte precisao, byte escala, decimal? valor)
        {
            var p = cmd.Parameters.Add(nome, SqlDbType.Decimal);
            p.Precision = precisao;
            p.Scale = escala;
            p.Value = valor.HasValue ? valor.Value : DBNull.Value;
            return cmd;
        }

        public static string? GetStringOrNull(this SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    }
}
