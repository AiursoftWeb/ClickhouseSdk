using Aiursoft.ClickhouseSdk;
using Aiursoft.ClickhouseSdk.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.ClickhouseLoggerProvider;

/// <summary>
/// Provides extension methods for registering ClickHouse logging.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds a ClickHouse logger to the logging builder.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">An action to configure the ClickHouse options for logging.</param>
    /// <returns>The updated logging builder.</returns>
    public static ILoggingBuilder AddClickhouse(this ILoggingBuilder builder, Action<ClickhouseLoggingOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddSingleton<LoggingDbContext>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ClickhouseLoggerProvider>());
        return builder;
    }

    /// <summary>
    /// Initializes the ClickHouse table used for storing logs.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task InitLoggingTableAsync(this IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptionsMonitor<ClickhouseLoggingOptions>>();
        await provider.InitClickhouseTableAsync<LogEntry>(options.CurrentValue.TableName, "EventTime", options.CurrentValue);
    }
}
