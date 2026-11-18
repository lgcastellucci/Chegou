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
