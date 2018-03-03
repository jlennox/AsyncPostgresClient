using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class SyntaxParsingTests
    {
        private static string[] ParseString(string input)
        {
            var command = new PostgresCommand();
            command.CommandText = input;

            command.PostgresParameters.Add("foobar", "value");

            return PostgresSqlCommandParser.Perform(null, command).ToArray();
        }

        private static void AssertEscaped(string input, string expected)
        {
            var parsed = ParseString(input);

            Assert.AreEqual(parsed.Length, 1);
            Assert.AreEqual(expected, parsed[0]);
        }

        [TestMethod]
        public void StringsAreSkippedOver()
        {
            AssertEscaped(
                @"select @foobar",
                @"select $1");

            AssertEscaped(
                @"select @foobar, @foobar",
                @"select $1, $1");

            AssertEscaped(
                @"select '@foobar', @foobar",
                @"select '@foobar', $1");

            AssertEscaped(
                @"select '''@foobar', @foobar",
                @"select '''@foobar', $1");

            AssertEscaped(
                @"select E'@foobar', @foobar",
                @"select E'@foobar', $1");

            AssertEscaped(
                @"select E'\'@foobar', @foobar",
                @"select E'\'@foobar', $1");

            AssertEscaped(
                @"select E'''@foobar', @foobar",
                @"select E'''@foobar', $1");

            AssertEscaped(
                @"select $$@foobar$$, @foobar",
                @"select $$@foobar$$, $1");

            AssertEscaped(
                @"select $name$@foobar$name$, @foobar",
                @"select $name$@foobar$name$, $1");

            AssertEscaped(
                @"select $name$@foobar$nested$@foobar$nested$@foobar$name$, @foobar",
                @"select $name$@foobar$nested$@foobar$nested$@foobar$name$, $1");

            AssertEscaped(
                @"select $2, @foobar",
                @"select $2, $1");
        }

        [TestMethod]
        public void ParametersCanBeEscaped()
        {
            AssertEscaped(
                @"select @@foobar, @foobar",
                @"select @@foobar, $1");
        }

        [TestMethod]
        public void DefaultSettingsAreEnforced()
        {
            var e = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                PostgresSqlCommandParser.DemandStandardSettings(new[] {
                    new PostgresPropertySetting(
                        PostgresProperties.BackslashQuote, "off")
                }));

            StringAssert.Contains(
                e.ToString(),
                PostgresProperties.BackslashQuote);

            e = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                PostgresSqlCommandParser.DemandStandardSettings(new[] {
                    new PostgresPropertySetting(
                        PostgresProperties.BackslashQuote, "on")
                }));

            StringAssert.Contains(
                e.ToString(),
                PostgresProperties.BackslashQuote);

            PostgresSqlCommandParser.DemandStandardSettings(new[] {
                new PostgresPropertySetting(
                    PostgresProperties.BackslashQuote, "safe_encoding")
            });

            e = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                PostgresSqlCommandParser.DemandStandardSettings(new[] {
                    new PostgresPropertySetting(
                        PostgresProperties.StandardConformingStrings, "off")
                }));

            StringAssert.Contains(
                e.ToString(),
                PostgresProperties.StandardConformingStrings);

            PostgresSqlCommandParser.DemandStandardSettings(new[] {
                new PostgresPropertySetting(
                    PostgresProperties.StandardConformingStrings, "on")
            });
        }
    }
}
