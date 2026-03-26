using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.ClickhouseSdk;

/// <summary>
/// Provides extension methods for ClickHouse initialization and management.
/// </summary>
public static class ClickhouseExtensions
{
    /// <summary>
    /// Initializes a ClickHouse table based on the provided entity type.
    /// Automatically creates the database and ensures all columns exist.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="serviceProvider">The DI service provider.</param>
    /// <param name="tableName">The name of the target table.</param>
    /// <param name="orderByColumn">The column name used for ORDER BY in the MergeTree engine.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task InitClickhouseTableAsync<T>(this IServiceProvider serviceProvider, string tableName, string orderByColumn)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<ClickhouseOptions>>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ClickhouseInitializer");

        if (!options.CurrentValue.Enabled)
        {
            return;
        }

        var connectionString = options.CurrentValue.ConnectionString;
        var targetDatabase = ClickhouseConnectionUtility.GetDatabaseName(connectionString);

        try
        {
            // Step 1: Ensure database exists.
            var initConnectionString = ClickhouseConnectionUtility.GetInitConnectionString(connectionString);
            await using var initConnection = new ClickHouseConnection(initConnectionString);
            await initConnection.OpenAsync();
            await using var dbCommand = initConnection.CreateCommand();
            dbCommand.CommandText = $"CREATE DATABASE IF NOT EXISTS {targetDatabase}";
            await dbCommand.ExecuteNonQueryAsync();
            logger.LogInformation("Database '{DatabaseName}' checked/created.", targetDatabase);

            // Step 2: Initialize table.
            await using var connection = new ClickHouseConnection(connectionString);
            await connection.OpenAsync();
            
            var properties = typeof(T).GetProperties();
            var columns = new List<string>();

            foreach (var prop in properties)
            {
                var chType = ClickhouseTypeMapper.MapClrTypeToChType(prop.PropertyType);
                columns.Add($"{prop.Name} {chType}");
            }

            var createTableSql = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    {string.Join(",\n                    ", columns)}
                ) ENGINE = MergeTree()
                ORDER BY {orderByColumn}";
            
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = createTableSql;
                await command.ExecuteNonQueryAsync();
            }

            // Step 3: Schema evolution - check for missing columns.
            foreach (var prop in properties)
            {
                var chType = ClickhouseTypeMapper.MapClrTypeToChType(prop.PropertyType);
                var alterSql = $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {prop.Name} {chType}";
                await using var command = connection.CreateCommand();
                command.CommandText = alterSql;
                await command.ExecuteNonQueryAsync();
            }

            logger.LogInformation("Clickhouse table '{TableName}' initialized and schema updated.", tableName);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to initialize Clickhouse table.");
        }
    }
}
