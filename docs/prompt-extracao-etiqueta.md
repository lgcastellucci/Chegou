# Prompt de extração de etiqueta

## System prompt

```
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
```

## User message

Envie a imagem (base64) com o texto: `Extraia os dados desta etiqueta.`

## Matching (lado .NET, após a extração)

1. **Normalizar** nome: maiúsculas, sem acentos, remover "DA/DE/DO/DAS/DOS/E", espaços duplicados.
2. **Filtrar por unidade:** se `bloco` + `apartamento` vierem lidos, buscar `Unidade` por `BlocoNorm`/`NumeroNorm`.
3. **Comparar nome** com os moradores da unidade e seus apelidos usando Jaro-Winkler (ou Levenshtein normalizado). Considere também coincidência por primeiro + último nome.
4. **Decisão:**
   - `score >= 0,90` e unidade confirmada → pré-seleciona o morador (porteiro só confirma).
   - `0,70 <= score < 0,90` → mostra os candidatos da unidade em ordem de score.
   - `score < 0,70` ou unidade não encontrada → busca por nome em todo o condomínio; senão, seleção manual.
5. **Aprendizado:** quando o porteiro escolher um morador diferente do sugerido (ou escolher manualmente), gravar o `destinatario` lido em `MoradorApelido` e marcar `MatchManual = 1`.
6. **Várias unidades com o mesmo nome:** nunca notificar automaticamente; exigir confirmação.

## Chamada em C# (esboço)

```csharp
// POST https://api.anthropic.com/v1/messages
// headers: x-api-key, anthropic-version: 2023-06-01
// body: model, max_tokens: 600, system: <prompt acima>,
//       messages: [{ role: "user", content: [
//          { type: "image", source: { type: "base64", media_type: "image/jpeg", data: <foto> } },
//          { type: "text", text: "Extraia os dados desta etiqueta." } ] }]
// Parse: content[0].text -> JsonSerializer.Deserialize<EtiquetaDto>()
// Se o parse falhar: 1 retry; depois marcar para preenchimento manual.
```

## Teste antes de construir o app

Reúna 30–50 fotos reais (Amazon, ML, Correios, etiquetas amassadas, à noite, com reflexo) e meça: destinatário correto, unidade correta e quantos casos caíram em confirmação manual.
