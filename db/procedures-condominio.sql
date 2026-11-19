-- Procedures de administração de condomínios.
-- Execute como administrador, depois do schema-encomendas.sql. Pode rodar de novo (CREATE OR ALTER).
-- O login da API (chegou_app) NÃO recebe permissão de EXECUTE: cadastrar condomínio é feito só pelo banco.

USE [Chegou];
GO

-- Cadastra um condomínio e gera o código secreto que o app envia no header X-Condominio-Id.
-- Uso:  EXEC dbo.CondominioCadastrar @Nome = N'Residencial Exemplo';
-- Devolve: Id, Nome, Codigo  (configure o Codigo no app do condomínio)
CREATE OR ALTER PROCEDURE dbo.CondominioCadastrar
    @Nome             NVARCHAR(150),
    @RetencaoFotoDias INT = 7
AS
BEGIN
    SET NOCOUNT ON;

    SET @Nome = LTRIM(RTRIM(ISNULL(@Nome, N'')));
    IF @Nome = N''
        THROW 50001, N'Informe o nome do condomínio.', 1;

    -- 24 bytes aleatórios criptograficamente seguros = 192 bits, em 48 caracteres hexadecimais
    DECLARE @Codigo NVARCHAR(64) = CONVERT(VARCHAR(64), CRYPT_GEN_RANDOM(24), 2);

    INSERT INTO Condominio (Nome, Codigo, RetencaoFotoDias)
    VALUES (@Nome, @Codigo, @RetencaoFotoDias);

    SELECT Id, Nome, Codigo
    FROM Condominio
    WHERE Id = SCOPE_IDENTITY();
END
GO

-- Gera um código novo para um condomínio existente (o app com o código antigo deixa de ter acesso).
-- Uso:  EXEC dbo.CondominioTrocarCodigo @CondominioId = 1;
-- Devolve: Id, Nome, Codigo
CREATE OR ALTER PROCEDURE dbo.CondominioTrocarCodigo
    @CondominioId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Codigo NVARCHAR(64) = CONVERT(VARCHAR(64), CRYPT_GEN_RANDOM(24), 2);

    UPDATE Condominio SET Codigo = @Codigo WHERE Id = @CondominioId;
    IF @@ROWCOUNT = 0
        THROW 50002, N'Condomínio não encontrado.', 1;

    SELECT Id, Nome, Codigo
    FROM Condominio
    WHERE Id = @CondominioId;
END
GO

-- Gera o cadastro das unidades do condominio, com base na quantidade de andates e unidades por andar.
-- Uso:  EXEC dbo.CondominioCadastrarUnidades @CondominioId = 1, @Andares = 20, @UnidadesPorAndar = 4;
-- Devolve: Id, CondominioId, Bloco, Numero, BlocoNorm, NumeroNorm
CREATE OR ALTER PROCEDURE dbo.CondominioCadastrarUnidades
    @CondominioId INT,
    @Andares INT,
    @UnidadesPorAndar INT
    AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @andar INT = 1
    DECLARE @unidade INT = 1
    WHILE @andar <= @Andares
    BEGIN
    	SET @unidade = 1
    	WHILE @unidade <= @UnidadesPorAndar
    	BEGIN
		    DECLARE @numero VARCHAR(10) = CAST(@andar AS VARCHAR(10)) + CAST(@unidade AS VARCHAR(10))
		    DECLARE @numeroNorm VARCHAR(10) = CAST(@andar AS VARCHAR(10)) + CAST(@unidade AS VARCHAR(10))
		    INSERT INTO Unidade (CondominioId, Numero, NumeroNorm) VALUES (@CondominioId, @numero, @numeroNorm)
		    SET @unidade = @unidade + 1
	    END
	    SET @andar = @andar + 1
    END

    SELECT Id, CondominioId, Bloco, Numero, BlocoNorm, NumeroNorm
    FROM Unidade
    WHERE CondominioId = @CondominioId;
END
GO
