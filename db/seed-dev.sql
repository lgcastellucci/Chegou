-- Dados de exemplo para DESENVOLVIMENTO do Chegou (não rodar em produção).
-- Execute como administrador, depois do schema-encomendas.sql.
-- Pode rodar mais de uma vez: não duplica o que já existe.

USE [Chegou];
GO
SET NOCOUNT ON;

-- 1) Condomínio ---------------------------------------------------------
DECLARE @cond INT = (SELECT TOP 1 Id FROM Condominio WHERE Nome = N'Condomínio Teste');
IF @cond IS NULL
BEGIN
    INSERT INTO Condominio (Nome) VALUES (N'Condomínio Teste');
    SET @cond = SCOPE_IDENTITY();
END;

-- 2) Unidades -----------------------------------------------------------
DECLARE @u TABLE (Bloco NVARCHAR(20), Numero NVARCHAR(20));
INSERT INTO @u (Bloco, Numero) VALUES
    (N'A', N'101'), (N'A', N'102'), (N'A', N'201'),
    (N'B', N'101'), (N'B', N'302');

INSERT INTO Unidade (CondominioId, Bloco, Numero, BlocoNorm, NumeroNorm)
SELECT @cond, u.Bloco, u.Numero, UPPER(u.Bloco), UPPER(u.Numero)
FROM @u u
WHERE NOT EXISTS (SELECT 1 FROM Unidade x
                  WHERE x.CondominioId = @cond
                    AND x.BlocoNorm = UPPER(u.Bloco)
                    AND x.NumeroNorm = UPPER(u.Numero));

-- 3) Moradores (só se o condomínio ainda não tiver nenhum) --------------
-- NomeNorm = maiúsculo, sem acento e sem "da/de/do/das/dos/e" (igual ao TextoNormalizador do código).
IF NOT EXISTS (SELECT 1 FROM Morador WHERE CondominioId = @cond)
BEGIN
    DECLARE @m TABLE (Bloco NVARCHAR(20), Numero NVARCHAR(20), Nome NVARCHAR(150), NomeNorm NVARCHAR(150), Telefone NVARCHAR(20));
    INSERT INTO @m (Bloco, Numero, Nome, NomeNorm, Telefone) VALUES
        (N'A', N'101', N'Maria da Silva',        N'MARIA SILVA',          N'19990000001'),
        (N'A', N'102', N'João Pedro Oliveira',   N'JOAO PEDRO OLIVEIRA',  N'19990000002'),
        (N'A', N'201', N'Ana Lima',              N'ANA LIMA',             N'19990000003'),  -- homônimos na mesma unidade:
        (N'A', N'201', N'Ana Lima',              N'ANA LIMA',             N'19990000004'),  -- nunca deve dar match "Alto"
        (N'B', N'101', N'Carlos Mendes',         N'CARLOS MENDES',        N'19990000005'),
        (N'B', N'302', N'Fernanda Rocha',        N'FERNANDA ROCHA',       N'19990000006');

    INSERT INTO Morador (CondominioId, UnidadeId, Nome, NomeNorm, Telefone)
    SELECT @cond, un.Id, m.Nome, m.NomeNorm, m.Telefone
    FROM @m m
    JOIN Unidade un ON un.CondominioId = @cond
                   AND un.BlocoNorm = UPPER(m.Bloco)
                   AND un.NumeroNorm = UPPER(m.Numero);
END;

-- 4) Porteiro (ainda não há login; a API usa o Id por header) ------------
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE Email = N'porteiro@teste.local')
    INSERT INTO Usuario (CondominioId, Nome, Email, SenhaHash, Perfil)
    VALUES (@cond, N'Porteiro Teste', N'porteiro@teste.local', N'SEM-LOGIN-AINDA', N'PORTEIRO');

-- 5) Conferência: use estes Ids nos headers X-Condominio-Id / X-Usuario-Id ---
SELECT 'Condominio' AS Tipo, Id, Nome AS Descricao FROM Condominio WHERE Id = @cond
UNION ALL
SELECT 'Usuario', Id, Nome FROM Usuario WHERE Email = N'porteiro@teste.local'
UNION ALL
SELECT 'Morador', m.Id, CONCAT(m.Nome, ' - Bloco ', u.Bloco, ' apto ', u.Numero)
FROM Morador m JOIN Unidade u ON u.Id = m.UnidadeId
WHERE m.CondominioId = @cond;
GO
