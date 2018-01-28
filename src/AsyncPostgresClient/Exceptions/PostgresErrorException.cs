using System;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient.Exceptions
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

    public class PostgresInvalidMessageException : Exception
    {
        internal PostgresInvalidMessageException(IPostgresMessage message)
            : base(Describe(message))
        {

        }

        internal PostgresInvalidMessageException(
            IPostgresMessage message, string description)
            : base($"{Describe(message)} {description}")
        {

        }

        private static string Describe(IPostgresMessage message)
        {
            return $"Message type {message.GetType()} not expected at this time.";
        }
    }
}