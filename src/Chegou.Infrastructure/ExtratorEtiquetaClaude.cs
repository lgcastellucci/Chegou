using System.Net.Http.Json;
using System.Text.Json;
using Chegou.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace Chegou.Infrastructure;

public class ClaudeOptions
{
    public string ApiKey { get; set; } = "";
    /// <summary>Teste também um modelo menor (mais barato) com suas fotos reais.</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";
}

public class ExtratorEtiquetaClaude(HttpClient http, IOptions<ClaudeOptions> opt) : IExtratorEtiqueta
{
    public async Task<EtiquetaExtraida> ExtrairAsync(byte[] foto, string mediaType, CancellationToken ct)
    {
        var body = new
        {
            model = opt.Value.Model,
            max_tokens = 600,
            system = PromptExtracao.Sistema,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image", source = new { type = "base64", media_type = mediaType, data = Convert.ToBase64String(foto) } },
                        new { type = "text", text = "Extraia os dados desta etiqueta." }
                    }
                }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages") { Content = JsonContent.Create(body) };
        req.Headers.Add("x-api-key", opt.Value.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var texto = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        texto = LimparCercas(texto);

        try
        {
            return JsonSerializer.Deserialize<EtiquetaExtraida>(texto)
                   ?? throw new InvalidOperationException("Resposta vazia do modelo.");
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
