# Chegou

Aviso automático de encomendas para condomínios. O porteiro fotografa a etiqueta (Amazon, Mercado Livre, Correios...), a IA lê os dados, o sistema sugere o morador e, depois de confirmado, a entrega é registrada.

Esta é a versão **simples e didática** da API: sem injeção de dependência, sem camadas separadas, cada caso de uso em uma classe e SQL escrito à mão.

## Como funciona

1. O app envia a foto da etiqueta em base64 (`POST /api/encomendas/etiqueta`).
2. A API salva a foto, pede à IA (Groq ou Claude) para ler a etiqueta e procura a unidade e os moradores no banco.
3. A API devolve a leitura, o `nivel` de confiança e até 5 `candidatos` de morador.
4. O porteiro confirma o morador e a encomenda é registrada (`registrar`, ainda não implementada).
5. Na retirada, o morador informa o código da encomenda (`retirar`, ainda não implementada).

## Estrutura

```
Chegou.slnx
Chegou.Api/
  Controllers/
    EncomendasController.cs      rotas (finas): leem o header e chamam o caso de uso
    Encomendas/                  um arquivo por caso de uso
      LeituraDaEtiqueta.cs         foto -> IA -> unidade -> candidatos
      Pendentes.cs                 lista as encomendas na portaria
  BancoDeDados/                  consultas (classe parcial AcessoAoBancoDeDados)
    IdentificaCondominio.cs        código do condomínio -> Id
    BuscarUnidade.cs               bloco/apartamento -> Id da unidade
    BuscarMoradores.cs             moradores (e apelidos) para o match
  Services/
    Groq.cs, Claude.cs             leitura da etiqueta pela IA (JSON)
    MatchService.cs                compara o nome da etiqueta com os moradores
    JaroWinkler.cs                 similaridade entre textos
    TextoNormalizador.cs           maiúsculas, sem acento, sem "da/de/do"
    SqlExtensions.cs               acesso ao SQL Server (SelecionarAsync / ExecutarAsync)
    Storage.cs                     grava a foto em disco
  Models/
    Encomenda.cs, EtiquetaExtraida.cs, PromptExtracao.cs
db/
  schema-encomendas.sql          banco, login da aplicação e tabelas
  procedures-condominio.sql      cadastrar condomínio e trocar código
  seed-dev.sql                   dados de teste (só desenvolvimento)
```

### Convenções

- Cada caso de uso é uma classe em `Controllers/Encomendas/` com o método `Executar(...)`.
- O retorno é uma tupla `(erro, dados)`: **erro vazio significa sucesso**.
- Consultas ficam em `BancoDeDados/`, uma por arquivo, todas na classe parcial `AcessoAoBancoDeDados`.
- SQL montado com `StringBuilder` e lido com `SqlExtensions.SelecionarAsync`, que devolve um `DataTable`.
- Tabelas e colunas em PascalCase (`CondominioId`, `RecebidaEm`). Sem EF e sem migrations: o esquema vive nos scripts de `db/`.

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

| Rota | Situação | O que faz |
|---|---|---|
| `POST /api/encomendas/etiqueta` | pronta | Lê a etiqueta de uma foto e sugere o morador |
| `POST /api/encomendas/registrar` | **não implementada** | Confirma o morador e registra a encomenda |
| `GET /api/encomendas/pendentes` | pronta | Encomendas com status `RECEBIDA` ou `NOTIFICADA` |
| `POST /api/encomendas/{id}/retirar` | **não implementada** | Registra a retirada com o código |

### `POST /api/encomendas/etiqueta`

Corpo (JSON):

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

Erros: `400` com uma mensagem de texto (header ausente, condomínio não encontrado, imagem não informada, falha na leitura da etiqueta).

**Níveis de match**
- `Alto`: unidade confirmada, score de 0,90 ou mais e sem empate entre candidatos (o app pode pré-selecionar).
- `Medio`: score de 0,70 ou mais (mostrar os candidatos).
- `Baixo`: escolha manual.
- Homônimos (empate no topo) nunca dão `Alto`.

### `GET /api/encomendas/pendentes`

Devolve uma lista: `id`, `status`, `transportadora`, `rastreio`, `recebidaEm`, `morador`, `bloco`, `numero`.

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
| `Groq:ApiKey`, `Groq:Model` | leitura da etiqueta pela Groq (usada nos testes iniciais). O modelo padrão no código é `qwen/qwen3.8-27b`: confirme o ID no console da Groq |
| `Claude:ApiKey`, `Claude:Model` | leitura da etiqueta pelo Claude (alternativa) |
| `Storage:Path` | pasta das fotos (padrão: `storage`, ignorada pelo git) |

- A IA usada é escolhida em `LeituraDaEtiqueta.cs`: o trecho do `Groq` está ativo e o do `Claude` está comentado. Troque comentando um e descomentando o outro.
- **Não versione chaves de API.** Preencha `ApiKey` só na sua máquina.
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

## Pendências

- [ ] Implementar `registrar` e `retirar`
- [ ] Notificar o morador quando a encomenda for registrada
- [ ] Revisar o SQL: valores que vêm de fora (header, JSON) devem ir por parâmetro, não concatenados
- [ ] Ler a configuração pelo `IConfiguration` do `Program` (user-secrets e `appsettings.Development.json`), em vez de reabrir o `appsettings.json` em cada classe
- [ ] Padronizar os status HTTP (401 para header ausente ou código inválido) e o tratamento de erros da leitura da etiqueta
- [ ] Acesso ao celular: a API precisa escutar na rede (não só em `localhost`) e o Android precisa permitir HTTP em texto claro nos testes locais
- [ ] Job de retenção: apagar fotos depois de `Condominio.RetencaoFotoDias` (LGPD)
- [ ] Testes do `MatchService` e documentação da API (Swagger), se voltarem ao projeto
