using Microsoft.Data.SqlClient;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace NeighborGoods.Api.Tests;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string TestDatabaseName = "NeighborGoodsTests";
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Testcontainers_Strong_Password_123!")
        .Build();

    public string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
            {
                InitialCatalog = TestDatabaseName
            };
            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID('{TestDatabaseName}') IS NULL
                CREATE DATABASE [{TestDatabaseName}];
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
