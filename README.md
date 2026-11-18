# Chegou

Aviso automático de encomendas para condomínios. O porteiro fotografa a etiqueta (Amazon, Mercado Livre, Correios...), a IA lê os dados, o sistema sugere o morador e, depois de confirmado, a entrega é registrada e pode ser retirada com um código.

Esta é a versão **simples e didática** da API (POC): sem injeção de dependência, sem camadas separadas, cada caso de uso em uma classe e SQL escrito à mão.

## Como funciona

1. O app envia a foto da etiqueta em base64 (`POST /api/encomendas/etiqueta`).
2. A API salva a foto, pede à IA (Groq ou Claude) para ler a etiqueta e procura a unidade e os moradores no banco.
3. A API devolve a leitura, o `nivel` de confiança e até 5 `candidatos` de morador.
4. O porteiro confirma o morador e a encomenda é registrada (`POST /api/encomendas/registrar`). A API devolve o **código de retirada**.
5. A encomenda aparece em `GET /api/encomendas/pendentes`.
6. Na retirada, informa-se o código (`POST /api/encomendas/{id}/retirar`) e a encomenda sai da lista.

## Estrutura

```
Chegou.slnx
Chegou.Api/
  Controllers/
    EncomendasController.cs      rotas (finas): leem o header e chamam o caso de uso
    Encomendas/                  um arquivo por caso de uso
      LeituraDaEtiqueta.cs         foto -> IA -> unidade -> candidatos
      Registrar.cs                 grava a encomenda, gera o código de retirada, aprende apelido
      Pendentes.cs                 lista as encomendas na portaria
      Retirar.cs                   registra a retirada com o código
  BancoDeDados/                  consultas (classe parcial AcessoAoBancoDeDados)
    IdentificaCondominio.cs        código do condomínio -> Id
    BuscarUnidade.cs               bloco/apartamento -> Id da unidade
    BuscarMoradores.cs             moradores (e apelidos) para o match
    BuscarMorador.cs               um morador pelo Id
  Services/
    Groq.cs, Claude.cs             leitura da etiqueta pela IA (JSON)
    MatchService.cs                compara o nome da etiqueta com os moradores
    JaroWinkler.cs                 similaridade entre textos
    TextoNormalizador.cs           normalização de nomes, códigos e números
    SqlExtensions.cs               acesso ao SQL Server (consultas e transação)
    Storage.cs                     grava a foto em disco
  Models/
    Encomenda.cs, Morador.cs, EtiquetaExtraida.cs, PromptExtracao.cs
db/
  schema-encomendas.sql          banco, login da aplicação e tabelas
  procedures-condominio.sql      cadastrar condomínio e trocar código
  seed-dev.sql                   dados de teste (só desenvolvimento)
docs/
  Chegou.postman_collection.json collection de testes (Postman, também importa no Insomnia/Bruno)
  prompt-extracao-etiqueta.md    prompt usado na leitura da etiqueta
```

### Convenções

- Cada caso de uso é uma classe em `Controllers/Encomendas/` com o método `Executar(...)`.
- O retorno é uma tupla `(erro, dados)`: **erro vazio significa sucesso**. O controller transforma qualquer erro em `400` com a mensagem em texto.
- Consultas ficam em `BancoDeDados/`, uma por arquivo, todas na classe parcial `AcessoAoBancoDeDados`.
- SQL montado com `StringBuilder` e lido com `SqlExtensions.SelecionarAsync` (devolve um `DataTable`). Escritas usam `ExecutarAsync`; a gravação da encomenda usa a transação do `SqlExtensions`.
- Tabelas e colunas em PascalCase (`CondominioId`, `RecebidaEm`). Sem EF e sem migrations: o esquema vive nos scripts de `db/`.
- Todo valor que entra concatenado no SQL passa por uma função do `TextoNormalizador`:

  | Função | Deixa | Usada em |
  |---|---|---|
  | `NormalizarCondominioCodigo` | letras, números e hífen | código do condomínio (header) |
  | `NormalizarCodigo` | letras e números | bloco e apartamento |
  | `NormalizarNumero` | só números | ids |
  | `Normalizar` | letras, números e espaço, sem "da/de/do" | nomes (apelidos) |

- **Datas e horas em hora local** do servidor SQL (`GETDATE()`), não em UTC.

## Identificação do condomínio (`X-Condominio-Id`)

Não há login nem usuários. Cada instalação (um celular em um condomínio) envia no header `X-Condominio-Id` o **código** do condomínio.

- O código é um segredo aleatório gravado em `Condominio.Codigo`. **Não é o `Id` do banco.**
- Sem login, esse código funciona como a chave de acesso do condomínio: trate-o como senha e guarde-o só na configuração do app.
- Cadastrar um condomínio novo (pelo banco, como administrador):
  ```sql
  EXEC dbo.CondominioCadastrar @Nome = N'Residencial Exemplo';
  ```
  Devolve `Id`, `Nome` e `Codigo` (48 caracteres hexadecimais). Configure o `Codigo` no app.
