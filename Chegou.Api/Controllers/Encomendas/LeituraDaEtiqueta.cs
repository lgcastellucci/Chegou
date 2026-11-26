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

            // Aceita também o formato "data:image/jpeg;base64,...." (usa só o que vem depois da vírgula)
            var imgBase64 = request.imgBase64.Trim();
            if (imgBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var virgula = imgBase64.IndexOf(',');
                imgBase64 = virgula >= 0 ? imgBase64[(virgula + 1)..] : "";
            }

            // Base64 inválido vira mensagem de erro (400) em vez de exceção (500)
            try
            {
                if (Convert.FromBase64String(imgBase64).Length == 0)
                    return ("Imagem inválida (base64 incorreto).", retorno);
            }
            catch (FormatException)
            {
                return ("Imagem inválida (base64 incorreto).", retorno);
            }

            //"image/jpeg",
            var mediaType = "image/jpeg";

            var fotoPath = await Storage.SalvarAsync(condominioId, imgBase64, ct);

            EtiquetaExtraida etiqueta;
            try
            {
                // Provedor de IA vem da tabela Configuracao (chave IA:Provedor): Groq ou Claude
                var provedor = (await Configuracao.ObterAsync("IA:Provedor", "Groq", ct)).Trim();
                if (string.IsNullOrWhiteSpace(provedor))
                    return ("IA:Provedor não configurado na tabela Configuracao.", retorno);

                if (provedor.ToUpper() == "CLAUDE")
                {
                    // Claude não tem limite de tamanho, mas para minimizar o tráfego, reduzimos a foto antes de enviar.
                    imgBase64 = await Storage.ReduzirFotoSeNecessarioAsync(imgBase64, 4, ct);

                    var claude = new Claude();
                    etiqueta = await claude.ExtrairEtiquetaAsync(imgBase64, mediaType, ct);
                }
                else if (provedor.ToUpper() == "GROQ")
                {
                    // Groq limita a 4MB, então reduzimos a foto antes de enviar (o Claude não tem limite)
                    imgBase64 = await Storage.ReduzirFotoSeNecessarioAsync(imgBase64, 4, ct);

                    var groq = new Groq();
                    etiqueta = await groq.ExtrairEtiquetaAsync(imgBase64, mediaType, ct);
                }
                else
                {
                    return ("IA:Provedor inválido na tabela Configuracao: use Groq ou Claude.", retorno);
                }

            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Estourou o tempo do HttpClient (não foi o cliente que cancelou a requisição)
                return ("A leitura da etiqueta demorou demais. Tente novamente ou preencha manualmente.", retorno);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                // Mostra o motivo real (Groq/Claude, chave ausente...) no console da API; o cliente recebe só a mensagem simples
                Console.Error.WriteLine("Falha ao ler a etiqueta: " + ex.Message);
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
