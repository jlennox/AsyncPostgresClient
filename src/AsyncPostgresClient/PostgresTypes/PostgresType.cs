using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.BufferAccess;

namespace Lennox.AsyncPostgresClient.PostgresTypes
{
    internal class PostgresTypeCollection
    {
        // https://www.postgresql.org/docs/9.2/static/catalog-pg-type.html
        private const string _query = "select typname, typtype, oid from pg_type;";

        private enum ResultIndex
        {
            Typename = 0,
            Type = 1,
            Oid = 2
        }

        public PostgresType[] Types { get; }

        private readonly Dictionary<int, PostgresTypeConverter> _oidLookup;

        private PostgresTypeCollection(List<PostgresType> types)
        {
            Types = types.ToArray();

            _oidLookup = types.ToDictionary(
                t => t.Oid,
                t => PostgresTypeConverter.ConverterByName(t.Name));
        }

        internal static async ValueTask<PostgresTypeCollection> Create(
            bool async,
            PostgresDbConnectionBase connection,
            CancellationToken cancel)
        {
            using (var command = (PostgresCommand)connection.CreateCommand())
            {
                command.CommandText = _query;

                // Avoid infinite loops.
                command.DoNotLoadTypeCollection = true;

                using (var reader = (PostgresDbDataReader)
                    await command.ExecuteDbDataReader(
                        async, CommandBehavior.Default, cancel)
                    .ConfigureAwait(false))
                {
                    var queryResults = new List<PostgresType>(500);

                    while (await reader.Read(async, cancel)
                        .ConfigureAwait(false))
                    {
                        queryResults.Add(new PostgresType {
                            Name = reader.GetString((int)ResultIndex.Typename),
                            //Type = reader.GetByte((int)ResultIndex.Type),
                            Oid = reader.GetInt32((int)ResultIndex.Oid)
                        });
                    }

                    return new PostgresTypeCollection(queryResults);
                }
            }
        }

        public object Convert(int oid, DataRow row, PostgresClientState state)
        {
            if (_oidLookup.TryGetValue(oid, out var typeConverter))
            {
                if (typeConverter.Converter == null)
                {
                    throw new NotImplementedException(
                        $"Convertion of type '{typeConverter.Name}' is not supported.");
                }

                return typeConverter.Converter(row, state);
            }

            // TODO
            throw new InvalidOperationException("Unknown oid " + oid);
        }
    }

    internal struct PostgresType
    {
        public string Name
        {
            get => _name;
            set {
                _name = value;
                _nameHashCode = value.GetHashCode();
            }
        }

        public byte Type { get; set; }
        public int Oid { get; set; }

        private string _name;
        private int _nameHashCode;
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal class PostgresTypeConverterMethodAttribute : Attribute
    {
        public string PostgresTypeName { get; }

        public PostgresTypeConverterMethodAttribute(string postgresTypeName)
        {
            PostgresTypeName = postgresTypeName;
        }
    }

    internal static class PostgresTypeNames
    {
        internal const string Bool = "bool";
        internal const string Text = "text";
        internal const string Int2 = "int2";
        internal const string Int4 = "int4";
        internal const string Int8 = "int8";
        internal const string Money = "money";
        internal const string Numeric = "numeric";
        internal const string Float4 = "float4";
        internal const string Float8 = "float8";
        internal const string Uuid = "uuid";
    }

    // TODO: Consider generating IL.
    internal struct PostgresTypeConverter
    {
        internal delegate object ConvertDelegate(
            DataRow row, PostgresClientState state);

        private static readonly Dictionary<int, ConvertDelegate> _lookup;

        public string Name { get; set; }
        public ConvertDelegate Converter { get; set; }

        static PostgresTypeConverter()
        {
            _lookup = typeof(PostgresTypeConverter).GetMethods(
                    BindingFlags.Static | BindingFlags.NonPublic)
                .Where(t => t.ReturnType == typeof(object))
                .Select(t => new {
                    Method = t,
                    Attribute = t.GetCustomAttribute<
                        PostgresTypeConverterMethodAttribute>()
                })
                .Where(t => t.Attribute != null)
                .ToDictionary(
                    t => t.Attribute.PostgresTypeName.GetHashCode(),
                    t => Delegate.CreateDelegate(
                        typeof(ConvertDelegate), t.Method) as ConvertDelegate);
        }

        public static PostgresTypeConverter ConverterByName(string postgresTypeName)
        {
            var typeHash = postgresTypeName.GetHashCode();

            _lookup.TryGetValue(typeHash, out var converter);

            return new PostgresTypeConverter {
                Name = postgresTypeName,
                Converter = converter
            };
        }

        public static object Convert(
            int typeNameHash,
            DataRow row,
            PostgresClientState state)
        {
            if (row.IsNull)
            {
                return DBNull.Value;
            }

            if (_lookup.TryGetValue(typeNameHash, out var converter))
            {
                return converter(row, state);
            }

            throw new ArgumentOutOfRangeException(
                nameof(typeNameHash), typeNameHash,
                "Type does not have a converter.");
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Bool)]
        private static object ForBoolBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForBool(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Bool)]
        public static bool ForBool(DataRow row, PostgresClientState state)
        {
            if (row.Data.Length != 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            switch (row.Data[0])
            {
                case (byte)'t': return true;
                case (byte)'f': return false;
            }

            throw new ArgumentOutOfRangeException();
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Text)]
        private static object ForStringBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForString(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Text)]
        public static string ForString(DataRow row, PostgresClientState state)
        {
            if (row.Data.Length == 0)
            {
                return "";
            }

            return state.ServerEncoding.GetString(row.Data, 0, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Int2)]
        private static object ForInt2Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt2(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Int2)]
        public static short ForInt2(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToShort(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Int4)]
        private static object ForInt4Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt4(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Int4)]
        public static int ForInt4(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToInt(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Int8)]
        private static object ForInt8Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt8(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Int8)]
        public static long ForInt8(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToLong(row.Data, 0, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Money)]
        private static object ForMoneyBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForMoney(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Money)]
        public static decimal ForMoney(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Numeric)]
        private static object ForNumericBoxing(
            DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Numeric)]
        public static decimal ForNumeric(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        public static decimal ForDecimal(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Float4)]
        private static object ForFloat4Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForFloat4(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Float4)]
        public static float ForFloat4(DataRow row, PostgresClientState state)
        {
            return (float)NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Float8)]
        private static object ForFloat8Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForFloat8(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Float8)]
        public static float ForFloat8(DataRow row, PostgresClientState state)
        {
            return (float)NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Uuid)]
        private static object ForUuidBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForUuid(row, state);
        }

        [PostgresTypeConverterMethod(PostgresTypeNames.Uuid)]
        public static Guid ForUuid(DataRow row, PostgresClientState state)
        {
            // TODO: Get rid of the string allocation here.
            var guidString = ForString(row, state);
            return new Guid(guidString);
        }
    }
}
