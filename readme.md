## About

AsyncPostgresClient is an ADO.NET client for [PostgreSQL](https://www.postgresql.org/)
written in C# and is intended for use with [Dapper](https://github.com/StackExchange/Dapper)
or other ORMs. It is open source and free.

At this time the project is in the initial development phase and should not be used.

The goals for this project are:
* Minimal external dependencies.
* Correct usage of async.
* Is usable in production.
* Minimizes allocations.
* Does what I need it to.

Because of the "does what I need it to" goal it may or may not be suitable for
all uses. This is a project done in my free time. If a requirement is missing,
please open an issue on github and I may (or may not) implement it. As always,
pull requests welcome.

## Missing features

Some of these maybe implemented at a future date.

* SequentialAccess: This is important for accessing large binary data in a database, such as images.
* Array support.
* The long tail of types.
* Likely many other things.

## What is supported?

* Implementations of: IDbConnection, IDataReader, IDbCommand
* Parameterized queries using `@name`
* Types: bool, text, int, int4, int8, money, numeric, float4, float8, uuid, date, timestamp, varchar.

## Why not use Npgsql?

The answer is you very likely should. [Npgsql](http://www.npgsql.org/) is a
mature codebase and likely does what you want and does it better.

AsyncPostgresClient was created as an alternative because Npgsql's
implementation of async has numerous deadlocks during threadpool exhaustion,
which prevents it from being usable in highly concurrent/high reliability
services.