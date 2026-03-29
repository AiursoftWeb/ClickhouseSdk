namespace Aiursoft.ClickhouseLoggerProvider;

/// <summary>
/// Represents a log entry to be stored in ClickHouse.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets or sets the time when the event occurred.
    /// </summary>
    public DateTime EventTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public int LogLevel { get; set; }

    /// <summary>
    /// Gets or sets the category of the log (usually the class name).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the formatted log message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception details, if any.
    /// </summary>
    public string Exception { get; set; } = string.Empty;
}
