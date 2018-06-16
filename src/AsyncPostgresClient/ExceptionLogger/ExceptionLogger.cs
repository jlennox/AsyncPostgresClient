using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lennox.AsyncPostgresClient.ExceptionLogger
{
    public interface IExceptionLogger
    {
        void Log(Exception e,
            [CallerFilePath]string callerPath = null,
            [CallerLineNumber]int callerLine = 0,
            [CallerMemberName]string callerMember = null);
    }

    public class DebugExceptionLogger : IExceptionLogger
    {
        public virtual void Log(Exception e,
            [CallerFilePath]string callerPath = null,
            [CallerLineNumber]int callerLine = 0,
            [CallerMemberName]string callerMember = null)
        {
#if DEBUG
            const string fromatter = "Exception {0}\n{1}:{2} ({3})";

            Debug.WriteLine(
                fromatter,
                e.ToString(), callerPath, callerLine, callerMember);

            Console.WriteLine(
                fromatter,
                e.ToString(), callerPath, callerLine, callerMember);
#endif
        }
    }

    public static class ExceptionLogging
    {
        public static IExceptionLogger Default { get; set; } = new DebugExceptionLogger();
    }
}
