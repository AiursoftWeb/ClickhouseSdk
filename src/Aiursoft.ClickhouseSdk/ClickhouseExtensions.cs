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
    /// <param name="orderByColumn">The column name used for ORDER BY in the MergeTree engine.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task InitClickhouseTableAsync<T>(this IServiceProvider serviceProvider, string orderByColumn)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<ClickhouseOptions>>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ClickhouseInitializer");

        if (!options.CurrentValue.Enabled)
        {
            return;
        }

        var connectionStringBuilder = new ClickHouseConnectionStringBuilder(options.CurrentValue.ConnectionString);
        var targetDatabase = connectionStringBuilder.Database;
        var tableName = connectionStringBuilder.TryGetValue("Table", out var tableObj) 
            ? tableObj.ToString() 
            : "Logs";

        try
        {
            // Step 1: Ensure database exists.
            var initBuilder = new ClickHouseConnectionStringBuilder(options.CurrentValue.ConnectionString);
            initBuilder.Remove("Table");

            if (!string.IsNullOrEmpty(targetDatabase))
            {
                initBuilder.Database = "default";
                await using var initConnection = new ClickHouseConnection(initBuilder.ConnectionString);
                await initConnection.OpenAsync();
                await using var dbCommand = initConnection.CreateCommand();
                dbCommand.CommandText = $"CREATE DATABASE IF NOT EXISTS {targetDatabase}";
                await dbCommand.ExecuteNonQueryAsync();
                logger.LogInformation("Database '{DatabaseName}' checked/created.", targetDatabase);
            }

            // Step 2: Initialize table.
            var tableInitBuilder = new ClickHouseConnectionStringBuilder(options.CurrentValue.ConnectionString);
            tableInitBuilder.Remove("Table");
            await using var connection = new ClickHouseConnection(tableInitBuilder.ConnectionString);
            await connection.OpenAsync();
            
            var properties = typeof(T).GetProperties();
            var columns = new List<string>();

            foreach (var prop in properties)
            {
                var chType = MapClrTypeToChType(prop.PropertyType);
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
                var chType = MapClrTypeToChType(prop.PropertyType);
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

    private static string MapClrTypeToChType(Type type)
    {
        return type switch
        {
            _ when type == typeof(string) => "String",
            _ when type == typeof(int) => "Int32",
            _ when type == typeof(uint) => "UInt32",
            _ when type == typeof(long) => "Int64",
            _ when type == typeof(ulong) => "UInt64",
            _ when type == typeof(float) => "Float32",
            _ when type == typeof(double) => "Float64",
            _ when type == typeof(bool) => "UInt8",
            _ when type == typeof(DateTime) => "DateTime",
            _ when type == typeof(Guid) => "UUID",
            _ => "String"
        };
    }
}
