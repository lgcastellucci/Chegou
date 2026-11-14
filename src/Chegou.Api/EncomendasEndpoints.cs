using System.Security.Cryptography;
using System.Text.Json;
using Chegou.Domain.Abstractions;
using Chegou.Domain.Entities;
using Chegou.Domain.Matching;
using Chegou.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Chegou.Api;

public record ConfirmarEncomendaRequest(
    string? FotoPath, EtiquetaExtraida Etiqueta, int MoradorId, int? MoradorSugeridoId, double? ScoreMatch);

public record RetirarRequest(string Codigo, string RetiradaPorNome);

public static class EncomendasEndpoints
{
    // PROVISÓRIO: até ter login (JWT), condomínio e usuário vêm por header.
    private static int Cond(HttpContext h) => int.Parse(h.Request.Headers["X-Condominio-Id"].FirstOrDefault() ?? "1");
    private static int Usr(HttpContext h) => int.Parse(h.Request.Headers["X-Usuario-Id"].FirstOrDefault() ?? "1");

    public static void MapEncomendas(this WebApplication app)
    {
        var g = app.MapGroup("/api/encomendas");

        // 1) Foto -> extração -> candidatos (não grava encomenda ainda)
        g.MapPost("/extrair", async (HttpContext http, IFormFile foto, IExtratorEtiqueta extrator,
            IFotoStorage storage, ChegouDbContext db, CancellationToken ct) =>
        {
            var cid = Cond(http);
            using var ms = new MemoryStream();
            await foto.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            var fotoPath = await storage.SalvarAsync(cid, bytes, ct);

            EtiquetaExtraida etiqueta;
            try
            {
                etiqueta = await extrator.ExtrairAsync(bytes, foto.ContentType ?? "image/jpeg", ct);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                return Results.UnprocessableEntity(new { erro = "Não foi possível ler a etiqueta. Preencha manualmente.", fotoPath });
            }

            var blocoNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Bloco);
            var numNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Apartamento);

            Unidade? unidade = null;
            if (numNorm.Length > 0)
            {
                var achadas = await db.Unidades
                    .Where(u => u.CondominioId == cid && u.NumeroNorm == numNorm
                                && (blocoNorm == "" || u.BlocoNorm == blocoNorm))
                    .ToListAsync(ct);
                if (achadas.Count == 1) unidade = achadas[0];
            }

            var q = db.Moradores.Include(m => m.Apelidos).Where(m => m.CondominioId == cid && m.Ativo);
            if (unidade != null) q = q.Where(m => m.UnidadeId == unidade.Id);
            var moradores = await q.ToListAsync(ct);

            var buscas = moradores
                .Select(m => new MoradorBusca(m.Id, m.UnidadeId, m.Nome, m.NomeNorm,
                    m.Apelidos.Select(a => a.ApelidoNorm).ToList()))
                .ToList();

            var resultado = MatchService.Avaliar(etiqueta.Destinatario, buscas, unidade != null);

            return Results.Ok(new
            {
                fotoPath,
                etiqueta,
                unidadeId = unidade?.Id,
                nivel = resultado.Nivel.ToString(),
                candidatos = resultado.Candidatos
            });
        }).DisableAntiforgery();

        // 2) Porteiro confirma -> grava, notifica, aprende apelido
        g.MapPost("/", async (HttpContext http, ConfirmarEncomendaRequest req, ChegouDbContext db,
            INotificador notificador, CancellationToken ct) =>
        {
            var cid = Cond(http);
            var morador = await db.Moradores.Include(m => m.Apelidos)
                .FirstOrDefaultAsync(m => m.Id == req.MoradorId && m.CondominioId == cid, ct);
            if (morador is null) return Results.NotFound(new { erro = "Morador não encontrado." });

            var manual = req.MoradorSugeridoId != req.MoradorId;
            var enc = new Encomenda
            {
                CondominioId = cid,
                UnidadeId = morador.UnidadeId,
                MoradorId = morador.Id,
                FotoPath = req.FotoPath,
                DadosExtraidosJson = JsonSerializer.Serialize(req.Etiqueta),
                DestinatarioLido = req.Etiqueta.Destinatario,
                BlocoLido = req.Etiqueta.Bloco,
                NumeroLido = req.Etiqueta.Apartamento,
                Transportadora = req.Etiqueta.Transportadora,
                Rastreio = req.Etiqueta.Rastreio,
                Confianca = (decimal)req.Etiqueta.Confianca,
                ScoreMatch = req.ScoreMatch is null ? null : (decimal)req.ScoreMatch.Value,
                MatchManual = manual,
                RecebidaPorUsuarioId = Usr(http),
                CodigoRetirada = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")
            };

            // aprendizado: o nome da etiqueta vira apelido do morador escolhido
            if (manual)
            {
                var apelido = TextoNormalizador.Normalizar(req.Etiqueta.Destinatario);
                if (apelido.Length > 0 && apelido != morador.NomeNorm
                    && !morador.Apelidos.Any(a => a.ApelidoNorm == apelido))
                    morador.Apelidos.Add(new MoradorApelido { ApelidoNorm = apelido });
            }

            db.Encomendas.Add(enc);
            await db.SaveChangesAsync(ct);

            if (morador.AceitaNotificacao)
            {
                await notificador.NotificarAsync(morador, enc, ct);
                enc.Status = StatusEncomenda.Notificada;
                enc.NotificadaEm = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Created($"/api/encomendas/{enc.Id}", new { enc.Id, Status = enc.Status.ToString(), enc.CodigoRetirada });
        });

        // 3) Pendentes na portaria
        g.MapGet("/pendentes", async (HttpContext http, ChegouDbContext db, CancellationToken ct) =>
        {
            var cid = Cond(http);
            var lista = await db.Encomendas
                .Where(e => e.CondominioId == cid
                            && (e.Status == StatusEncomenda.Recebida || e.Status == StatusEncomenda.Notificada))
                .OrderByDescending(e => e.RecebidaEm)
                .Select(e => new
                {
                    e.Id, e.Status, e.Transportadora, e.Rastreio, e.RecebidaEm,
                    Morador = e.Morador!.Nome,
                    Bloco = e.Unidade!.Bloco,
                    Numero = e.Unidade!.Numero
                })
                .ToListAsync(ct);
            return Results.Ok(lista);
        });

        // 4) Retirada com código
        g.MapPost("/{id:long}/retirar", async (HttpContext http, long id, RetirarRequest req,
            ChegouDbContext db, CancellationToken ct) =>
        {
            var cid = Cond(http);
            var enc = await db.Encomendas.FirstOrDefaultAsync(e => e.Id == id && e.CondominioId == cid, ct);
            if (enc is null) return Results.NotFound();
            if (enc.Status is StatusEncomenda.Retirada or StatusEncomenda.Devolvida)
                return Results.Conflict(new { erro = "Encomenda já finalizada." });
            if (enc.CodigoRetirada != req.Codigo)
                return Results.BadRequest(new { erro = "Código de retirada inválido." });

            enc.Status = StatusEncomenda.Retirada;
            enc.RetiradaEm = DateTime.UtcNow;
            enc.RetiradaPorNome = req.RetiradaPorNome;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { enc.Id, Status = enc.Status.ToString() });
        });
    }
}
