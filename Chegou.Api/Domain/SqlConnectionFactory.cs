using Microsoft.Data.SqlClient;

namespace Chegou.Api.Domain
{
    public interface IDbConnectionFactory
    {
        /// <summary>Devolve uma conexão já aberta. Quem chama é responsável por descartá-la (using).</summary>
        SqlConnection Abrir();
    }

    public class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public SqlConnection Abrir()
        {
            var con = new SqlConnection(connectionString);
            try
            {
                con.Open();
                return con;
            }
            catch
            {
                con.Dispose();
                throw;
            }
        }
    }

}
