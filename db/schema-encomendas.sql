-- MVP Chegou - Encomendas de Condomínio (SQL Server)
-- Multi-tenant: todo registro carrega CondominioId.
-- Datas e horas em HORA LOCAL do servidor SQL (GETDATE), não em UTC.

-- Arquivos do banco em /databases (a pasta precisa existir e ser gravável pelo SQL Server).
IF DB_ID(N'Chegou') IS NULL
    CREATE DATABASE [Chegou]
    ON PRIMARY
    (
        NAME = N'Chegou',
        FILENAME = N'/databases/Chegou.mdf',
        SIZE = 64MB,
        FILEGROWTH = 64MB
    )
    LOG ON
    (
        NAME = N'Chegou_log',
        FILENAME = N'/databases/Chegou_log.ldf',
        SIZE = 32MB,
        FILEGROWTH = 32MB
    );
GO

-- Login dedicado da aplicação (isolado dos demais bancos do servidor).
-- O servidor SQL não é público, por isso a senha pode ficar versionada.
IF SUSER_ID(N'chegou_app') IS NULL
    CREATE LOGIN [chegou_app]
        WITH PASSWORD = N'JnwIcUQibEPPI9MJ_aw5qmvV',
             DEFAULT_DATABASE = [Chegou],
             CHECK_EXPIRATION = OFF,
             CHECK_POLICY = ON;
GO

USE [Chegou];
GO

-- Usuário no banco Chegou: só leitura/escrita de dados (sem db_owner, sem DDL).
IF USER_ID(N'chegou_app') IS NULL
    CREATE USER [chegou_app] FOR LOGIN [chegou_app] WITH DEFAULT_SCHEMA = [dbo];
ALTER ROLE db_datareader ADD MEMBER [chegou_app];
ALTER ROLE db_datawriter ADD MEMBER [chegou_app];
GO

CREATE TABLE Condominio (
    Id              INT IDENTITY PRIMARY KEY,
    Nome            NVARCHAR(150) NOT NULL,
    Codigo          NVARCHAR(64)  NOT NULL,  -- segredo do condomínio (header X-Condominio-Id); NÃO é o Id
    RetencaoFotoDias INT NOT NULL DEFAULT 7,  -- LGPD: apagar foto N dias após retirada
    Ativo           BIT NOT NULL DEFAULT 1,
    CriadoEm        DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Condominio_Codigo UNIQUE (Codigo)
);

CREATE TABLE Unidade (
    Id              INT IDENTITY PRIMARY KEY,
    CondominioId    INT NOT NULL REFERENCES Condominio(Id),
    Bloco           NVARCHAR(20) NULL,
    Numero          NVARCHAR(20) NOT NULL,      -- apto/casa, texto (ex.: "101", "12B")
    BlocoNorm       NVARCHAR(20) NULL,          -- normalizado p/ busca (sem acento, maiúsculo)
    NumeroNorm      NVARCHAR(20) NOT NULL,
    CONSTRAINT UQ_Unidade UNIQUE (CondominioId, BlocoNorm, NumeroNorm)
);

CREATE TABLE Morador (
    Id              INT IDENTITY PRIMARY KEY,
    CondominioId    INT NOT NULL REFERENCES Condominio(Id),
    UnidadeId       INT NOT NULL REFERENCES Unidade(Id),
    Nome            NVARCHAR(150) NOT NULL,
    NomeNorm        NVARCHAR(150) NOT NULL,     -- sem acento, maiúsculo, sem preposições
    Telefone        NVARCHAR(20) NULL,
    PushToken       NVARCHAR(300) NULL,         -- FCM
    AceitaNotificacao BIT NOT NULL DEFAULT 1,
    Ativo           BIT NOT NULL DEFAULT 1
);
CREATE INDEX IX_Morador_Unidade ON Morador(CondominioId, UnidadeId);

-- Nomes alternativos aprendidos com as correções do porteiro
CREATE TABLE MoradorApelido (
    Id              INT IDENTITY PRIMARY KEY,
    MoradorId       INT NOT NULL REFERENCES Morador(Id),
    ApelidoNorm     NVARCHAR(150) NOT NULL,
    Origem          NVARCHAR(20) NOT NULL DEFAULT 'CORRECAO', -- CORRECAO | CADASTRO
    CONSTRAINT UQ_Apelido UNIQUE (MoradorId, ApelidoNorm)
);

CREATE TABLE Encomenda (
    Id              BIGINT IDENTITY PRIMARY KEY,
    CondominioId    INT NOT NULL REFERENCES Condominio(Id),
    UnidadeId       INT NULL REFERENCES Unidade(Id),
    MoradorId       INT NULL REFERENCES Morador(Id),

    FotoPath        NVARCHAR(300) NULL,          -- apagada após retenção (LGPD)
    DadosExtraidosJson NVARCHAR(MAX) NULL,       -- resposta bruta do modelo
    DestinatarioLido NVARCHAR(150) NULL,
    BlocoLido       NVARCHAR(20) NULL,
    NumeroLido      NVARCHAR(20) NULL,
    Transportadora  NVARCHAR(50) NULL,           -- AMAZON | MERCADO_LIVRE | CORREIOS | OUTRA
    Rastreio        NVARCHAR(60) NULL,
    Confianca       DECIMAL(3,2) NULL,           -- 0.00 a 1.00 (extração)
    ScoreMatch      DECIMAL(3,2) NULL,           -- similaridade nome x morador
    MatchManual     BIT NOT NULL DEFAULT 0,      -- porteiro escolheu/corrigiu

    Status          NVARCHAR(20) NOT NULL DEFAULT 'RECEBIDA', -- RECEBIDA | NOTIFICADA | RETIRADA | DEVOLVIDA
    RecebidaEm      DATETIME2 NOT NULL DEFAULT GETDATE(),
    NotificadaEm    DATETIME2 NULL,
    CodigoRetirada  CHAR(6) NULL,                -- senha/QR para retirada
    RetiradaEm      DATETIME2 NULL,
    RetiradaPorNome NVARCHAR(150) NULL,
    Observacao      NVARCHAR(300) NULL
);
CREATE INDEX IX_Encomenda_Pendentes ON Encomenda(CondominioId, Status, RecebidaEm DESC);
CREATE INDEX IX_Encomenda_Rastreio ON Encomenda(CondominioId, Rastreio);
