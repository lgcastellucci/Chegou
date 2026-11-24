using Chegou.Api.Models;
using System.Text.Json;

namespace Chegou.Api.Services
{
    public class Groq
    {
        private const string ModeloPadrao = "qwen/qwen3.8-27b";

        public async Task<EtiquetaExtraida> ExtrairEtiquetaAsync(string imgBase64, string mediaType, CancellationToken ct)
        {
            // Configurações vêm da tabela Configuracao (ou da variável de ambiente CHEGOU_GROQ_APIKEY / CHEGOU_GROQ_MODEL)
            var apiKey = await Configuracao.ObterAsync("Groq:ApiKey", "", ct);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Groq:ApiKey não configurada na tabela Configuracao.");
            var modelo = await Configuracao.ObterAsync("Groq:Model", ModeloPadrao, ct);

            var body = new
            {
                model = modelo,
                max_completion_tokens = 600,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = PromptExtracao.Sistema },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Extraia os dados desta etiqueta." },
                            new { type = "image_url", image_url = new { url = $"data:{mediaType};base64,{imgBase64}" } }
                        }
                    }
                }
            };

            using var http = new HttpClient();
            http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            http.Timeout = TimeSpan.FromSeconds(60);

            using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = JsonContent.Create(body) };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var requestId = resp.Headers.TryGetValues("x-request-id", out var ids)
                    ? ids.FirstOrDefault()
                    : resp.Headers.TryGetValues("request-id", out ids) ? ids.FirstOrDefault() : null;
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

                throw new HttpRequestException(
                    $"Groq recusou a requisição. HTTP {(int)resp.StatusCode}; request-id {requestId}; tipo {tipo}; mensagem {mensagem ?? "Mensagem de erro indisponível."}",
                    null,
                    resp.StatusCode);
            }

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("A resposta da Groq não contém o texto esperado.");

            var texto = LimparCercas(content.GetString() ?? "");

            try
            {
                return JsonSerializer.Deserialize<EtiquetaExtraida>(texto)
                    ?? throw new InvalidOperationException("A Groq não devolveu dados da etiqueta.");
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
