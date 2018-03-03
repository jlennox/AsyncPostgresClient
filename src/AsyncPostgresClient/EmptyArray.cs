using System.Collections.Generic;

namespace Lennox.AsyncPostgresClient
{
    internal static class EmptyArray<T>
    {
        public static readonly T[] Value = new T[0];
    }

    internal static class EmptyList<T>
    {
        public static readonly IReadOnlyList<T> Value = new List<T>();
    }
}
