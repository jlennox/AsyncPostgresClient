using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncPostgresClient
{
    internal static class EmptyArray<T>
    {
        public static readonly T[] Value = new T[0];
    }
}
