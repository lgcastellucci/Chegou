using Chegou.Api.BancoDeDados;
using Chegou.Api.Models;
using Chegou.Api.Services;

namespace Chegou.Api.Controllers.Encomendas
{
    public class LeituraDaEtiqueta
    {
        private readonly string _condominioCodigo;

        public class Request
        {
            /// <summary>
            /// Foto da etiqueta em base64
            /// </summary>
            public string imgBase64 { get; set; }
        }

        public class Response
        {
            public string fotoPath { get; set; }
            public EtiquetaExtraida etiqueta { get; set; }
            public int? unidadeId { get; set; }
            public string nivel { get; set; }
            public IReadOnlyList<CandidatoMorador> candidatos { get; set; }

            public Response()
            {
                fotoPath = string.Empty;
                etiqueta = new EtiquetaExtraida();
                unidadeId = null;
                nivel = string.Empty;
                candidatos = new List<CandidatoMorador>();
            }
        }

        public LeituraDaEtiqueta(string condominioCodigo)
        {
            _condominioCodigo = condominioCodigo;
        }

        public async Task<(string erro, Response response)> Executar(Request request, CancellationToken ct)
        {
            var retorno = new Response();

            if (string.IsNullOrWhiteSpace(_condominioCodigo))
                return ("Condominio Codigo não informado.", retorno);

            var condominioId = await AcessoAoBancoDeDados.DevolveCondominioId(_condominioCodigo, ct);

            if (string.IsNullOrWhiteSpace(condominioId))
                return ("Condominio não encontrado", retorno);


            if (string.IsNullOrWhiteSpace(request.imgBase64))
                return ("Imagem não informada", retorno);

            using var ms = new MemoryStream();
            var bytes = Convert.FromBase64String(request.imgBase64);
            await ms.WriteAsync(bytes, 0, bytes.Length, ct);

            //"image/jpeg",
            var mediaType = "image/jpeg";

            var fotoPath = await Storage.SalvarAsync(condominioId, request.imgBase64, ct);

            EtiquetaExtraida etiqueta;
            try
            {
                //var claude = new Claude();
                //etiqueta = await claude.ExtrairEtiquetaAsync(request.imgBase64, mediaType, ct);

                var groq = new Groq();
                etiqueta = await groq.ExtrairEtiquetaAsync(request.imgBase64, mediaType, ct);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                return ("Não foi possível ler a etiqueta. Preencha manualmente.", retorno);
            }


            var blocoNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Bloco);
            var numeroNorm = TextoNormalizador.NormalizarCodigo(etiqueta.Apartamento);

            int? unidadeId = numeroNorm.Length > 0 ? await AcessoAoBancoDeDados.BuscarUnidade(condominioId, numeroNorm, blocoNorm, ct) : null;
            var moradores = await AcessoAoBancoDeDados.BuscarMoradores(condominioId, unidadeId?.ToString(), ct);

            var resultado = MatchService.Avaliar(etiqueta.Destinatario, moradores, unidadeId != null);

            retorno.fotoPath = fotoPath;
            retorno.etiqueta = etiqueta;
            retorno.unidadeId = unidadeId;
            retorno.nivel = resultado.Nivel.ToString();
            retorno.candidatos = resultado.Candidatos;

            return ("", retorno);

        }
    }
}
