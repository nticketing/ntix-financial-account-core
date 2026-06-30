namespace Worker.BackgroundServices.OutboxTransactionEntry.Configuration;

public sealed record OutboxTransactionEntryWorkerConfiguration(int DelayBetweenIterationsInMilliseconds)
{
    public TimeSpan Timeout => TimeSpan.FromMilliseconds(DelayBetweenIterationsInMilliseconds);
}