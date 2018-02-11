using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Columns;
using BenchmarkDotNet.Attributes.Exporters;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using KVDictionary = System.Collections.Generic.Dictionary<string, Lennox.AsyncPostgresClient.Tests.Benchmarks.IsDictionaryOrEnumerationFaster.KeyValueStorage>;

namespace Lennox.AsyncPostgresClient.Tests.Benchmarks
{
    [TestClass]
    public class IsDictionaryOrEnumerationFasterRunner
    {
        [TestMethod]
        public void Run()
        {
            var summary = BenchmarkRunner
                .Run(typeof(IsDictionaryOrEnumerationFaster));
        }
    }

    [SimpleJob]
    //[RPlotExporter, RankColumn]
    public class IsDictionaryOrEnumerationFaster
    {
        public struct KeyValueStorage
        {
            public string Name { get; set; }
            public int Index { get; set; }
        }

        [ParamsSource(nameof(ItemCountRange))]
        public int ItemCount;

        public static IEnumerable<int> ItemCountRange =>
            Enumerable.Range(0, 100);

        [Params(0, .5, 1)]
        public int KeyDistance;

        private const string _keyPrefix = "HelloWorld";

        private KeyValueStorage[] _items;
        private KVDictionary _dictionary;
        private KVDictionary _caseInsensitiveDictionary;

        private string _key;

        [GlobalSetup]
        public void Setup()
        {
            _items = new KeyValueStorage[ItemCount];

            for (var i = 0; i < ItemCount; ++i)
            {
                _items[i] = new KeyValueStorage {
                    Name = _keyPrefix + i,
                    Index = i
                };
            }

            _dictionary = _items.ToDictionary(t => t.Name);
            _caseInsensitiveDictionary = new KVDictionary(
                _dictionary.ToArray(),
                StringComparer.OrdinalIgnoreCase);

            if (ItemCount == 0)
            {
                _key = _keyPrefix + 0;
            }
            else
            {
                if (KeyDistance == 1)
                {
                    _key = _keyPrefix + (ItemCount - 1);
                }
                else
                {
                    _key = _keyPrefix + (ItemCount * KeyDistance);
                }
                
            }
        }

        [Benchmark]
        public KeyValueStorage DictionaryLoopup()
        {
            if (!_dictionary.TryGetValue(_key, out var item))
            {
                return ThrowIfEmpty();
            }

            return item;
        }

        [Benchmark]
        public KeyValueStorage CaseInsensitiveDictionaryLoopup()
        {
            if (!_caseInsensitiveDictionary.TryGetValue(_key, out var item))
            {
                return ThrowIfEmpty();
            }

            return item;
        }

        [Benchmark]
        public KeyValueStorage EnumerationFor()
        {
            for (var i = 0; i < _items.Length; ++i)
            {
                if (_items[i].Name == _key)
                {
                    return _items[i];
                }
            }

            return ThrowIfEmpty();
        }

        [Benchmark]
        public KeyValueStorage EnumerationForEach()
        {
            foreach (var item in _items)
            {
                if (item.Name == _key)
                {
                    return item;
                }
            }

            return ThrowIfEmpty();
        }

        private KeyValueStorage ThrowIfEmpty()
        {
            if (_items.Length > 0)
            {
                throw new IndexOutOfRangeException();
            }

            return default(KeyValueStorage);
        }
    }
}
