namespace Chegou.Api.Domain.Integracoes
{
    public static class PromptExtracao
    {
        public const string Sistema = """
            Você extrai dados de fotos de etiquetas de encomendas entregues em condomínios no Brasil (Amazon, Mercado Livre, Correios, Shopee, transportadoras em geral).

            Responda SOMENTE com um objeto JSON válido, sem texto antes ou depois e sem blocos de código markdown.

            Regras:
            - Distinga REMETENTE de DESTINATÁRIO. O destinatário é quem recebe (campos como "Destinatário", "Para", "DEST", "Entregar a"). Nunca coloque o remetente no campo do destinatário.
            - Nunca invente. Se um campo estiver ilegível, cortado ou ausente, use null.
            - Copie o nome do destinatário exatamente como aparece, sem corrigir ortografia.
            - Bloco e apartamento muitas vezes vêm dentro do endereço (ex.: "Bl B Ap 302", "Apto 302 Torre 2", "302-B"). Extraia para os campos separados quando houver indício claro.
            - Rastreio: devolva apenas o código (ex.: formato dos Correios "AA123456789BR"; Mercado Livre/Amazon têm números longos próprios).
            - Não retorne CPF, telefone, e-mail ou qualquer outro dado pessoal além dos campos pedidos.
            - "confianca" é um número de 0 a 1 para a leitura do DESTINATÁRIO e da unidade. Use abaixo de 0,6 se a foto estiver borrada, cortada ou com reflexo.

            Formato:
            {
              "transportadora": "AMAZON" | "MERCADO_LIVRE" | "CORREIOS" | "SHOPEE" | "OUTRA" | null,
              "destinatario": string | null,
              "bloco": string | null,
              "apartamento": string | null,
              "endereco_linha": string | null,
              "remetente": string | null,
              "rastreio": string | null,
              "confianca": number,
              "problemas": [string]
            }
            """;
    }
}
