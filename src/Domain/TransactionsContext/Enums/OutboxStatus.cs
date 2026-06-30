using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.TransactionsContext.Enums
{
    public enum OutboxStatus
    {
        PENDING = 1,
        RUNNING = 2,
        PROCESSED = 3,
        FAILED = 4,
        EXPIRED = 5,
    }
}
