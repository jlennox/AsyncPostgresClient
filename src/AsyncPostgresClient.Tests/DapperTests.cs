using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.AsyncPostgresClient.Tests
{
    [TestClass]
    public class DapperTests
    {
        [TestMethod]
        public async Task TestDapperSelectOne()
        {
            using (var connection = await PostgresServerInformation.Open())
            {
                var one = await connection.QueryAsync<int>("SELECT 1");

                CollectionAssert.AreEqual(new[] { 1 }, one.ToArray());
            }
        }

        class TheOne
        {
            public int AOne { get; set; }
        }

        [TestMethod]
        public async Task TestDapperSelectOneParameter()
        {
            using (var connection = await PostgresServerInformation.Open())
            {
                var one = await connection.QueryAsync<TheOne>(
                    "SELECT AOne FROM (SELECT 1 AS AOne) s WHERE AOne = @AOne", new {
                        AOne = 1
                    });

                var results = one.ToArray();
                CollectionAssert.AreEqual(new[] { 1 }, results);
            }
        }

        [TestMethod]
        public async Task TestExecuteScalarAsync()
        {
            var cancel = CancellationToken.None;

            using (var connection = await PostgresServerInformation.Open())
            using (var command = new PostgresCommand("SELECT 1", connection))
            {
                var one = await command.ExecuteScalarAsync(cancel);
                Assert.AreEqual(1, one);
            }
        }

        class TempUser
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Location { get; set; }

            public Collection<TempUserInfo> Infos { get; set; }

            public TempUser()
            {
                Infos = new Collection<TempUserInfo>();
            }
        }

        class TempUserInfo
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string Info { get; set; }
        }

        private static async Task CreateTempUsers(
            PostgresDbConnection connection)
        {
            const string schema = @"
                CREATE TEMP TABLE tempUser (id int4, name text, location text);

                INSERT INTO tempUser (id, name, location) VALUES
                (0, 'guy one', 'Mars'),
                (1, 'guy two', 'Jupiter'),
                (2, 'guy three', 'Venus');

                CREATE TEMP TABLE tempUserInfo (id int4, user_id int4, info text);

                INSERT INTO tempUserInfo (id, user_id, info) VALUES
                (0, 0, 'info one'),
                (1, 0, 'info two'),
                (2, 0, 'info three'),
                (3, 1, 'info one'),
                (4, 1, 'info two');";

            await connection.ExecuteAsync(schema);
        }

        private static async Task<TempUser[]> ReadTempUsers(
            PostgresDbConnection connection, string query, object param)
        {
            var userLookup = new Dictionary<int, TempUser>();

            await connection
                .QueryAsync<TempUser, TempUserInfo, TempUser>(
                    query, (user, info) => {
                        if (!userLookup.TryGetValue(user.Id, out var found))
                        {
                            found = user;
                            userLookup[user.Id] = user;
                        }

                        if (info != null)
                        {
                            found.Infos.Add(info);
                        }

                        return found;
                    }, param);

            return userLookup.Values.ToArray();
        }

        [TestMethod]
        public async Task TestQuery()
        {
            using (var connection = await PostgresServerInformation.Open())
            using (var transaction = connection.BeginTransaction())
            {
                await CreateTempUsers(connection);

                const string query = @"
                    SELECT *
                    FROM tempUser
                    LEFT JOIN tempUserInfo ON (tempUserInfo.user_id = tempUser.id)";

                var users = await ReadTempUsers(connection, query, null);

                Assert.AreEqual(3, users.Length);
                var guyOne = users.Single(t => t.Id == 0);
                var guyTwo = users.Single(t => t.Id == 1);
                var guyThree = users.Single(t => t.Id == 2);

                Assert.AreEqual(3, guyOne.Infos.Count);
                Assert.AreEqual(2, guyTwo.Infos.Count);
                Assert.AreEqual(0, guyThree.Infos.Count);

                Assert.AreEqual("guy one", guyOne.Name);
                Assert.AreEqual("guy two", guyTwo.Name);
                Assert.AreEqual("guy three", guyThree.Name);

                Assert.AreEqual("Mars", guyOne.Location);
                Assert.AreEqual("Jupiter", guyTwo.Location);
                Assert.AreEqual("Venus", guyThree.Location);

                Assert.IsTrue(guyOne.Infos.Any(t => t.Info == "info one"));
                Assert.IsTrue(guyOne.Infos.Any(t => t.Info == "info two"));
                Assert.IsTrue(guyOne.Infos.Any(t => t.Info == "info three"));
                Assert.IsTrue(guyTwo.Infos.Any(t => t.Info == "info one"));
                Assert.IsTrue(guyTwo.Infos.Any(t => t.Info == "info two"));
            }
        }

        [TestMethod]
        public async Task TestParameterizedQuery()
        {
            using (var connection = await PostgresServerInformation.Open())
            using (var transaction = connection.BeginTransaction())
            {
                await CreateTempUsers(connection);

                const string query = @"
                    SELECT *
                    FROM tempUser
                    LEFT JOIN tempUserInfo ON (tempUserInfo.user_id = tempUser.id)";

                {
                    const string withWhere = query + @"
                        WHERE tempUser.id = @Id";

                    var users = await ReadTempUsers(
                        connection, withWhere, new {
                            Id = 1
                        });

                    Assert.AreEqual(1, users.Length);
                    Assert.AreEqual("guy two", users.Single().Name);
                }

                {
                    const string withWhere = query + @"
                        WHERE tempUser.name = @Name";

                    var users = await ReadTempUsers(
                        connection, withWhere, new {
                            Name = "guy two"
                        });

                    Assert.AreEqual(1, users.Length);
                    Assert.AreEqual("guy two", users.Single().Name);

                    var noResults = await ReadTempUsers(
                        connection, withWhere, new {
                            Name = "Invalid name"
                        });

                    Assert.AreEqual(0, noResults.Length);

                    var nullResults = await ReadTempUsers(
                        connection, withWhere, new {
                            Name = (string)null
                        });

                    Assert.AreEqual(0, nullResults.Length);

                    var escapeAttempts = await ReadTempUsers(
                        connection, withWhere, new {
                            Name = "\"'$FOOBVAR$$$"
                        });

                    Assert.AreEqual(0, escapeAttempts.Length);
                }
            }
        }

        [TestMethod]
        public async Task ArrayParameterTest()
        {
            using (var connection = await PostgresServerInformation.Open())
            using (var transaction = connection.BeginTransaction())
            {
                await CreateTempUsers(connection);

                const string query = @"
                    SELECT *
                    FROM tempUser
                    LEFT JOIN tempUserInfo IN (@Ids)
                    WHERE tempUser.id IN (@Ids)";

                var users = await ReadTempUsers(
                    connection, query, new {
                        Ids = new[] { 1, 2 }
                    });

                Assert.AreEqual(2, users.Length);

                var guyOne = users.Single(t => t.Id == 0);
                var guyTwo = users.Single(t => t.Id == 1);

                Assert.AreEqual(3, guyOne.Infos.Count);
                Assert.AreEqual(2, guyTwo.Infos.Count);
            }
        }

        private struct TempArray
        {
            public int Id { get; set; }
            public int[] Numbers { get; set; }
            public string[][] Texts { get; set; }
        }

        [TestMethod]
        public async Task ArrayReaderTest()
        {
            using (var connection = await PostgresServerInformation.Open())
            using (var transaction = connection.BeginTransaction())
            {
                const string schema = @"
                    CREATE TEMP TABLE tempArray (id int4, numbers integer[], texts text[][]);

                    INSERT INTO tempArray
                        VALUES (0,
                        '{10000, 10000, 10000, 10000}',
                        '{{""meeting"", ""lunch""}, {""training"", ""presentation""}}');

                    INSERT INTO tempArray
                        VALUES (1,
                        '{10001, 10002, 10003, 10004}',
                        '{{""meeting"", ""lunch""}, {""training"", ""presentation""}}');

                    INSERT INTO tempArray
                        VALUES (2,
                        '{10001, 10002, 10003, 10004}',
                        '{{""meeting"", ""lunch""}, {""training"", ""presentation""}}');

                    INSERT INTO tempArray
                        VALUES (3,
                        '{}',
                        '{{""meeting"", ""lunch""}, {null, null}}');

                    INSERT INTO tempArray
                        VALUES (4,
                        null,
                        null);";

                await connection.ExecuteAsync(schema);

                const string query = @"
                    SELECT *
                    FROM tempArray";

                var data = (await connection
                    .QueryAsync<TempArray>(query)).ToArray();

                Assert.AreEqual(5, data.Length);

                var guy0 = data.Single(t => t.Id == 0);
                var guy1 = data.Single(t => t.Id == 1);
                var guy2 = data.Single(t => t.Id == 2);
                var guy3 = data.Single(t => t.Id == 3);
                var guy4 = data.Single(t => t.Id == 4);

                void AssertNumbers(TempArray guy, int[] expected)
                {
                    Assert.AreEqual(expected.Length, guy.Numbers.Length);
                    CollectionAssert.AreEqual(expected, guy.Numbers);
                }

                AssertNumbers(guy0, new[] { 10000, 10000, 10000, 10000 });
                AssertNumbers(guy1, new[] { 10001, 10002, 10003, 10004 });
                AssertNumbers(guy2, new[] { 10001, 10002, 10003, 10004 });

                Assert.AreEqual(0, guy3.Numbers.Length);
                Assert.AreEqual(null, guy4.Numbers);
            }
        }
    }
}
