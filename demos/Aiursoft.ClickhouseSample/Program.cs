using Aiursoft.ClickhouseLoggerProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aiursoft.ClickhouseSample;

/// <summary>
/// A sample application demonstrating how to use Aiursoft.ClickhouseLoggerProvider.
/// </summary>
public static class Program
{
    /// <summary>
    /// The entry point of the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static async Task Main(string[] args)
    {
        // 1. Setup the host builder
        var builder = Host.CreateApplicationBuilder(args);

        // 2. Configure logging with a single connection string
        // The connection string includes Host, Port, Database, and a custom 'Table' parameter.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddClickhouse(options =>
        {
            options.ConnectionString = "Host=localhost;Protocol=http;Port=8123;User=default;Password=password;Database=DemoDb;Table=AppLogs";
            options.Enabled = true;
        });

        using var host = builder.Build();

        // 3. Initialize the database and table schema automatically
        // This will create the 'DemoDb' database and 'AppLogs' table if they don't exist.
        // It also handles schema migration if the LogEntry structure changes.
        Console.WriteLine("Initializing Clickhouse schema...");
        await host.Services.InitLoggingTableAsync();

        // 4. Resolve the logger and start logging
        var logger = host.Services.GetRequiredService<ILogger<object>>();

        logger.LogInformation("Application started successfully.");
        logger.LogWarning("This is a sample warning message recorded in Clickhouse.");

        try
        {
            throw new InvalidOperationException("Something went wrong!");
        }
        catch (Exception ex)
        {
            // The logger automatically captures the exception stack trace and stores it in Clickhouse.
            logger.LogError(ex, "Caught an expected exception for demonstration purposes.");
        }

        logger.LogCritical("Critical error logged. Check Clickhouse for details.");

        Console.WriteLine("Logs have been buffered. Flushing to Clickhouse in the background...");
        
        // Wait for the background worker to flush the logs (flushes every 2 seconds).
        await Task.Delay(5000);

        Console.WriteLine("Sample application finished execution.");
    }
}
