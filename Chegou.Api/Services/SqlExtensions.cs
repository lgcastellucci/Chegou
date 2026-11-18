using Microsoft.Data.SqlClient;
using System.Data;

namespace Chegou.Api.Services
{
    public class SqlExtensions
    {
        private readonly string _connectionString;
        private SqlConnection _conTransaction;
        private SqlTransaction _transaction;
        public SqlExtensions()
        {
            var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
            _connectionString = configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");
        }

        private async Task<SqlConnection> ConectarAsync(CancellationToken ct)
        {
            var con = new SqlConnection(_connectionString);
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

        public async Task<DataTable> SelecionarAsync(string query, CancellationToken ct)
        {
            using var con = await ConectarAsync(ct);
            using var cmd = new SqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var dataTable = new DataTable();
            dataTable.Load(reader);
            return dataTable;
        }

        public async Task<int> ExecutarAsync(string query, CancellationToken ct)
        {
            using var con = await ConectarAsync(ct);
            using var cmd = new SqlCommand(query, con);
            return await cmd.ExecuteNonQueryAsync(ct);
        }


        public async Task<bool> BeginTransactionAsync(CancellationToken ct)
        {
            _conTransaction = await ConectarAsync(ct);
            try
            {
                _transaction = _conTransaction.BeginTransaction();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CommitAsync(CancellationToken ct)
        {
            try
            {
                _transaction.Commit();
                await _conTransaction.DisposeAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RollbackAsync(CancellationToken ct)
        {
            try
            {
                _transaction.Rollback();
                await _conTransaction.DisposeAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        //Execução de query dentro de uma transação
        public async Task<int> ExecutartransactionAsync(string query, CancellationToken ct)
        {
            using var cmd = new SqlCommand(query, _conTransaction, _transaction);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        //Selecionar dados dentro de uma transação
        public async Task<DataTable> SelecionartransactionAsync(string query, CancellationToken ct)
        {
            using var cmd = new SqlCommand(query, _conTransaction, _transaction);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var dataTable = new DataTable();
            dataTable.Load(reader);
            return dataTable;
        }
    }
}
