using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lennox.AsyncPostgresClient
{
    public class PostgresDbParameterCollection : DbParameterCollection
    {
        private volatile List<PostgresParameter> _paramaters;

        public PostgresDbParameterCollection()
        {
            _paramaters = /*Interlocked.Exchange(ref _paramsStorage, null) ??*/
                new List<PostgresParameter>();

            _paramaters.Clear();
        }

        public override int Add(object value)
        {
            _paramaters.Add(GetParameter(value));

            return _paramaters.Count;
        }

        private static PostgresParameter GetParameter(object value)
        {
            if (value is PostgresParameter param)
            {
                return param;
            }

            return new PostgresParameter(value);
        }

        public override void Clear()
        {
            _paramaters.Clear();
        }

        public override bool Contains(object value)
        {
            return _paramaters.Any(t => t.Value == value);
        }

        public override int IndexOf(object value)
        {
            for (var i = 0; i < _paramaters.Count; ++i)
            {
                if (_paramaters[i].Value == value)
                {
                    return i;
                }
            }

            return -1;
        }

        public override void Insert(int index, object value)
        {
            _paramaters.Insert(index, GetParameter(value));
        }

        public override void Remove(object value)
        {
            var indexOf = IndexOf(value);

            if (indexOf == -1)
            {
                return;
            }

            RemoveAt(indexOf);
        }

        public override void RemoveAt(int index)
        {
            _paramaters.RemoveAt(index);
        }

        public override void RemoveAt(string parameterName)
        {
            var indexOf = IndexOf(parameterName);

            if (indexOf == -1)
            {
                return;
            }

            RemoveAt(indexOf);
        }

        protected override void SetParameter(int index, DbParameter value)
        {
            throw new NotImplementedException();
        }

        protected override void SetParameter(
            string parameterName,
            DbParameter value)
        {
            throw new NotImplementedException();
        }

        public override int Count => _paramaters.Count;
        public override object SyncRoot { get; }

        public override int IndexOf(string parameterName)
        {
            for (var i = 0; i < _paramaters.Count; ++i)
            {
                if (_paramaters[i].ParameterName == parameterName)
                {
                    return i;
                }
            }

            return -1;
        }

        public override bool Contains(string value)
        {
            throw new NotImplementedException();
        }

        public override void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public override IEnumerator GetEnumerator()
        {
            return _paramaters.GetEnumerator();
        }

        protected override DbParameter GetParameter(int index)
        {
            return _paramaters[index];
        }

        protected override DbParameter GetParameter(string parameterName)
        {
            var indexOf = IndexOf(parameterName);

            if (indexOf == -1)
            {
                return null;
            }

            return _paramaters[indexOf];
        }

        public override void AddRange(Array values)
        {
            throw new NotImplementedException();
        }
    }

    internal class PostgresParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName
        {
            get => _name;
            set => _name = CleanName(value);
        }
        public override string SourceColumn { get; set; }
        public override object Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }

        private string _name;

        public PostgresParameter() { }

        public PostgresParameter(object value)
        {
            Value = value;
        }

        public override void ResetDbType()
        {
            throw new NotImplementedException();
        }

        internal static string CleanName(string name)
        {
            if (name == null || name.Length == 0)
            {
                return "";
            }

            switch (name[0])
            {
                case '@':
                case ':':
                    return name.Substring(1);
            }

            return name;
        }
    }
}
