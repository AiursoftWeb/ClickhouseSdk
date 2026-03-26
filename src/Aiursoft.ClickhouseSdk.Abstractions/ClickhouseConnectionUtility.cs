using ClickHouse.Client.ADO;

namespace Aiursoft.ClickhouseSdk.Abstractions;

/// <summary>
/// A utility for working with ClickHouse connection strings.
/// </summary>
public static class ClickhouseConnectionUtility
{
    /// <summary>
    /// Extract the database name from a ClickHouse connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The database name, or "default" if not specified.</returns>
    public static string GetDatabaseName(string connectionString)
    {
        var builder = new ClickHouseConnectionStringBuilder(connectionString);
        return string.IsNullOrEmpty(builder.Database) ? "default" : builder.Database;
    }

    /// <summary>
    /// Creates an initialization connection string for ClickHouse. 
    /// This removes the 'Database' parameter to ensure we can connect and create the database.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The initialization connection string.</returns>
    public static string GetInitConnectionString(string connectionString)
    {
        var builder = new ClickHouseConnectionStringBuilder(connectionString)
        {
            Database = "default"
        };
        return builder.ConnectionString;
    }
}
