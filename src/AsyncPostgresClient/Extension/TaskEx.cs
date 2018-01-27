using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.Extension
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
