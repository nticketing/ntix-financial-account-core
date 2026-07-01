-- 1. Criação do Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SQL_NTIX_FINANCIAL_ACCOUNT')
BEGIN
	CREATE DATABASE [SQL_NTIX_FINANCIAL_ACCOUNT]
END;
GO

USE [SQL_NTIX_FINANCIAL_ACCOUNT];
GO

-- 2. Criação de Login Autorizado ao Servidor
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'ntix_financial_account_core_user')
BEGIN
	CREATE LOGIN ntix_financial_account_core_user WITH PASSWORD = 'yPm4N0sGZDH8562p76g72LuYwxV75ee2';
END;
GO

-- 3. Criação de Usuário do DB
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'ntix_financial_account_core_user')
BEGIN
	CREATE USER ntix_financial_account_core_user FOR LOGIN ntix_financial_account_core_user;
END;
GO

-- 4. Criação do Schema para Isolar Domínio de Fonte de Verdade Financeira
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'financial_truth')
BEGIN
	EXEC('CREATE SCHEMA [financial_truth]');
END

-- 5. Afiliação do Usuário de Login ao Schema apenas
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::financial_truth TO ntix_financial_account_core_user;

-- 6. Script para Partition Function de YYYYMM
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'PF_Transaction_YearMonth')
BEGIN
	DECLARE @StartDate date = '2020-01-01';
	DECLARE @EndDate date = '2040-01-01';

	DECLARE @SQL nvarchar(max) = N'CREATE PARTITION FUNCTION PF_Transaction_YearMonth (int) AS RANGE RIGHT FOR VALUES ('

	DECLARE @CurrentDate date = @StartDate;

	WHILE @CurrentDate <= @EndDate
	BEGIN
		DECLARE @YearMonth int = (YEAR(@CurrentDate) * 100) + MONTH(@CurrentDate);
		SET @SQL += CAST(@YearMonth as nvarchar(10)) + N',';
		SET @CurrentDate = DATEADD(month, 1, @CurrentDate);
	END

	SET @SQL = LEFT(@SQL, len(@SQL) - 1) + N');';

	EXEC sys.sp_executesql @SQL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'PF_Transaction_YearMonth')
BEGIN
	CREATE PARTITION SCHEME PS_Transaction_YearMonth AS PARTITION PF_Transaction_YearMonth ALL TO ([PRIMARY]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'transactions' AND schema_id = SCHEMA_ID('financial_truth'))  
BEGIN   
	CREATE TABLE financial_truth.transactions (
	Id BIGINT IDENTITY(1,1) NOT NULL,    
	CorrelationId UNIQUEIDENTIFIER NOT NULL, -- ID de Correlacionamento com a Operação do Produto
	TransactionId UNIQUEIDENTIFIER NOT NULL, -- ID de Lançamento Transacional
	OperationId UNIQUEIDENTIFIER NOT NULL, -- ID Identificador da Operação de Lançamento  
	ClientId UNIQUEIDENTIFIER NOT NULL, -- ID Identificador da Conta Relacionada
	Amount DECIMAL(18, 2) NOT NULL, -- Valor da Operação
	Type VARCHAR(10) NOT NULL, -- Tipo da Operação (Crédito/Débito)
	OccurredAt DATETIME2(7) NOT NULL, -- Horário da Ocorrência da Operação
	PersistedAt DATETIME2(7) NOT NULL CONSTRAINT DF_PersistedAt DEFAULT SYSUTCDATETIME(), -- Horário de Persistência da Operação
	Balance DECIMAL(18, 2) NOT NULL, -- Saldo após a operação
	YearMonth AS (DATEPART(year, OccurredAt) * 100 + DATEPART(month, OccurredAt)) PERSISTED -- Particionamento do Banco de Dados para Melhoria de Consultas
	CONSTRAINT PK_TransactionId PRIMARY KEY NONCLUSTERED (Id) ON [PRIMARY])   
	ON PS_Transaction_YearMonth (YearMonth);  
END;  

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'balances' AND schema_id = SCHEMA_ID('financial_truth'))
BEGIN
	CREATE TABLE financial_truth.balances (
		Id BIGINT IDENTITY(1,1) NOT NULL,
		ClientId UNIQUEIDENTIFIER NOT NULL,
		Balance DECIMAL(18, 2) NOT NULL,
		CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_PersistedAt DEFAULT SYSUTCDATETIME(), 
		LastModifiedAt DATETIME2(2) NOT NULL,
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'CX_Transactions_YearMonth_Id' AND object_id = OBJECT_ID('financial_truth.transactions'))
BEGIN
	CREATE CLUSTERED INDEX CX_Transactions_YearMonth_Id ON financial_truth.transactions (YearMonth, Id) ON PS_Transaction_YearMonth (YearMonth);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Transactions_TransactionId' AND object_id = OBJECT_ID('financial_truth.transactions'))
