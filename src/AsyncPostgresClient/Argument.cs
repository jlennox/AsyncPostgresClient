using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Global
namespace Lennox.AsyncPostgresClient
{
    internal static class Argument
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HasValue<T>(string name, T value)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HasValue<T>(string name, T value, string message)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name, message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsEqual<T>(string name, T expected, T actual, string message)
            where T : IEquatable<T>
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new ArgumentOutOfRangeException(name, actual, message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsEqual<T>(string name, T expected, T actual)
            where T : IEquatable<T>
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new ArgumentOutOfRangeException(name, actual,
                    $"Unexpected value. Expected '{expected}', received '{actual}'");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsAtLeast<T>(string name, T expected, T actual)
            where T : IComparable<T>
        {
            var result = expected.CompareTo(actual);

            if (result > 0)
            {
                throw new ArgumentOutOfRangeException(name, actual,
                    $"Unexpected value. Expected at least '{expected}', received '{actual}' ({result})");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsAtLeast(string name, int expected, int actual)
        {
            if (actual < expected)
            {
                throw new ArgumentOutOfRangeException(name, actual,
                    $"Unexpected value. Expected at least '{expected}', received '{actual}'");
            }
        }
    }
}
