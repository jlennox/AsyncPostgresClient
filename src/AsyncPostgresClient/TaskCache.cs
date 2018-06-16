using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient
{
    internal static class TaskCache
    {
        public static readonly Task<bool> False = Task.FromResult(false);
        public static readonly Task<bool> True = Task.FromResult(true);
    }
}
