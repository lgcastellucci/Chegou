using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Chegou.Api.Middleware
{
    public class ApiRequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _connectionString;
        private readonly ILogger<ApiRequestLoggingMiddleware> _logger;

        public ApiRequestLoggingMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiRequestLoggingMiddleware> logger)
        {
            _next = next;
            _connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var inicio = Stopwatch.GetTimestamp();
            var headersRequest = SerializarHeaders(context.Request.Headers);
            context.Request.EnableBuffering();
            var respostaOriginal = context.Response.Body;
            using var respostaCapturada = new ResponseCaptureStream(respostaOriginal);
            context.Response.Body = respostaCapturada;

            try
            {
                await _next(context);
            }
            catch
            {
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                throw;
            }
            finally
            {
                context.Response.Body = respostaOriginal;
                var duracaoMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
                string? request = null;
                try
                {
                    context.Request.Body.Position = 0;
                    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                    request = await reader.ReadToEndAsync(context.RequestAborted);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                {
                    _logger.LogWarning(ex, "Não foi possível capturar o corpo da requisição para o log da API.");
                }

                try
                {
                    await RegistrarAsync(context, duracaoMs, headersRequest,
                        SerializarHeaders(context.Response.Headers), request, respostaCapturada.GetBody());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Não foi possível registrar a chamada da API no banco de dados.");
                }
            }
        }

        private static string SerializarHeaders(IHeaderDictionary headers)
        {
            var protegidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "X-Condominio-Id"
            };
            return JsonSerializer.Serialize(headers.ToDictionary(
                header => header.Key,
                header => protegidos.Contains(header.Key) ? new[] { "[REDACTED]" } : header.Value.ToArray()));
        }

        private async Task RegistrarAsync(HttpContext context, long duracaoMs, string headersRequest, string headersResponse, string? request, string response)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(CancellationToken.None);

            const string sql = """
                INSERT INTO ApiLog
                    (RequestId, Metodo, Caminho, HeadersRequest, RequestBody, HeadersResponse, ResponseBody, StatusCode, DuracaoMs)
                VALUES
                    (@RequestId, @Metodo, @Caminho, @HeadersRequest, @RequestBody, @HeadersResponse, @ResponseBody, @StatusCode, @DuracaoMs)
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@RequestId", SqlDbType.NVarChar, 256).Value = Limitar(context.TraceIdentifier, 256);
            command.Parameters.Add("@Metodo", SqlDbType.NVarChar, 20).Value = Limitar(context.Request.Method, 20);
            command.Parameters.Add("@Caminho", SqlDbType.NVarChar, 2048).Value = Limitar(context.Request.Path.Value ?? "/", 2048);
            command.Parameters.Add("@HeadersRequest", SqlDbType.NVarChar, -1).Value = headersRequest;
            command.Parameters.Add("@RequestBody", SqlDbType.NVarChar, -1).Value = (object?)request ?? DBNull.Value;
            command.Parameters.Add("@HeadersResponse", SqlDbType.NVarChar, -1).Value = headersResponse;
            command.Parameters.Add("@ResponseBody", SqlDbType.NVarChar, -1).Value = response;
            command.Parameters.Add("@StatusCode", SqlDbType.Int).Value = context.Response.StatusCode;
            command.Parameters.Add("@DuracaoMs", SqlDbType.BigInt).Value = duracaoMs;

            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        private static string Limitar(string valor, int tamanho) => valor.Length <= tamanho ? valor : valor[..tamanho];
    }
}
