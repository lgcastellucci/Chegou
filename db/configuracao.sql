-- Configurações da aplicação (chave/valor), no lugar do appsettings.json.
-- Só a connection string continua no appsettings.json (é ela que dá acesso ao banco).
-- Execute como administrador, depois do schema-encomendas.sql. Pode rodar de novo: não apaga o que já existe.

USE [Chegou];
GO

IF OBJECT_ID(N'dbo.Configuracao', N'U') IS NULL
    CREATE TABLE dbo.Configuracao (
        Chave           NVARCHAR(100) NOT NULL PRIMARY KEY,   -- ex.: Groq:ApiKey
        Valor           NVARCHAR(MAX) NULL,
        Descricao       NVARCHAR(300) NULL,
        AtualizadoEm    DATETIME2 NOT NULL DEFAULT GETDATE()
    );
GO

-- Valores iniciais (só insere o que ainda não existe). As chaves de API ficam VAZIAS aqui:
-- preencha com a procedure dbo.ConfiguracaoDefinir, sem guardar a chave em nenhum arquivo do repositório.
INSERT INTO dbo.Configuracao (Chave, Valor, Descricao)
SELECT v.Chave, v.Valor, v.Descricao
FROM (VALUES
    (N'Groq:ApiKey',   N'',                  N'Chave da API da Groq (leitura da etiqueta)'),
    (N'Groq:Model',    N'qwen/qwen3.8-27b',  N'Modelo da Groq usado na leitura da etiqueta'),
    (N'Claude:ApiKey', N'',                  N'Chave da API do Claude (alternativa à Groq)'),
    (N'Claude:Model',  N'claude-haiku-5-5',  N'Modelo do Claude usado na leitura da etiqueta'),
    (N'Storage:Path',  N'storage',           N'Pasta onde as fotos das etiquetas são gravadas')
) AS v (Chave, Valor, Descricao)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Configuracao c WHERE c.Chave = v.Chave);
GO

-- A API só LÊ as configurações: o login da aplicação não pode inserir, alterar nem apagar nesta tabela
-- (o DENY vale mesmo com o papel db_datawriter).
DENY INSERT, UPDATE, DELETE ON dbo.Configuracao TO chegou_app;
GO


