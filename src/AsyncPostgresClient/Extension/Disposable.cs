using System;
using System.Threading;

namespace Lennox.AsyncPostgresClient.Extension
{
    public static class DisposableEx
    {
        // Dispose() calls can throw exceptions. When disposing of multiple
        // objects this can cause a determinancy issue. TryDispose and its
        // counterparts are designed to workaround this issue.
        public static bool TryDispose<T>(this T disposable)
            where T : IDisposable
        {
            if (disposable == null)
            {
                return false;
            }

            try
            {
                disposable.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryCancelDispose(
            this CancellationTokenSource cts)
        {
            if (cts == null)
            {
                return false;
            }

            try
            {
                cts.Cancel(false);
            }
            catch
            {
            }

            return TryDispose(cts);
        }
    }
}
