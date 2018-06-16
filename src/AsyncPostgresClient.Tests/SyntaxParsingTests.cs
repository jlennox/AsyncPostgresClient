using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class SyntaxParsingTests
    {
        private static string[] ParseString(string input)
        {
            var command = new PostgresCommand {
                CommandText = input
            };

            command.PostgresParameters.Add("foobar", "value");
            command.PostgresParameters.Add("baz", "value2");

            return PostgresSqlCommandParser.Perform(null, command).ToArray();
        }

        private static void AssertEscaped(string input, string expected)
        {
            var parsed = ParseString(input);

            Assert.AreEqual(1, parsed.Length);
            Assert.AreEqual(expected, parsed[0]);
        }

        [DataTestMethod]
        // Ensure parameters are rewritten.
        [DataRow(
            "select @foobar",
            "select $1",
            DisplayName = "select @foobar")]
        // Ensure multiple parameters are rewritten.
        [DataRow(
            "select @foobar, @foobar, @baz",
            "select $1, $1, $2",
            DisplayName = "select @foobar, @foobar, @baz")]
        // Ensure parameters inside strings are not rewritten.
        [DataRow(
            "select '@foobar', @foobar",
            "select '@foobar', $1",
            DisplayName = "select '@foobar', @foobar")]
        // Ensure empty strings do not cause issues.
        [DataRow(
            "select '', @foobar",
            "select '', $1",
            DisplayName = "select '', @foobar")]
        // Ensure adjacent strings with newlines inbetween them to not cause issues.
        [DataRow(
            "select 'hello'\n'world', @foobar",
            "select 'hello'\n'world', $1",
            DisplayName = "select 'hello'\n'world', @foobar")]
        // Ensure double quotes does not terminate string.
        [DataRow(
            "select '''@foobar', @foobar",
            "select '''@foobar', $1",
            DisplayName = "select '''@foobar', @foobar")]
        // Ensure escape quotes are detected.
        [DataRow(
            "select E'@foobar', @foobar",
            "select E'@foobar', $1",
            DisplayName = "select E'@foobar', @foobar")]
        // Ensure \' does not terminate string in escape quotes.
        [DataRow(
            "select E'\\'@foobar', @foobar",
            "select E'\\'@foobar', $1",
            DisplayName = "select E'\'@foobar', @foobar")]
        // Ensure escape strings support double quote quoting.
        [DataRow(
            "select E'''@foobar', @foobar",
            "select E'''@foobar', $1",
            DisplayName = "select E'''@foobar', @foobar")]
        // Ensure nameless dollar quotes are detected.
        [DataRow(
            "select $$@foobar$$, @foobar",
            "select $$@foobar$$, $1",
            DisplayName = "select $$@foobar$$, @foobar")]
        // Ensure ; inside dollar quotes does not cause issues.
        [DataRow(
            "select $name$@foobar;$name$, @foobar",
            "select $name$@foobar;$name$, $1",
            DisplayName = "select $name$@foobar;$name$, @foobar")]
        // Ensure nested dollar quotes do not cause issues.
        [DataRow(
            "select $name$@foobar$nested$@foobar'hello\\'@foobar';@foobar$nested$@foobar$name$, @foobar",
            "select $name$@foobar$nested$@foobar'hello\\'@foobar';@foobar$nested$@foobar$name$, $1",
            DisplayName = "select $name$@foobar$nested$@foobar'hello@foobar';@foobar$nested$@foobar$name$, @foobar")]
        // Ensure numeric $ values are left alone.
        [DataRow(
            "select $2, @foobar",
            "select $2, $1",
            DisplayName = "select $2, @foobar")]
        // Ensure paramaters inside -- comment are not rewritten.
        [DataRow(
            "select @foobar -- @foobar",
            "select $1 -- @foobar",
            DisplayName = "select @foobar -- @foobar")]
        // Ensure -- inside string does not cause issues.
        [DataRow(
            "select '@foobar --', @foobar",
            "select '@foobar --', $1",
            DisplayName = "select @foobar -- @foobar")]
        // Ensure parameter inside /**/ comment is not rewritten.
        [DataRow(
            "select @foobar /* @foobar */, @foobar",
            "select $1 /* @foobar */, $1",
            DisplayName = "select @foobar /* @foobar */, @foobar")]
        // Ensure /* inside string does not cause issues.
        [DataRow(
            "select $foobar$/* /* @foobar $foobar$, @foobar",
            "select $foobar$/* /* @foobar $foobar$, $1",
            DisplayName = "select $foobar$/* /* @foobar $foobar$, @foobar")]
        // Ensure /**/ does not cause issues with no contents.
        [DataRow(
            "select 5/**/, @foobar",
            "select 5/**/, $1",
            DisplayName = "select 5/**/, @foobar")]
        // Ensure -- terminates on line ends.
        [DataRow(
            "select @baz, -- @foobar\n@foobar",
            "select $2, -- @foobar\n$1",
            DisplayName = "select @baz, -- @foobar\n@foobar")]
        public void StringsAreSkippedOver(string input, string expected)
        {
            AssertEscaped(input, expected);
        }

        [DataTestMethod]
        // Terminal dollar sign does not exception.
        [DataRow(
            "select $",
            "select $",
            DisplayName = "select $")]
        // Terminal dollar string does not exception.
        [DataRow(
            "select $$",
            "select $$",
            DisplayName = "select $$")]
        // Terminal named dollar string does not exception.
        [DataRow(
            "select $foo$",
            "select $foo$",
            DisplayName = "select $foo$")]
        // Terminal named dollar string does not exception.
        [DataRow(
            "select '",
            "select '",
            DisplayName = "select '")]
        // Terminal escaped string does not cause exception.
        [DataRow(
            "select E'\\'",
            "select E'\\'",
            DisplayName = "select E'\\'")]
        // Terminal multiline comment does not cause exception.
        [DataRow(
            "select /*",
            "select /*",
            DisplayName = "select /*")]
        public void InvalidSyntaxDoesNotBreakRewritter(string input, string expected)
        {
            AssertEscaped(input, expected);
        }

        private static void AssertSplit(string input, params string[] expected)
        {
            var parsed = ParseString(input);

            CollectionAssert.AreEqual(expected, parsed);
        }

        [DataTestMethod]
        public void CommandsAreSplit()
        {
            AssertSplit(
                "select 1; select 2;  ",
                "select 1", "select 2");
        }

        [TestMethod]
        public void ParametersCanBeEscaped()
        {
            AssertEscaped(
                "select @@foobar, @foobar",
                "select @@foobar, $1");
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

        [DataTestMethod]
        [DataRow("INSERT 0 5", 5)]
        [DataRow("INSERT 0 5923", 5923)] // Be sure '9' is tested.
        [DataRow("INSERT 0 5023", 5023)] // As well as '0'.
        [DataRow("INSERT 0 asd", null)]
        [DataRow(null, null)]
        [DataRow("", null)]
        [DataRow("invalid", null)]
        [DataRow("invalid ", null)]
        [DataRow("5", null)]
        public void ParseNumericValueFromNonQueryResponseTests(
            string s, int? expected)
        {
            var result = PostgresCommand
                .ParseNumericValueFromNonQueryResponse(s);

            Assert.AreEqual(expected, result);
        }
    }
}
