using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    internal static class NumericAsserts
    {
        public static void Less<T>(T expectedLarger, T expectedSmaller)
            where T : IComparable
        {
            if (expectedLarger.CompareTo(expectedSmaller) >= 0)
            {
                Assert.Fail($"{expectedLarger} < {expectedSmaller} check failed.");
            }
        }

        public static void FloatEquals(
            double expected,
            double actual,
            decimal precision)
        {
            var diff = (decimal)Math.Abs(expected - actual);
            if (diff > precision)
            {
                Assert.Fail($"Expected {expected} but got {actual} with a difference of {diff} outside of precision {precision}.");
            }
        }
    }
}
