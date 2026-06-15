IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'transactions_outbox' AND schema_id = SCHEMA_ID('financial_truth'))
BEGIN
	CREATE TABLE financial_truth.transactions_outbox (
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Transactions_Outbox PRIMARY KEY CLUSTERED,
		CorrelationId UNIQUEIDENTIFIER NOT NULL, -- ID de Correlacionamento com a Operação do Produto
		TransactionId UNIQUEIDENTIFIER NOT NULL, -- ID de Lançamento Transacional
		OperationId UNIQUEIDENTIFIER NOT NULL, -- ID Identificador da Operação de Lançamento  
		ClientId UNIQUEIDENTIFIER NOT NULL, -- ID Identificador da Conta Relacionada
		Amount DECIMAL(18, 2) NOT NULL, -- Valor da Operação
		Type VARCHAR(10) NOT NULL, -- Tipo da Operação (Crédito/Débito)
		OccurredAt DATETIME2(7) NOT NULL, -- Horário da Ocorrência da Operação
		Status VARCHAR(10) NOT NULL, -- Status do Outbox PENDING, RUNNING
		CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Outbox_PersistedAt DEFAULT SYSUTCDATETIME(), -- Horário de Persistência para Consumo do Processamento,
		LastModifiedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Transactions_Outbox_LastModifiedAt DEFAULT SYSUTCDATETIME(),
		ProcessedAt DATETIME2(7)
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_Outbox_Oldest_Pending')
BEGIN
	CREATE INDEX IX_Transactions_Outbox_Oldest_Pending ON financial_truth.transactions_outbox (CreatedAt, Id) INCLUDE (CorrelationId, TransactionId, OperationId, ClientId, Amount, [Type], OccurredAt) WHERE Status = 'PENDING';
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Transactions_Outbox_Status')
BEGIN
	ALTER TABLE financial_truth.transactions_outbox ADD CONSTRAINT CK_transactions_outbox_status CHECK (Status IN ('PENDING', 'RUNNING', 'PROCESSED', 'FAILED'));
END;

-- ID Único para Trava de Concorrência
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_Transactions_Outbox_One_Running_By_Client')
BEGIN
	CREATE UNIQUE INDEX UK_Transactions_Outbox_One_Running_By_Client ON financial_truth.transactions_outbox (ClientId) WHERE Status = 'RUNNING';
END;
GO

CREATE OR ALTER PROCEDURE financial_truth.sp_dequeue_transaction_outbox
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	BEGIN TRANSACTION;

	;WITH NextOutbox AS (
		SELECT TOP(1)
			o.Id,
			o.CorrelationId,
			o.TransactionId,
			o.OperationId,
			o.ClientId,
			o.Amount,
			o.[Type],
			o.OccurredAt,
			o.Status,
			o.CreatedAt,
			o.LastModifiedAt,
			o.ProcessedAt
		FROM financial_truth.transactions_outbox o WITH (INDEX(IX_Transactions_Outbox_Oldest_Pending), UPDLOCK, READPAST, ROWLOCK)
		WHERE o.Status = 'PENDING' AND NOT EXISTS (SELECT 1 FROM financial_truth.transactions_outbox r WITH(INDEX(UK_Transactions_Outbox_One_Running_By_Client)) WHERE r.ClientId = o.ClientId AND r.Status = 'RUNNING')
		ORDER BY o.CreatedAt ASC, o.Id ASC
	) UPDATE NextOutbox SET Status = 'RUNNING', LastModifiedAt = SYSUTCDATETIME()
	OUTPUT
		inserted.Id,
		inserted.CorrelationId,
		inserted.TransactionId,
		inserted.OperationId,
		inserted.ClientId,
		inserted.Amount,
		inserted.[Type],
		inserted.OccurredAt,
		inserted.Status;
	COMMIT TRANSACTION;
END;