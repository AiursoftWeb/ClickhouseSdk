namespace Aiursoft.ClickhouseSdk.Abstractions;

/// <summary>
/// Configuration options for connecting to a ClickHouse server.
/// </summary>
public class ClickhouseOptions
{
    /// <summary>
    /// Gets or sets the connection string. 
    /// Example: Host=localhost;Protocol=http;Port=8123;Database=MyLogs
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default table name to use.
    /// </summary>
    public string TableName { get; set; } = "AppLogs";

    /// <summary>
    /// Gets or sets a value indicating whether ClickHouse logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
