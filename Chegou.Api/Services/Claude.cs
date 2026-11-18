using Chegou.Api.Models;
using System.Text.Json;

namespace Chegou.Api.Services
{
    public class Claude
    {
        private static string _apiKey;
        private static string _model;

        public Claude()
        {
            var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
            _apiKey = configuration.GetSection("Claude")?.GetValue<string>("ApiKey") ?? "";
            _model = configuration.GetSection("Claude")?.GetValue<string>("Model") ?? "";
        }

        public async Task<EtiquetaExtraida> ExtrairEtiquetaAsync(string imgBase64, string mediaType, CancellationToken ct)
        {
            var body = new
            {
                model = _model,
                max_tokens = 600,
                system = PromptExtracao.Sistema,
                messages = new[]
                          {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "image", source = new { type = "base64", media_type = mediaType, data =imgBase64 } },
                            new { type = "text", text = "Extraia os dados desta etiqueta." }
                        }
                    }
                }
            };

            using var http = new HttpClient();
            http.BaseAddress = new Uri("https://api.anthropic.com/");
            http.Timeout = TimeSpan.FromSeconds(60);

            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages") { Content = JsonContent.Create(body) };
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var requestId = resp.Headers.TryGetValues("request-id", out var ids) ? ids.FirstOrDefault() : null;
                string? tipo = null;
                string? mensagem = null;
                try
                {
                    using var erro = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (erro.RootElement.ValueKind == JsonValueKind.Object &&
                        erro.RootElement.TryGetProperty("error", out var detalhe) && detalhe.ValueKind == JsonValueKind.Object)
                    {
                        if (detalhe.TryGetProperty("type", out var tipoJson) && tipoJson.ValueKind == JsonValueKind.String)
                            tipo = tipoJson.GetString();
                        if (detalhe.TryGetProperty("message", out var mensagemJson) && mensagemJson.ValueKind == JsonValueKind.String)
                            mensagem = mensagemJson.GetString();
                    }
                }
                catch (JsonException)
                {
                    mensagem = "Resposta de erro não é um JSON válido.";
                }

                mensagem = $"Claude recusou a requisição. HTTP {(int)resp.StatusCode}; request-id {requestId}; tipo {tipo}; mensagem {mensagem ?? "Mensagem de erro indisponível."}";
                resp.EnsureSuccessStatusCode();
            }

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var texto = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            texto = LimparCercas(texto);

            try
            {
                return JsonSerializer.Deserialize<EtiquetaExtraida>(texto);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("O modelo não devolveu um JSON válido.", ex);
            }

        }

        // Segurança: se o modelo vier com ```json ... ```, remove as cercas.
        private static string LimparCercas(string s)
        {
            s = s.Trim();
            if (!s.StartsWith("```")) return s;
            var ini = s.IndexOf('\n');
            var fim = s.LastIndexOf("```", StringComparison.Ordinal);
            return ini >= 0 && fim > ini ? s[(ini + 1)..fim].Trim() : s;
        }

    }
}
