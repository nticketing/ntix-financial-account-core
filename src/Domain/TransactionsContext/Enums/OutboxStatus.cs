using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.TransactionsContext.Enums
{
    public enum OutboxStatus
    {
        PENDING = 1,
        PUBLISHED = 2,
    }
}
