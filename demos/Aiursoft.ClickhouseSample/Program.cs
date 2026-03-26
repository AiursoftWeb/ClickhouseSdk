using Aiursoft.ClickhouseLoggerProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Aiursoft.ClickhouseSample;

/// <summary>
/// A sample application demonstrating how to use Aiursoft.ClickhouseLoggerProvider with configuration files.
/// </summary>
public static class Program
{
    /// <summary>
    /// The entry point of the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static async Task Main(string[] args)
    {
        // 1. Setup the host builder and load configuration
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false);

        // 2. Configure logging using the configuration section
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddClickhouse(options =>
        {
            builder.Configuration.GetSection("Clickhouse").Bind(options);
        });

        using var host = builder.Build();

        // 3. Initialize the database and table schema automatically
        Console.WriteLine("Initializing Clickhouse schema...");
        await host.Services.InitLoggingTableAsync();

        // 4. Resolve the logger and start logging
        var logger = host.Services.GetRequiredService<ILogger<object>>();

        logger.LogInformation("Application started successfully and loaded configuration from appsettings.json.");
        logger.LogWarning("This is a sample warning message recorded in Clickhouse.");

        try
        {
            throw new InvalidOperationException("Something went wrong!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Caught an expected exception for demonstration purposes.");
        }

        logger.LogCritical("Critical error logged. Check Clickhouse for details.");

        Console.WriteLine("Logs have been buffered. Flushing to Clickhouse in the background...");
        
        // Wait for the background worker to flush the logs (flushes every 2 seconds).
        await Task.Delay(5000);

        Console.WriteLine("Sample application finished execution.");
    }
}
