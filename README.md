# Chegou

Aviso automático de encomendas para condomínios: o porteiro fotografa a etiqueta (Amazon, Mercado Livre, Correios...), a IA lê os dados, o sistema identifica o morador e avisa a entrega.

## Estrutura

```
Chegou.slnx
src/
  Chegou.Domain          entidades, abstrações e matching (nome x morador) — sem dependências
  Chegou.Infrastructure  repositórios com SQL em texto (Dapper + Microsoft.Data.SqlClient), extrator via Claude, storage de fotos, notificador
  Chegou.Api             Web API com Controllers (.NET 10): Controllers/, Contracts/ (requests/responses)
tests/Chegou.Tests       testes do matching (xUnit)
db/schema-encomendas.sql esquema SQL Server
docs/                    prompt de extração e regras de matching
```

## Rodar

```bash
dotnet restore
dotnet test
# banco: rode db/schema-encomendas.sql como administrador (cria o banco, o login chegou_app e as tabelas)
# dados de teste (opcional, só dev): rode db/seed-dev.sql como administrador
cd src/Chegou.Api
dotnet user-secrets init
dotnet user-secrets set "Claude:ApiKey" "<sua-chave>"
dotnet run
```

## Documentação da API

- Interface interativa (Swagger UI): `http://localhost:<porta>/swagger`
- Especificação OpenAPI 3.1: `/openapi/v1.json` (gerada pelo `Microsoft.AspNetCore.OpenApi`, nativo do .NET 10)
- Disponível em Development. Em produção, ligue com `"Docs": { "Habilitar": true }` no `appsettings.json`.
- Para documentar uma action nova: `[EndpointSummary]`, `[EndpointDescription]` e `[ProducesResponseType<T>(...)]`. Os headers provisórios aparecem como parâmetros porque usam `[FromHeader]`.

## Banco de dados

- Arquivos em `/databases` (`Chegou.mdf` / `Chegou_log.ldf`).
- A aplicação usa o login `chegou_app`, com apenas `db_datareader` + `db_datawriter` no banco `Chegou` — sem acesso aos outros bancos do servidor.
- A senha está versionada de propósito (servidor SQL não público). Troque nos dois lugares se mudar: `db/schema-encomendas.sql` e `appsettings.json`.
- Sem EF e sem migrations: o esquema é mantido em scripts SQL (`db/`). A conta de aplicação não tem DDL, então alterações de estrutura rodam com um login administrador.
- Todo SQL da aplicação fica em constantes de texto em `src/Chegou.Infrastructure/Repositorios.cs`.

## Fluxo da API

1. `POST /api/encomendas/extrair` (multipart, campo `foto`) → lê a etiqueta e devolve `etiqueta`, `nivel` (Alto/Medio/Baixo) e `candidatos`.
2. `POST /api/encomendas` → porteiro confirma o morador; grava, notifica e aprende apelido se corrigiu.
3. `GET /api/encomendas/pendentes`
4. `POST /api/encomendas/{id}/retirar` → exige o código de retirada.

## Pendências conhecidas (MVP)

- [ ] Login/JWT (hoje `X-Condominio-Id` e `X-Usuario-Id` por header — **provisório**)
- [ ] Controle de versão do esquema (scripts numerados em `db/` ou uma ferramenta como DbUp/Flyway)
- [ ] Notificação real (FCM push; WhatsApp depois)
- [ ] Job de retenção: apagar fotos após `Condominio.RetencaoFotoDias` (LGPD)
- [ ] Limite de tamanho de upload e rate limit
- [ ] Encomenda sem morador identificado (hoje o porteiro sempre escolhe um)
- [ ] Importação de moradores por planilha
