# EF Core Ephemeral LocalDB Instance

In this repository I prototype spinning up an ephemeral SQL Server LocalDB instance for EF Core.
This should be useful as a replacement for the in-memory provider most commonly used for tests.
The advantage of using this over it should be the option of `ExecuteSql`/`FromSql`, views, UDFs etc.

It works by invoking [`sqllocaldb`](https://docs.microsoft.com/en-us/sql/tools/sqllocaldb-utility)
to create and delete the instance. The instance ben be scoped to both a file name and an assembly
name, so one DB per executable path or per any instance of application on the system, respectively.

- [ ] Redo to be an extension to the options builder (`UseLocalDb(scope)`)

Creating and deleting the database will be encapsulated in the `UseLocalDb` extension method if it
is possible to hook into the DB context destructor from there. We need that to be able to release
the SQL LocalDB instance. Additionally, for all but `NewGuid` scopes we will need to make sure the
context is last so that we do not stop and delete the instance prematurely.

- [ ] Consider removing the idea of the scope entirely and provide user just with the methods

Tests are usually ran not only locally, but also as a part of continuous integration / delivery.
I aim to also find out if SQL Server LocalDB can be installed into Azure Pipelines so that this
solution could work in both a developer's local environment as well as the remote build scenario.

- [ ] Find out if SQL Server LocalDB can be installed into an Azure Pipeline without agent image
- [ ] Find out how to provide it as a baseline tool in the agent image to avoid installing per run
