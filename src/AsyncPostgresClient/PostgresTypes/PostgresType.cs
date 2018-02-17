using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.BufferAccess;
using Lennox.AsyncPostgresClient.Exceptions;

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

        private readonly Dictionary<int, PostgresTypeConverter> _oidTextLookup;
        private readonly Dictionary<int, PostgresTypeConverter> _oidBinLookup;

        private PostgresTypeCollection(List<PostgresType> types)
        {
            Types = types.ToArray();

            _oidTextLookup = types.ToDictionary(
                t => t.Oid,
                t => PostgresTypeConverter.ConverterByName(
                    PostgresFormatCode.Text, t.Name));

            _oidBinLookup = types.ToDictionary(
                t => t.Oid,
                t => PostgresTypeConverter.ConverterByName(
                    PostgresFormatCode.Binary, t.Name));
        }

        private Dictionary<int, PostgresTypeConverter> GetLookup(
            PostgresFormatCode formatCode)
        {
            switch (formatCode)
            {
                case PostgresFormatCode.Text:
                    return _oidTextLookup;
                case PostgresFormatCode.Binary:
                    return _oidBinLookup;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(formatCode), formatCode,
                        "Unknown format code.");
            }
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

        public object Convert(
            int oid,
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            var lookup = GetLookup(formatCode);

            if (lookup.TryGetValue(oid, out var typeConverter))
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

    internal struct PostgresTypeConverter
    {
        public delegate object ConvertDelegate(
            DataRow row, PostgresClientState state);

        public string Name { get; set; }
        public ConvertDelegate Converter { get; set; }

        private static readonly Dictionary<int, ConvertDelegate> _textLookup;
        private static readonly Dictionary<int, ConvertDelegate> _binLookup;

        static PostgresTypeConverter()
        {
            _textLookup = CreateLookup(PostgresTextTypeConverter.Default);
            _binLookup = CreateLookup(PostgresBinaryTypeConverter.Default);
        }

        private static Dictionary<int, ConvertDelegate> GetLookup(
            PostgresFormatCode formatCode)
        {
            switch (formatCode)
            {
                case PostgresFormatCode.Text:
                    return _textLookup;
                case PostgresFormatCode.Binary:
                    return _binLookup;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(formatCode), formatCode,
                        "Unknown format code.");
            }
        }

        public static object Convert(
            int typeNameHash,
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            if (row.IsNull)
            {
                return DBNull.Value;
            }

            var lookup = GetLookup(formatCode);

            if (lookup.TryGetValue(typeNameHash, out var converter))
            {
                return converter(row, state);
            }

            throw new ArgumentOutOfRangeException(
                nameof(typeNameHash), typeNameHash,
                "Type does not have a converter.");
        }

        public static PostgresTypeConverter ConverterByName(
            PostgresFormatCode formatCode,
            string postgresTypeName)
        {
            var typeHash = postgresTypeName.GetHashCode();

            var lookup = GetLookup(formatCode);
            lookup.TryGetValue(typeHash, out var converter);

            return new PostgresTypeConverter {
                Name = postgresTypeName,
                Converter = converter
            };
        }

        private static Dictionary<int, ConvertDelegate> CreateLookup(
            IPostgresTypeBoxingConverter target)
        {
            // This could be a lot cleaner.
            var convension = typeof(IPostgresTypeBoxingConverter).GetMethods()
                .Where(t => t.ReturnType == typeof(object))
                .Select(t => new {
                    Method = t,
                    Attribute = t.GetCustomAttribute<
                        PostgresTypeConverterMethodAttribute>()
                })
                .Where(t => t.Attribute != null)
                .ToDictionary(
                    t => t.Method.Name,
                    t => t.Attribute.PostgresTypeName);

            var targetType = target.GetType();
            const BindingFlags methodBinding =
                BindingFlags.Instance | BindingFlags.NonPublic;

            return convension
                .ToDictionary(
                    t => convension[t.Key].GetHashCode(),
                    t =>
                    {
                        var fullyQualifiedMethodName =
                            $"{typeof(IPostgresTypeBoxingConverter).FullName}.{t.Key}";

                        return Delegate.CreateDelegate(
                            typeof(ConvertDelegate),
                            target,
                            targetType.GetMethod(
                                fullyQualifiedMethodName,
                                methodBinding)) as ConvertDelegate;
                    });
        }

        private static IPostgresTypeConverter GetCovnerter(
            PostgresFormatCode formatCode)
        {
            switch (formatCode)
            {
                case PostgresFormatCode.Binary:
                    return PostgresBinaryTypeConverter.Default;
                case PostgresFormatCode.Text:
                    return PostgresTextTypeConverter.Default;
            }

            BadFormatCode(formatCode);
            throw new UnreachableCodeException();
        }

        public static bool ForBool(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForBool(row, state);
        }

        public static string ForString(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForString(row, state);
        }

        public static short ForInt2(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForInt2(row, state);
        }

        public static int ForInt4(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForInt4(row, state);
        }

        public static long ForInt8(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForInt8(row, state);
        }

        public static decimal ForDecimal(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForDecimal(row, state);
        }

        public static float ForFloat8(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForFloat8(row, state);
        }

        public static Guid ForUuid(
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            return GetCovnerter(formatCode).ForUuid(row, state);
        }

        private static void BadFormatCode(PostgresFormatCode formatCode)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatCode), formatCode,
                "Unknown format code.");
        }
    }

    // There's two code paths. IPostgresTypeConverter is to avoid the boxing
    // of converting the result to an object prematurely. The reason
    // IPostgresTypeBoxingConverter is specific is to allow creation of a
    // delegate. The delegate is much faster than using MethodInfo based
    // invoke, but still slower than what should ultimately be the solution --
    // IPostgresTypeBoxingConverter is desolved and has automatic IL generated.
    internal interface IPostgresTypeConverter
    {
        [PostgresTypeConverterMethod(PostgresTypeNames.Bool)]
        bool ForBool(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Text)]
        string ForString(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Int2)]
        short ForInt2(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Int4)]
        int ForInt4(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Int8)]
        long ForInt8(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Money)]
        decimal ForMoney(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Numeric)]
        decimal ForNumeric(DataRow row, PostgresClientState state);
        decimal ForDecimal(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Float4)]
        float ForFloat4(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Float8)]
        float ForFloat8(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Uuid)]
        Guid ForUuid(DataRow row, PostgresClientState state);
    }

    internal interface IPostgresTypeBoxingConverter
    {
        [PostgresTypeConverterMethod(PostgresTypeNames.Bool)]
        object ForBoolBoxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Text)]
        object ForStringBoxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Int2)]
        object ForInt2Boxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Int4)]
        object ForInt4Boxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Int8)]
        object ForInt8Boxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Money)]
        object ForMoneyBoxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Numeric)]
        object ForNumericBoxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Float4)]
        object ForFloat4Boxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Float8)]
        object ForFloat8Boxing(DataRow row, PostgresClientState state);
        [PostgresTypeConverterMethod(PostgresTypeNames.Uuid)]
        object ForUuidBoxing(DataRow row, PostgresClientState state);
    }

    internal class PostgresBinaryTypeConverter :
        IPostgresTypeConverter, IPostgresTypeBoxingConverter
    {
        public static PostgresBinaryTypeConverter Default =
            new PostgresBinaryTypeConverter();

        object IPostgresTypeBoxingConverter.ForBoolBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForBool(row, state);
        }

        public bool ForBool(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForStringBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForString(row, state);
        }

        public string ForString(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForInt2Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt2(row, state);
        }

        public short ForInt2(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForInt4Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt4(row, state);
        }

        public int ForInt4(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForInt8Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt8(row, state);
        }

        public long ForInt8(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForMoneyBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForMoney(row, state);
        }

        public decimal ForMoney(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForNumericBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForNumeric(row, state);
        }

        public decimal ForNumeric(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        public decimal ForDecimal(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForFloat4Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForFloat4(row, state);
        }

        public float ForFloat4(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForFloat8Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForFloat8(row, state);
        }

        public float ForFloat8(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }

        object IPostgresTypeBoxingConverter.ForUuidBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForUuid(row, state);
        }

        public Guid ForUuid(DataRow row, PostgresClientState state)
        {
            throw new NotImplementedException();
        }
    }

    internal class PostgresTextTypeConverter :
        IPostgresTypeConverter, IPostgresTypeBoxingConverter
    {
        public static PostgresTextTypeConverter Default =
            new PostgresTextTypeConverter();

        object IPostgresTypeBoxingConverter.ForBoolBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForBool(row, state);
        }

        public bool ForBool(DataRow row, PostgresClientState state)
        {
            if (row.Data.Length != 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            switch (row.Data[0])
            {
                case (byte)'t':
                    return true;
                case (byte)'f':
                    return false;
            }

            throw new ArgumentOutOfRangeException();
        }

        object IPostgresTypeBoxingConverter.ForStringBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForString(row, state);
        }

        public string ForString(DataRow row, PostgresClientState state)
        {
            if (row.Data.Length == 0)
            {
                return "";
            }

            return state.ServerEncoding.GetString(row.Data, 0, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForInt2Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt2(row, state);
        }

        public short ForInt2(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToShort(row.Data, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForInt4Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt4(row, state);
        }

        public int ForInt4(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToInt(row.Data, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForInt8Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForInt8(row, state);
        }

        public long ForInt8(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToLong(row.Data, 0, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForMoneyBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForMoney(row, state);
        }

        public decimal ForMoney(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForNumericBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForNumeric(row, state);
        }

        public decimal ForNumeric(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        public decimal ForDecimal(DataRow row, PostgresClientState state)
        {
            return NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForFloat4Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForFloat4(row, state);
        }

        public float ForFloat4(DataRow row, PostgresClientState state)
        {
            return (float)NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForFloat8Boxing(
            DataRow row, PostgresClientState state)
        {
            return ForFloat8(row, state);
        }

        public float ForFloat8(DataRow row, PostgresClientState state)
        {
            return (float)NumericBuffer.AsciiToDecimal(row.Data, row.Length);
        }

        object IPostgresTypeBoxingConverter.ForUuidBoxing(
            DataRow row, PostgresClientState state)
        {
            return ForUuid(row, state);
        }

        public Guid ForUuid(DataRow row, PostgresClientState state)
        {
            // TODO: Get rid of the string allocation here.
            var guidString = ForString(row, state);
            return new Guid(guidString);
        }
    }
}