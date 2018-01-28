using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.Extension
{
    internal static class TaskEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Forget(this Task task)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Forget<T>(this Task<T> task)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertCompleted(this Task task)
        {
            if (!task.IsCompleted)
            {
                throw new InvalidOperationException(
                    "Attempt to get value from incomplete task.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertCompleted<T>(this ValueTask<T> valueTask)
        {
            if (!valueTask.IsCompleted)
            {
                throw new InvalidOperationException(
                    "Attempt to get value from incomplete task.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CompletedTaskValue<T>(this Task<T> task)
        {
            AssertCompleted(task);

            return task.Result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CompletedTaskValue<T>(this ValueTask<T> valueTask)
        {
            AssertCompleted(valueTask);

            return valueTask.Result;
        }
    }
}