BEGIN
	CREATE UNIQUE INDEX UX_Transactions_TransactionId ON financial_truth.transactions (TransactionId) ON [PRIMARY];
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Balances_ClientId' AND object_id = OBJECT_ID('financial_truth.balances'))
BEGIN
	CREATE UNIQUE INDEX UX_Balances_ClientId ON financial_truth.balances (ClientId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_YearMonth_Id_ClientId' AND object_id = OBJECT_ID('financial_truth.transactions'))
BEGIN
	CREATE INDEX IX_Transactions_YearMonth_Id_ClientId ON financial_truth.transactions (ClientId, YearMonth) INCLUDE (TransactionId, CorrelationId, OperationId, Amount, Type, PersistedAt) ON PS_Transaction_YearMonth (YearMonth);
END;
GO

-- Procedure para Débito em Conta
CREATE OR ALTER PROCEDURE financial_truth.sp_debit_transaction
	@ClientId       UNIQUEIDENTIFIER,
	@Amount         DECIMAL(18, 2),
	@CorrelationId  UNIQUEIDENTIFIER,
	@TransactionId  UNIQUEIDENTIFIER,   -- chave de idempotência (UX_Transactions_TransactionId)
	@OperationId    UNIQUEIDENTIFIER,
	@OccurredAt     DATETIME2(7) = NULL,
	@NewBalance     DECIMAL(18, 2) = NULL OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;
 
	-- Validação de entrada: débito precisa ser um valor positivo
	IF @Amount IS NULL OR @Amount <= 0
		THROW 50001, 'O valor do débito deve ser maior que zero.', 1;
 
	IF @OccurredAt IS NULL
		SET @OccurredAt = SYSUTCDATETIME();
 
	DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
	DECLARE @CurrentBalance DECIMAL(18, 2);
	DECLARE @TransactionType VARCHAR(10) = 'DEBIT';
 
	BEGIN TRY
		BEGIN TRANSACTION;
 
		/* ------------------------------------------------------------------
		   (6) PROTEÇÃO DE CONCORRÊNCIA
		   Adquire o lock da linha de saldo da conta ANTES de qualquer leitura
		   de decisão. UPDLOCK impede que dois débitos simultâneos do mesmo
		   ClientId leiam o mesmo saldo e gerem duplo-gasto: o segundo fica
		   bloqueado até o primeiro efetivar (COMMIT) e então lê o saldo já
		   atualizado. HOLDLOCK protege contra a inexistência/inserção
		   concorrente da linha (phantom). Como o lock de conta é sempre o
		   primeiro recurso adquirido, a ordem de lock é consistente e não há
		   deadlock entre operações do mesmo cliente.
		   ------------------------------------------------------------------ */
		SELECT @CurrentBalance = b.Balance
		FROM financial_truth.balances b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
		WHERE b.ClientId = @ClientId;
 
		-- (1) Conta precisa existir
		IF @CurrentBalance IS NULL
			THROW 50002, 'Saldo não encontrado para o ClientId informado.', 1;
 
		/* Idempotência: se este TransactionId já foi lançado, não repete o
		   débito. Como estamos sob o lock da conta, esta checagem é segura
		   contra reentrância/retentativas concorrentes. */
		IF EXISTS (SELECT 1 FROM financial_truth.transactions WHERE TransactionId = @TransactionId)
		BEGIN
			SELECT @NewBalance = Balance
			FROM financial_truth.transactions
			WHERE TransactionId = @TransactionId;
 
			COMMIT TRANSACTION;
			RETURN 0; -- lançamento já existente: operação idempotente
		END
 
		/* (2) Regra de suficiência de saldo.
		   Conforme especificado: saldo deve ser MAIOR que o débito (estrito).
		   >>> Se a intenção for permitir zerar a conta, troque para: < @Amount. */
		IF @CurrentBalance <= @Amount
			THROW 50003, 'Saldo insuficiente para o débito solicitado.', 1;
 
		-- (3) Saldo resultante que será gravado no lançamento
		SET @NewBalance = @CurrentBalance - @Amount;
 
		-- (3) Lançamento na fonte de verdade, já com o saldo pós-operação
		INSERT INTO financial_truth.transactions
			(CorrelationId, TransactionId, OperationId, ClientId, Amount, [Type], OccurredAt, Balance)
		VALUES
			(@CorrelationId, @TransactionId, @OperationId, @ClientId, @Amount, @TransactionType, @OccurredAt, @NewBalance);
 
		-- (4) Atualiza o saldo materializado da conta
		UPDATE financial_truth.balances
			SET Balance = @NewBalance,
			    LastModifiedAt = @Now
		WHERE ClientId = @ClientId;
 
		-- (5) Enfileira na Outbox para processamento futuro (defaults preenchem
		--     CreatedAt, LastModifiedAt, TimeoutSeconds e RetryCount)
		INSERT INTO financial_truth.transactions_outbox
			(CorrelationId, TransactionId, OperationId, ClientId, Amount, [Type], OccurredAt, Status)
		VALUES
			(@CorrelationId, @TransactionId, @OperationId, @ClientId, @Amount, @TransactionType, @OccurredAt, 'PENDING');
 
		COMMIT TRANSACTION;
		RETURN 0; -- sucesso
	END TRY
	BEGIN CATCH
		IF XACT_STATE() <> 0
			ROLLBACK TRANSACTION;
		THROW; -- propaga o erro (50001/50002/50003 ou erro de sistema) já com rollback
	END CATCH
END;
GO
