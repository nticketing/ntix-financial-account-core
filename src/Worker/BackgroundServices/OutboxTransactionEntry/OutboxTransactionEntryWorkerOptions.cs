namespace Worker.BackgroundServices.OutboxTransactionEntry;

public sealed record OutboxTransactionEntryWorkerOptions(
    int DelayBetweenIterationsInMilliseconds);