- Trocar o código de um condomínio (o app com o código antigo perde o acesso):
  ```sql
  EXEC dbo.CondominioTrocarCodigo @CondominioId = 1;
  ```
- O login `chegou_app` usado pela API não tem permissão de executar as procedures: cadastrar condomínio é tarefa só do banco.
- Código de teste do `seed-dev.sql` (SÓ desenvolvimento): `DEV-CHEGOU-CONDOMINIO-TESTE-0001`.

## Rotas

Todas exigem o header `X-Condominio-Id`.

| Rota | O que faz |
|---|---|
| `POST /api/encomendas/etiqueta` | Lê a etiqueta de uma foto e sugere o morador |
| `POST /api/encomendas/registrar` | Confirma o morador e registra a encomenda |
| `GET /api/encomendas/pendentes` | Encomendas com status `RECEBIDA` ou `NOTIFICADA` |
| `POST /api/encomendas/{id}/retirar` | Registra a retirada com o código |

### Erros

Nesta versão **todo erro é `400` com uma mensagem em texto simples** (não há 401, 404 nem 409). Mensagens atuais:

| Mensagem | Quando |
|---|---|
| `Header não encontrado` | falta o `X-Condominio-Id` |
| `Condominio não encontrado` | código desconhecido |
| `Imagem não informada` | `imgBase64` vazio |
| `Não foi possível ler a etiqueta. Preencha manualmente.` | falha na IA |
| `Morador não encontrado` | `moradorId` inválido no registro |
| `Encomenda não encontrada` | id inexistente na retirada |
| `Codigo de retirada inválido` | código de retirada errado |
| `Encomenda já retirada ou devolvida` | retirada repetida |

Corpo JSON inválido ou incompleto também devolve `400` (validação automática do ASP.NET).

### `POST /api/encomendas/etiqueta`

Corpo:

```json
{ "imgBase64": "<foto em base64, só o texto, sem o prefixo data:image>" }
```

Resposta 200:

```json
{
  "fotoPath": "1/3f2a....jpg",
  "etiqueta": { "destinatario": "Maria da Silva", "bloco": "A", "apartamento": "101",
                "rastreio": "AA123456789BR", "transportadora": "CORREIOS", "confianca": 0.95, "problemas": [] },
  "unidadeId": 1,
  "nivel": "Alto",
  "candidatos": [ { "moradorId": 1, "nome": "Maria da Silva", "unidadeId": 1, "score": 1.0 } ]
}
```

**Níveis de match**
- `Alto`: unidade confirmada, score de 0,90 ou mais e sem empate entre candidatos (o app pode pré-selecionar).
- `Medio`: score de 0,70 ou mais (mostrar os candidatos).
- `Baixo`: escolha manual.
- Homônimos (empate no topo) nunca dão `Alto`.
- O bloco é opcional. Sem unidade encontrada (ou ambígua), a busca considera todos os moradores do condomínio.

### `POST /api/encomendas/registrar`

Corpo:

```json
{
  "fotoPath": "1/3f2a....jpg",
  "etiqueta": { "...": "o mesmo objeto devolvido pela leitura" },
  "moradorId": 1,
  "moradorSugeridoId": 1,
  "scoreMatch": 1.0
}
```

Resposta 200:

```json
{ "idEncomenda": 3, "status": "NOTIFICADA", "codigoRetirada": "889841" }
```

- `status` é `NOTIFICADA` quando o morador aceita notificação; senão, `RECEBIDA`. **O envio real da notificação (push) ainda não existe**: hoje só o status é gravado.
- **Aprendizado de apelido:** se `moradorSugeridoId` for diferente de `moradorId` (ou nulo), o nome lido na etiqueta é gravado em `MoradorApelido`, e nas próximas leituras o match acerta melhor.
- A encomenda e o apelido são gravados na mesma transação.

### `GET /api/encomendas/pendentes`

Devolve uma lista: `id`, `status`, `transportadora`, `rastreio`, `recebidaEm`, `morador`, `bloco`, `numero`.

### `POST /api/encomendas/{id}/retirar`

Corpo: `{ "codigo": "889841", "retiradaPorNome": "Maria da Silva" }`. Resposta 200: `{ "idEncomenda": 3, "status": "RETIRADA" }`.

## Banco de dados

- Ordem dos scripts, todos como administrador: `schema-encomendas.sql`, depois `procedures-condominio.sql`, e o `seed-dev.sql` (opcional, só desenvolvimento).
- Os arquivos do banco ficam em `/databases` (`Chegou.mdf` e `Chegou_log.ldf`). A pasta precisa existir e ser gravável pelo SQL Server.
- A API usa o login `chegou_app`, com apenas `db_datareader` e `db_datawriter` no banco `Chegou`: sem DDL e sem acesso aos outros bancos do servidor.
- A senha do `chegou_app` está versionada de propósito (o servidor SQL não é público). Se trocar, altere nos dois lugares: `db/schema-encomendas.sql` e `appsettings.json`.
- Mudou o esquema? Em desenvolvimento, apague e recrie o banco:
  ```sql
  USE master;
  ALTER DATABASE [Chegou] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [Chegou];
  ```

