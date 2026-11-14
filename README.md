# Chegou

Aviso automático de encomendas para condomínios: o porteiro fotografa a etiqueta (Amazon, Mercado Livre, Correios...), a IA lê os dados, o sistema identifica o morador e avisa a entrega.

## Estrutura

Projeto único, sem camadas separadas.

```
Chegou.slnx
Chegou.Api/
  Controllers/        rotas finas: só chamam os casos de uso
  Models/             entidades, requests e responses
  Domain/             um caso de uso por classe + acesso a dados
    Matching/         nome x morador (lógica pura, com testes)
    Integracoes/      leitura da etiqueta (Claude), fotos e notificação
  Program.cs          configuração e injeção de dependência
Chegou.Tests/         testes do matching (xUnit)
db/                   scripts SQL (schema e dados de teste)
docs/                 prompt de extração e regras de matching
```

### Convenções

- **Um caso de uso por classe** em `Domain/`, com um método `ExecutarAsync(...)` (ex.: `EncomendasPendentes`, `EncomendaRetirada`).
- **ADO.NET puro** (`Microsoft.Data.SqlClient`), sem EF e sem Dapper. O SQL fica em constantes de texto no topo de cada caso de uso.
- **Sempre `SqlParameter`**: use o atalho `.Par("@Nome", SqlDbType.X, valor, tamanho)` de `SqlExtensions`. Nunca concatene valores no SQL.
- **Tudo assíncrono** (`async`/`await` com `CancellationToken`), do controller até o banco.
- **Injeção pelo construtor** do controller (construtor primário).
- **Status** trafegam e são gravados em maiúsculas: `RECEBIDA`, `NOTIFICADA`, `RETIRADA`, `DEVOLVIDA`.

### Criar um caso de uso novo

1. Crie `Domain/MeuCasoDeUso.cs` com `ExecutarAsync(...)`.
2. Registre em `Program.cs`: `builder.Services.AddScoped<MeuCasoDeUso>();`
3. Receba no construtor de `EncomendasController` e chame na action.

## Rodar

```bash
dotnet restore
dotnet test
# banco: rode db/schema-encomendas.sql como administrador (cria o banco, o login chegou_app e as tabelas)
# dados de teste (opcional, só dev): rode db/seed-dev.sql como administrador
cd Chegou.Api
dotnet user-secrets init
dotnet user-secrets set "Claude:ApiKey" "<sua-chave>"
dotnet run
```

Exemplos de chamadas prontas em `Chegou.Api/Chegou.Api.http`.
Collection do Postman com testes automáticos (importa também no Insomnia e no Bruno): `docs/Chegou.postman_collection.json`. A pasta 1 roda de ponta a ponta sem IA.

## Documentação da API

- Interface interativa (Swagger UI): `http://localhost:5234/swagger`
- Especificação OpenAPI: `/openapi/v1.json` (gerada pelo `Microsoft.AspNetCore.OpenApi`, nativo do .NET 10)
- Disponível em Development. Em produção, ligue com `"Docs": { "Habilitar": true }` no `appsettings.json`.
- Para documentar uma action nova: `[EndpointSummary]`, `[EndpointDescription]` e `[ProducesResponseType<T>(...)]`.

## Banco de dados

- Arquivos em `/databases` (`Chegou.mdf` / `Chegou_log.ldf`).
- A aplicação usa o login `chegou_app`, com apenas `db_datareader` + `db_datawriter` no banco `Chegou`, sem acesso aos outros bancos do servidor.
- A senha está versionada de propósito (servidor SQL não público). Troque nos dois lugares se mudar: `db/schema-encomendas.sql` e `appsettings.json`.
- Não há migrations: o esquema é mantido em scripts SQL (`db/`). A conta de aplicação não tem DDL, então alterações de estrutura rodam com um login administrador.

## Fluxo da API

1. `POST /api/encomendas/extrair` (multipart, campo `foto`): lê a etiqueta e devolve `etiqueta`, `nivel` (Alto/Medio/Baixo) e `candidatos`.
2. `POST /api/encomendas`: o porteiro confirma o morador; grava, notifica e aprende apelido se corrigiu.
3. `GET /api/encomendas/pendentes`
4. `POST /api/encomendas/{id}/retirar`: exige o código de retirada.

## Pendências conhecidas (MVP)

- [ ] Login/JWT (hoje tudo roda no condomínio 1 e usuário 1, constantes em `EncomendasController`)
- [ ] Controle de versão do esquema (scripts numerados em `db/` ou uma ferramenta como DbUp/Flyway)
- [ ] Notificação real (FCM push; WhatsApp depois)
- [ ] Job de retenção: apagar fotos após `Condominio.RetencaoFotoDias` (LGPD)
- [ ] Limite de tamanho de upload, rate limit e limite de tentativas no código de retirada
- [ ] Encomenda sem morador identificado (hoje o porteiro sempre escolhe um)
- [ ] Importação de moradores por planilha
