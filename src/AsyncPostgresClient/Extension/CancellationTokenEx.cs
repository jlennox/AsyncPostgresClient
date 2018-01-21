using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AsyncPostgresClient.Extension
{
    public static class CancellationTokenEx
    {
        public static CancellationTokenSource Combine(
            this CancellationTokenSource cts,
            CancellationToken cancellationToken)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(
                cts.Token, cancellationToken);
        }
    }
}
