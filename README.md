# Chegou

Aviso automático de encomendas para condomínios: o porteiro fotografa a etiqueta (Amazon, Mercado Livre, Correios...), a IA lê os dados, o sistema identifica o morador e avisa a entrega.

## Estrutura

```
Chegou.slnx
src/
  Chegou.Domain          entidades, abstrações e matching (nome x morador) — sem dependências
  Chegou.Infrastructure  EF Core (SQL Server), extrator via Claude, storage de fotos, notificador
  Chegou.Api             minimal API (.NET 10)
tests/Chegou.Tests       testes do matching (xUnit)
db/schema-encomendas.sql esquema SQL Server
docs/                    prompt de extração e regras de matching
```

## Rodar

```bash
dotnet restore
dotnet test
# banco: rode db/schema-encomendas.sql (ou use migrations EF depois)
cd src/Chegou.Api
dotnet user-secrets init
dotnet user-secrets set "Claude:ApiKey" "<sua-chave>"
dotnet run
```

OpenAPI em `/openapi/v1.json` (ambiente Development).

## Fluxo da API

1. `POST /api/encomendas/extrair` (multipart, campo `foto`) → lê a etiqueta e devolve `etiqueta`, `nivel` (Alto/Medio/Baixo) e `candidatos`.
2. `POST /api/encomendas` → porteiro confirma o morador; grava, notifica e aprende apelido se corrigiu.
3. `GET /api/encomendas/pendentes`
4. `POST /api/encomendas/{id}/retirar` → exige o código de retirada.

## Pendências conhecidas (MVP)

- [ ] Login/JWT (hoje `X-Condominio-Id` e `X-Usuario-Id` por header — **provisório**)
- [ ] Migrations do EF Core (hoje o schema vem do .sql)
- [ ] Notificação real (FCM push; WhatsApp depois)
- [ ] Job de retenção: apagar fotos após `Condominio.RetencaoFotoDias` (LGPD)
- [ ] Limite de tamanho de upload e rate limit
- [ ] Encomenda sem morador identificado (hoje o porteiro sempre escolhe um)
- [ ] Importação de moradores por planilha
