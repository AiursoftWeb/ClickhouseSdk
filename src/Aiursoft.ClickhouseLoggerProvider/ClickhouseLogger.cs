using Microsoft.Extensions.Logging;

namespace Aiursoft.ClickhouseLoggerProvider;

/// <summary>
/// A logger that captures messages and stores them in a ClickHouse buffer.
/// </summary>
public class ClickhouseLogger(string categoryName, LoggingDbContext dbContext) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => dbContext.Enabled;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, 
        EventId eventId, 
        TState state, 
        Exception? exception, 
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = new LogEntry
        {
            Category = categoryName,
            LogLevel = (int)logLevel,
            Message = formatter(state, exception),
            Exception = exception?.ToString() ?? string.Empty,
            EventTime = DateTime.UtcNow
        };

        dbContext.Logs.Add(entry);
    }
}
