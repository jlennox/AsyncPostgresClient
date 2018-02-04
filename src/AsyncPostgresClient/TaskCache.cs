using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient
{
    internal static class TaskCache
    {
        public static Task<bool> False = Task.FromResult(false);
        public static Task<bool> True = Task.FromResult(true);
    }
}
