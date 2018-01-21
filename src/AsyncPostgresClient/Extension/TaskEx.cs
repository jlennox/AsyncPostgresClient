using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AsyncPostgresClient.Extension
{
    internal static class TaskEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Forget(this Task t)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Forget<T>(this Task<T> t)
        {
        }
    }
}
