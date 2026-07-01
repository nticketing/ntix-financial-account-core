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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_YearMonth_Id_ClientId' AND object_id = OBJECT_ID('financial_truth.transactions'))
BEGIN
	CREATE INDEX IX_Transactions_YearMonth_Id_ClientId ON financial_truth.transactions (ClientId, YearMonth) INCLUDE (TransactionId, CorrelationId, OperationId, Amount, Type, PersistedAt) ON PS_Transaction_YearMonth (YearMonth);
END;
GO