using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Chegou.Api.Services
{
    /// <summary>
    /// Lê as configurações da tabela Configuracao (Chave/Valor), com cache de 5 minutos.
    /// Só a connection string continua no appsettings.json (é ela que dá acesso ao banco).
    /// Ordem de busca: variável de ambiente CHEGOU_{CHAVE} (":" vira "_", ex.: CHEGOU_GROQ_APIKEY) -> tabela -> valor padrão.
    /// </summary>
    public static class Configuracao
    {
        private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
        private static readonly ConcurrentDictionary<string, (string? Valor, DateTime Expira)> Cache = new();

        public static async Task<string> ObterAsync(string chave, string padrao, CancellationToken ct)
        {
            // A chave vem sempre do código (nunca do cliente), mas só aceitamos caracteres seguros por garantia.
            if (!Regex.IsMatch(chave, @"^[A-Za-z0-9:_.\-]+$"))
                throw new ArgumentException("Chave de configuração inválida.", nameof(chave));

            var ambiente = Environment.GetEnvironmentVariable("CHEGOU_" + chave.Replace(':', '_').ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(ambiente))
                return ambiente;

            if (!Cache.TryGetValue(chave, out var item) || item.Expira <= DateTime.UtcNow)
            {
                var sql = new StringBuilder();
                sql.Append(" SELECT Valor ");
                sql.Append(" FROM Configuracao ");
                sql.Append(" WHERE Chave = '" + chave + "'");

                var banco = new SqlExtensions();
                var dt = await banco.SelecionarAsync(sql.ToString(), ct);
                var valor = dt.Rows.Count == 0 ? null : dt.Rows[0]["Valor"]?.ToString();

                item = (valor, DateTime.UtcNow.Add(Validade));
                Cache[chave] = item;
            }

            return string.IsNullOrWhiteSpace(item.Valor) ? padrao : item.Valor;
        }

        /// <summary>Esquece o cache (a próxima leitura vai ao banco).</summary>
        public static void LimparCache() => Cache.Clear();
    }
}