## Configuração (`Chegou.Api/appsettings.json`)

| Chave | Para quê |
|---|---|
| `ConnectionStrings:Default` | conexão com o SQL Server |
| `Groq:ApiKey`, `Groq:Model` | leitura da etiqueta pela Groq (usada nos testes iniciais). Sem `Model`, o código usa `qwen/qwen3.8-27b`: confirme o ID no console da Groq |
| `Claude:ApiKey`, `Claude:Model` | leitura da etiqueta pelo Claude (alternativa) |
| `Storage:Path` | pasta das fotos (padrão: `storage`, ignorada pelo git) |

- A IA usada é escolhida em `LeituraDaEtiqueta.cs`: o trecho do `Groq` está ativo e o do `Claude` está comentado. Troque comentando um e descomentando o outro.
- **Não versione chaves de API.** O `appsettings.json` é versionado e hoje é o único arquivo lido pelo código: faça commit sempre com as chaves **vazias**. Se uma chave já foi para o git ou para qualquer lugar público, revogue e gere outra.
- A Groq limita imagens em base64 a 4 MB: reduza a foto no app antes de enviar.

## Rodar

```bash
dotnet restore
dotnet run --project Chegou.Api
```

A API sobe em `http://localhost:5234` (veja `Properties/launchSettings.json`).

Testar a lista de pendentes:

```bash
curl -H "X-Condominio-Id: DEV-CHEGOU-CONDOMINIO-TESTE-0001" http://localhost:5234/api/encomendas/pendentes
```

Testar a leitura de uma etiqueta (PowerShell):

```powershell
$img = [Convert]::ToBase64String([IO.File]::ReadAllBytes("etiqueta.jpg"))
Invoke-RestMethod -Method Post -Uri http://localhost:5234/api/encomendas/etiqueta `
  -Headers @{ "X-Condominio-Id" = "DEV-CHEGOU-CONDOMINIO-TESTE-0001" } `
  -ContentType "application/json" -Body (@{ imgBase64 = $img } | ConvertTo-Json)
```

### Collection do Postman

`docs/Chegou.postman_collection.json` tem 5 pastas com testes automáticos:

1. **Identificação do condomínio:** header ausente, código inexistente, tentativa de SQL injection e código válido.
2. **Fluxo completo sem IA:** registrar, listar, retirar (com código errado, certo e repetido). Precisa do `seed-dev.sql`.
3. **Leitura da etiqueta:** usa a IA. Cole a foto em base64 na variável `imgBase64` da collection.
4. **Aprendizado de apelido.**
5. **Casos de erro.**

O header é enviado sozinho a partir da variável `codigoCondominio` (padrão: o código de teste do seed).

## Limitações conhecidas (POC)

- **Textos livres concatenados no SQL.** Valores normalizados (código do condomínio, ids, bloco, apartamento, apelido) são seguros. Já os textos livres (destinatário, transportadora, rastreio, `fotoPath` no registro e `retiradaPorNome` na retirada) entram concatenados: um nome com apóstrofo (D'Ávila) quebra o SQL com erro 500 e, em teoria, quem tem o código do condomínio consegue injetar SQL. Decisão da POC; antes de produção, passar para parâmetros (`SqlParameter`).
- **Transação do registro** sem `Rollback` quando um comando falha no meio, e o `Commit` não tem o resultado verificado.
- **Notificação** ao morador ainda não é enviada.
- **Configuração** lida direto do `appsettings.json` em cada classe (sem user-secrets nem variáveis de ambiente).
- O código de retirada (6 dígitos) não tem limite de tentativas.

## Pendências

- [ ] Enviar a notificação (push) ao morador no registro
- [ ] Rollback e verificação do commit no registro
- [ ] Carregar os apelidos do morador em `BuscarMorador` (hoje só `BuscarMoradores` os carrega)
- [ ] Ler a configuração pelo `IConfiguration` do `Program` (user-secrets e `appsettings.Development.json`)
- [ ] Tratar base64 inválido e timeout da IA (hoje viram erro 500) e reutilizar o `HttpClient`
- [ ] Padronizar os status HTTP (401 para header ausente ou código inválido; 404/409 onde fizer sentido)
- [ ] Acesso ao celular: a API precisa escutar na rede (não só em `localhost`) e o Android precisa permitir HTTP em texto claro nos testes locais
- [ ] Job de retenção: apagar fotos depois de `Condominio.RetencaoFotoDias` (LGPD)
- [ ] Testes do `MatchService` e documentação da API (Swagger), se voltarem ao projeto
