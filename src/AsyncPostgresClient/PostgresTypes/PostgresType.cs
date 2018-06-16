using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lennox.AsyncPostgresClient.PostgresTypes
{
    // https://github.com/postgres/postgres/tree/master/src/backend/utils/adt
    // select proname, prorettype::regtype from pg_proc where proname like '%recv';

    internal class PostgresTypeCollection
    {
        // https://www.postgresql.org/docs/9.2/static/catalog-pg-type.html
        private const string _query = "SELECT typname, typtype, oid, typarray FROM pg_type;";

        private enum ResultIndex
        {
            Typename = 0,
            Type = 1,
            Oid = 2,
            ArrayType = 3
        }

        public PostgresType[] Types { get; }

        private readonly Dictionary<int, PostgresTypeConverter> _oidCodecLookup;

        private PostgresTypeCollection(List<PostgresType> types)
        {
            Types = types.ToArray();

            var arrayOids = new HashSet<int>(Types.Select(t => t.ArrayOid));

            _oidCodecLookup = types.ToDictionary(
                t => t.Oid,
                t => PostgresTypeConverter.ConverterByName(
                    t.Name,
                    arrayOids.Contains(t.Oid)));
        }

        internal static async Task<PostgresTypeCollection> Create(
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
                            Oid = reader.GetInt32((int)ResultIndex.Oid),
                            ArrayOid = reader.GetInt32((int)ResultIndex.ArrayType)
                        });
                    }

                    return new PostgresTypeCollection(queryResults);
                }
            }
        }

        public Type LookupType(int oid)
        {
            if (!_oidCodecLookup.TryGetValue(oid, out var typeConverter))
            {
                return typeof(object);
            }

            var codec = typeConverter.Codec;

            return codec.SystemType;
        }

        public object Convert(
            int oid,
            DataRow row,
            PostgresFormatCode formatCode,
            PostgresClientState state)
        {
            if (!_oidCodecLookup.TryGetValue(oid, out var typeConverter))
            {
                // TODO
                throw new InvalidOperationException("Unknown oid " + oid);
            }

            var codec = typeConverter.Codec;

            if (codec == null)
            {
                throw new NotImplementedException(
                    $"Convertion of type '{typeConverter.Name}' ({oid}) is not supported.");
            }

            switch (formatCode)
            {
                case PostgresFormatCode.Text:
                    return codec.DecodeTextObject(row, state);
                case PostgresFormatCode.Binary:
                    return codec.DecodeBinaryObject(row, state);
            }

            throw new ArgumentOutOfRangeException(
                nameof(formatCode), formatCode,
                "Unknown format code.");
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
        public int ArrayOid { get; set; }

        private string _name;
        private int _nameHashCode;
    }

    internal static class PostgresTypeNames
    {
        public const string Bool = "bool";
        public const string Text = "text";
        public const string Int2 = "int2";
        public const string Int4 = "int4";
        public const string Int8 = "int8";
        public const string Money = "money";
        public const string Numeric = "numeric";
        public const string Float4 = "float4";
        public const string Float8 = "float8";
        public const string Uuid = "uuid";
        public const string Date = "date";
        public const string Timestamp = "timestamp";
        public const string Int2Vector = "int2vector";
        public const string Byte = "bytea";
        public const string Xml = "xml";
        public const string Json = "json";
        public const string Smgr = "smgr";
        public const string Point = "point";
        public const string Path = "path";
        public const string Box = "box";
        public const string Polygon = "polygon";
        public const string Line = "line";
        public const string AbsoluteTime = "abstime";
        public const string RelativeTime = "reltime";
        public const string Interval = "interval";
        public const string Circle = "circle";
        public const string MacAddress = "macaddr";
        public const string Bpchar = "bpchar";
        public const string VarChar = "varchar";
        public const string Timezone = "timetz";
        public const string Bit = "bit";
        public const string JsonB = "jsonb";
    }

    internal struct PostgresTypeConverter
    {
        public string Name { get; set; }
        public IPostgresTypeCodec Codec { get; set; }
        public bool IsArray { get; set; }

        private static readonly Dictionary<int, IPostgresTypeCodec> _codecLookup;

        static PostgresTypeConverter()
        {
            _codecLookup = CreateLookup();
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

            if (!_codecLookup.TryGetValue(typeNameHash, out var codec))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(typeNameHash), typeNameHash,
                    "Type does not have a converter.");
            }

            switch (formatCode)
            {
                case PostgresFormatCode.Text:
                    return codec.DecodeTextObject(row, state);
                case PostgresFormatCode.Binary:
                    return codec.DecodeBinaryObject(row, state);
            }

            throw new ArgumentOutOfRangeException(
                nameof(formatCode), formatCode,
                "Unknown format code.");
        }

        public static PostgresTypeConverter ConverterByName(
            string postgresTypeName, bool isArray)
        {
            var typeHash = postgresTypeName.GetHashCode();

            _codecLookup.TryGetValue(typeHash, out var codec);

            return new PostgresTypeConverter {
                Name = postgresTypeName,
                Codec = codec,
                IsArray = isArray
            };
        }

        private static Dictionary<int, IPostgresTypeCodec> CreateLookup()
        {
            var assembly = typeof(PostgresTypeConverterMethodAttribute).Assembly;

            return assembly.GetTypes()
                .Where(t => t.IsClass)
                .Select(t => new {
                    TypeAttribute = t.GetCustomAttribute<
                        PostgresTypeConverterMethodAttribute>(),
                    Type = t
                })
                .Where(t => t.TypeAttribute != null)
                .SelectMany(t => t.TypeAttribute.PostgresTypeNames
                    .Select(q => new {
                        NameHash = q.GetHashCode(),
                        Type = (IPostgresTypeCodec)Activator
                            .CreateInstance(t.Type)
                    }))
                .ToDictionary(
                    t => t.NameHash,
                    t => t.Type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DemandDataLength(DataRow row, int length)
        {
            if (row.Data.Length < length || row.Length != length)
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    // Information on encodings:
    // https://github.com/scrive/hpqtypes/tree/master/libpqtypes/src

    internal static class PostgresDateTime
    {
        // https://doxygen.postgresql.org/datatype_2timestamp_8h_source.html#l00163
        public const int JulianMinYear = -4713;
        public const int JulianMinMonth = 11;
        public const int JulianMinDay = 24;
        public const int JulianMaxYear = 5874898;
        public const int JulianMaxMonth = 6;
        public const int JulianMaxDay = 3;
        public const int UnixEpochJdate = 2440588;
        public const int PostgresEpochJdate = 2451545;
        public const int DateTimeMinJulian = 0;
        public const int DateTimeMaxJulian = 2147483494;
        public const int TimestampMaxJulian = 109203528;
        public const long TimestampMin = -211813488000000000L;
        public const long TimestampMax = 9223371331200000000L;

        // https://github.com/scrive/hpqtypes/blob/master/libpqtypes/src/datetime.c#L1021
        public static DateTime FromJulian(int jd)
        {
            var julian = jd;
            julian += 32044;
            var quad = julian / 146097;
            var extra = (julian - quad * 146097) * 4 + 3;
            julian += 60 + quad * 3 + extra / 146097;
            quad = julian / 1461;
            julian -= quad * 1461;
            var y = julian * 4 / 1461;
            julian = ((y != 0) ? ((julian + 305) % 365) : ((julian + 306) % 366))
                + 123;
            y += quad * 4;
            var year = y - 4800;
            quad = julian * 2141 / 65536;
            var day = julian - 7834 * quad / 256;
            var month = (quad + 10) % 12 + 1;

            return new DateTime(year, month, day, 0, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    internal class PostgresBinaryTypeConverter
    {
        public DateTime ForTimestamp(DataRow row, PostgresClientState state)
        {
            /*const int secondsPerDay = 86400;
            const int secondsPerHour = 3600;
            const int secondsPerMinute = 60;

            var aslong = ForInt8(row, state);
            double asdouble;

            int Modulo(ref double t, int u)
            {
                var q = t < 0
                    ? Math.Ceiling(t / u)
                    : Math.Floor(t / u);

                if (q != 0)
                {
                    t -= Math.Round(q * u, 0);
                }

                return (int)q;
            }

            double Timeround(double j)
            {
                const double timePrecInv = 10000000000.0;
                return Math.Round(j * timePrecInv, 0) / timePrecInv;
            }

            if (state.IntegerDatetimes)
            {
                asdouble = aslong / (double)1000000;
            }
            else
            {
                throw new NotSupportedException(
                    "Only integer datetimes are supported.");
            }

            var rem = asdouble;
            var hour = Modulo(ref rem, secondsPerHour);
            var minutes = Modulo(ref rem, secondsPerMinute);
            var seconds = Modulo(ref rem, 1);
            rem = Timeround(rem);

            // There's a weird goto here that should logically infinite loop?

            var millisec = rem * 1000;

            return new TimeSpan(0, hour, minutes, seconds, (int)millisec);*/
            throw new NotImplementedException();
        }
    }
}