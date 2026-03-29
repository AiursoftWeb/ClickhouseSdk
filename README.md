# Aiursoft ClickHouse SDK & Logger Provider

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/-/pipelines)
[![NuGet version (Aiursoft.ClickhouseSdk)](https://img.shields.io/nuget/v/Aiursoft.ClickhouseSdk.svg)](https://www.nuget.org/packages/Aiursoft.ClickhouseSdk/)
[![Man hours](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/ClickhouseSdk.svg)](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/ClickhouseSdk.html)

A high-performance, textbook-grade ClickHouse integration suite for .NET applications. It provides an effortless way to interact with ClickHouse using a "DbContext" style pattern and a high-performance logging provider for the standard Microsoft.Extensions.Logging infrastructure.

## Why Aiursoft.ClickhouseSdk?

ClickHouse is a column-oriented database that excels at analytical processing but requires specific patterns for optimal performance (e.g., bulk writes instead of single-row inserts). This SDK abstracts these complexities:

1.  **Buffered Bulk Writing**: Automatically buffers entities and uses `ClickHouseBulkCopy` for massive throughput.
2.  **Automatic Schema Management**: No need to manually create tables or databases. The SDK handles it on startup.
3.  **Schema Evolution**: Automatically detects and adds missing columns to your tables as your data models evolve.
4.  **Resilient Logging**: A specialized `ILogger` provider that won't block your application during log flushes.

---

## Installation

You can install the core SDK or the logger provider via NuGet:

```bash
# Core SDK for database interaction
dotnet add package Aiursoft.ClickhouseSdk

# Specialized logger provider
dotnet add package Aiursoft.ClickhouseLoggerProvider
```

---

## Configuration

Both projects share a common configuration structure. You can configure them via `appsettings.json`:

```json
{
  "Clickhouse": {
    "ConnectionString": "Host=localhost;Protocol=http;Port=8123;Database=MyBusinessDb;User=default;Password=password",
    "Enabled": true,
    "TableName": "AppLogs" 
  }
}
```

- **ConnectionString**: Standard ClickHouse connection string.
- **Enabled**: Global switch to enable/disable ClickHouse features.
- **TableName**: Used by the Logger Provider to specify the target log table.

---

## Project 1: Aiursoft.ClickhouseSdk (Core)

The core SDK provides the base infrastructure for high-performance ClickHouse communication.

### 1. Define your Entity

Create a POCO class that matches your table schema.

```csharp
public class MyEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 2. Create your DbContext

Inherit from `ClickhouseDbContext`. Use `ClickhouseSet<T>` to define your tables.

```csharp
using Aiursoft.ClickhouseSdk;
using Aiursoft.ClickhouseSdk.Abstractions;

public class MyDbContext : ClickhouseDbContext
{
    public ClickhouseSet<MyEntity> MyEntities { get; }

    public MyDbContext(IOptionsMonitor<ClickhouseOptions> options) : base(options)
    {
        // Parameter 1: Connection factory (inherited from ClickhouseDbContext)
        // Parameter 2: Table Name
        // Parameter 3: Mapper (converts your object to an object array for ClickHouse)
        MyEntities = new ClickhouseSet<MyEntity>(GetConnection, "MyBusinessTable", entity => new object[] 
        {
            entity.Id,
            entity.Name,
            entity.Value,
            entity.CreatedAt
        });
    }

    public override async Task SaveChangesAsync()
    {
        // Flush all buffered entities in this set
        await MyEntities.SaveChangesAsync();
    }
}
```

### 3. Registration and Initialization

Register your context in `Program.cs` and initialize the schema.

```csharp
var builder = Host.CreateApplicationBuilder(args);

// 1. Bind configuration
builder.Services.Configure<ClickhouseOptions>(builder.Configuration.GetSection("Clickhouse"));

// 2. Register DbContext
builder.Services.AddSingleton<MyDbContext>();

using var host = builder.Build();

// 3. Initialize the table schema on startup
// This ensures the DB and table exist, and columns match your entity properties.
await host.Services.InitClickhouseTableAsync<MyEntity>("MyBusinessTable", "CreatedAt"); 

await host.RunAsync();
```

### 4. Basic Operations

```csharp
public class MyService(MyDbContext dbContext)
{
    public async Task ProcessData()
    {
        // Add to local buffer (non-blocking)
        dbContext.MyEntities.Add(new MyEntity 
        { 
            Id = Guid.NewGuid(), 
            Name = "Sample", 
            Value = 100, 
            CreatedAt = DateTime.UtcNow 
        });

        // Bulk upload all buffered entities to ClickHouse
        await dbContext.SaveChangesAsync();
    }
}
```

---

## Project 2: Aiursoft.ClickhouseLoggerProvider

A high-performance provider that redirects `ILogger` output to ClickHouse. It uses an internal buffer and background worker to ensure logging doesn't slow down your request pipeline.

### 1. Registration

Add the provider to your logging configuration.

```csharp
builder.Logging.AddClickhouse(options => 
{
    builder.Configuration.GetSection("Clickhouse").Bind(options);
});
```

### 2. Initialization

Initialize the logging table at startup.

```csharp
// This creates the 'AppLogs' table (defined in TableName config) 
// using 'EventTime' as the primary sort key.
await host.Services.InitLoggingTableAsync();
```

### 3. Usage

Simply use the standard `ILogger` interface.

```csharp
public class MyService(ILogger<MyService> logger)
{
    public void DoWork()
    {
        logger.LogInformation("Work started at {Time}", DateTime.UtcNow);
        try 
        {
             // ...
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred!");
        }
    }
}
```

---

## Advanced Details

### Schema Evolution

The `InitClickhouseTableAsync<T>` method performs the following:
1.  **Database Creation**: `CREATE DATABASE IF NOT EXISTS`.
2.  **Table Creation**: `CREATE TABLE IF NOT EXISTS` using the `MergeTree` engine.
3.  **Column Alignment**: For every property in your entity `T`, it executes `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`. This means you can add new properties to your C# class, and the SDK will automatically add the corresponding columns in ClickHouse on the next application start.

### Data Types Mapping

The SDK automatically maps C# types to ClickHouse types:
- `string` -> `String`
- `int` -> `Int32`
- `DateTime` -> `DateTime`
- `Guid` -> `UUID`
- `bool` -> `UInt8`

### Performance Note

ClickHouse prefers large batch writes. While `ClickhouseSet<T>.Add()` is extremely fast (just adds to a local list), calling `SaveChangesAsync()` frequently with few items can be inefficient for ClickHouse. It is recommended to call `SaveChangesAsync()` periodically or after a significant number of items have been added.

The `ClickhouseLoggerProvider` handles this automatically by flushing logs in the background every few seconds.

---

## How to Contribute

We welcome contributions! Please follow these steps:

1. Fork the repository.
2. Create a feature branch.
3. Ensure all code passes `lint.sh` and all tests pass.
4. Submit a Pull Request.

## License

This project is licensed under the MIT License.
