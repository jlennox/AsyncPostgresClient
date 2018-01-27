using System;
using System.Collections.Generic;

namespace Lennox.AsyncPostgresClient
{
    // TODO: Read the spec and implement an actual parser.
    internal class PostgresConnectionString
    {
        public string this[string key] => _fields[key];

        private readonly Dictionary<string, string> _fields =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Hostname => _fields["Server"];
        public string Username => _fields["User ID"];
        public string Password => _fields["Password"];
        public string Database => _fields["Database"];
        public int Timeout => int.Parse(_fields["Timeout"]);
        public int CommandTimeout => int.Parse(_fields["Command Timeout"]);
        public int Port => 5432;
        public string Encoding => "UTF8";

        public PostgresConnectionString(string connectionString)
        {
            var fieldsSplit = connectionString.Split(';');
            foreach (var field in fieldsSplit)
            {
                var kvSplit = field.Split('=');
                _fields[kvSplit[0].Trim()] = kvSplit[1].Trim();
            }
        }
    }
}
