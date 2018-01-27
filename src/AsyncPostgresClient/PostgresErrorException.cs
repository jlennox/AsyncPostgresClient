using System;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresErrorException : Exception
    {
        internal PostgresErrorException(ErrorResponseMessage errorMessage)
            : base(Describe(errorMessage))
        {
            
        }

        private static string Describe(ErrorResponseMessage errorMessage)
        {
            if (errorMessage.ErrorCount == 0)
            {
                return "Server returned an error with no description.";
            }

            var sb = StringBuilderPool.Get();

            try
            {
                for (var i = 0;
                    i < errorMessage.ErrorCount - 1;
                    ++i, sb.Append(", "))
                {
                    var error = errorMessage.Errors[i];

                    sb.Append(error.Value);
                }

                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Free(ref sb);
            }
        }
    }
}