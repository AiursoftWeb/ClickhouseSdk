namespace Aiursoft.ClickhouseSdk.Abstractions;

/// <summary>
/// Configuration options for connecting to a ClickHouse server.
/// </summary>
public class ClickhouseOptions
{
    /// <summary>
    /// Gets or sets the connection string. 
    /// Support custom parameters: 'Table' to specify the target table name.
    /// Example: Host=localhost;Protocol=http;Port=8123;Database=MyLogs;Table=AppLogs
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether ClickHouse logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
