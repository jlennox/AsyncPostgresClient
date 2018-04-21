using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Lennox.AsyncPostgresClient.Diagnostic;
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
            public string Text { get; set; }
        }

        [TestMethod]
        public async Task TestQuery()
        {
            using (var connection = await PostgresServerInformation.Open())
            using (var transaction = connection.BeginTransaction())
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

                const string query = @"
                    SELECT *
                    FROM tempUser
                    LEFT JOIN tempUserInfo ON (tempUserInfo.user_id = tempUser.id)";

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
                        });

                var users = userLookup.Values.ToArray();

                Assert.AreEqual(3, users.Length);
                var guyOne = users.Single(t => t.Id == 0);
                var guyTwo = users.Single(t => t.Id == 1);
                var guyThree = users.Single(t => t.Id == 2);

                Assert.AreEqual(3, guyOne.Infos.Count);
                Assert.AreEqual(2, guyTwo.Infos.Count);
                Assert.AreEqual(0, guyThree.Infos.Count);

                Assert.IsTrue(guyOne.Infos.Any(t => t.Text == "info one"));
                Assert.IsTrue(guyOne.Infos.Any(t => t.Text == "info two"));
                Assert.IsTrue(guyOne.Infos.Any(t => t.Text == "info three"));
                Assert.IsTrue(guyTwo.Infos.Any(t => t.Text == "info one"));
                Assert.IsTrue(guyTwo.Infos.Any(t => t.Text == "info two"));
            }
        }
    }
}
