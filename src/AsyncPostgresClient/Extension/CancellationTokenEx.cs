using System.Threading;

namespace Lennox.AsyncPostgresClient.Extension
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
