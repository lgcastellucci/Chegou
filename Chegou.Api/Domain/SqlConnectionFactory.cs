using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    public interface IDbConnectionFactory
    {
        /// <summary>Devolve uma conexão já aberta. Quem chama é responsável por descartá-la (await using).</summary>
        Task<SqlConnection> AbrirAsync(CancellationToken ct);
    }

    public class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public async Task<SqlConnection> AbrirAsync(CancellationToken ct)
        {
            var con = new SqlConnection(connectionString);
            try
            {
                await con.OpenAsync(ct);
                return con;
            }
            catch
            {
                await con.DisposeAsync();
                throw;
            }
        }
    }
}
