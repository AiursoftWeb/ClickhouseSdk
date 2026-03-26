using Aiursoft.ClickhouseSdk;
using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.Options;

namespace Aiursoft.ClickhouseLoggerProvider;

/// <summary>
/// A specialized DbContext for storing application logs in ClickHouse.
/// </summary>
public class LoggingDbContext : ClickhouseDbContext
{
    /// <summary>
    /// Gets the set of logs to be flushed.
    /// </summary>
    public ClickhouseSet<LogEntry> Logs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingDbContext"/> class.
    /// </summary>
    /// <param name="options">Configuration options for ClickHouse.</param>
    public LoggingDbContext(IOptionsMonitor<ClickhouseOptions> options) 
        : base(options)
    {
        var builder = new ClickHouseConnectionStringBuilder(options.CurrentValue.ConnectionString);
        var tableName = builder.TryGetValue("Table", out var tableObj) 
            ? tableObj.ToString() 
            : "Logs";

        Logs = new ClickhouseSet<LogEntry>(GetConnection, tableName!, log => new object[] 
        {
            log.EventTime,
            log.LogLevel,
            log.Category,
            log.Message,
            log.Exception
        });
    }

    /// <inheritdoc />
    public override async Task SaveChangesAsync()
    {
        if (!Enabled)
        {
            return;
        }
        await Logs.SaveChangesAsync();
    }
}
