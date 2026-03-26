using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aiursoft.ClickhouseLoggerProvider;

/// <summary>
/// A provider for creating <see cref="ClickhouseLogger"/> instances.
/// Manages a background task to periodically flush logs to ClickHouse.
/// </summary>
public sealed class ClickhouseLoggerProvider : ILoggerProvider
{
    private readonly LoggingDbContext _dbContext;
    private readonly ConcurrentDictionary<string, ClickhouseLogger> _loggers = new();
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickhouseLoggerProvider"/> class.
    /// </summary>
    /// <param name="dbContext">The database context used for log storage.</param>
    public ClickhouseLoggerProvider(LoggingDbContext dbContext)
    {
        _dbContext = dbContext;
        // Start background flushing
        Task.Run(BackgroundFlush);
    }

    private async Task BackgroundFlush()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, _cts.Token);
                await _dbContext.SaveChangesAsync();
            }
            catch (OperationCanceledException)
            {
                // Task is being cancelled, ignore
            }
            catch (Exception e)
            {
                // Fail-safe to avoid crashing the background worker
                Console.Error.WriteLine($"Failed to flush logs to Clickhouse: {e.Message}");
            }
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new ClickhouseLogger(name, _dbContext));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        // Final attempt to flush logs before disposal
        _dbContext.SaveChangesAsync().GetAwaiter().GetResult();
        _dbContext.Dispose();
        _cts.Dispose();
    }
}
