using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Lennox.AsyncPostgresClient.ExceptionLogger;

namespace Lennox.AsyncPostgresClient.Extension
{
    public static class DisposableEx
    {
        // Dispose() calls can throw exceptions. When disposing of multiple
        // objects this can cause a determinancy issue. TryDispose and its
        // counterparts are designed to workaround that.
        public static bool TryDispose<T>(
            this T disposable,
            [CallerFilePath]string callerPath = null,
            [CallerLineNumber]int callerLine = 0,
            [CallerMemberName]string callerMember = null)
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
            catch (Exception e)
            {
                ExceptionLogging.Default.Log(
                    e, callerPath, callerLine, callerMember);
                return false;
            }
        }

        public static bool TryCancelDispose(
            this CancellationTokenSource cts,
            [CallerFilePath]string callerPath = null,
            [CallerLineNumber]int callerLine = 0,
            [CallerMemberName]string callerMember = null)
        {
            if (cts == null)
            {
                return false;
            }

            try
            {
                cts.Cancel(false);
            }
            catch (Exception e)
            {
                ExceptionLogging.Default.Log(
                    e, callerPath, callerLine, callerMember);
            }

            return TryDispose(cts);
        }
    }
}
