# Aiursoft ClickHouse SDK & Logger Provider

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/clickhouseSdk/-/pipelines)
[![NuGet version (Aiursoft.ClickhouseSdk)](https://img.shields.io/nuget/v/Aiursoft.ClickhouseSdk.svg)](https://www.nuget.org/packages/Aiursoft.ClickhouseSdk/)
[![Man hours](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/ClickhouseSdk.svg)](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/ClickhouseSdk.html)

A high-performance, textbook-grade ClickHouse integration suite for .NET applications. It provides an effortless way to interact with ClickHouse using a "DbContext" style pattern and a high-performance logging provider for the standard Microsoft.Extensions.Logging infrastructure.

## Key Features

- **One Connection String Configuration**: Configure host, port, database, and target table all within a single connection string.
- **Automatic Schema Management**: Automatically creates databases and tables. Supports schema evolution (automatically adds missing columns).
- **High-Performance Bulk Writing**: Uses `ClickHouseBulkCopy` for asynchronous, non-blocking batch uploads.
- **Background Log Flushing**: Buffered logging with background flushing to ensure zero impact on application performance.

## Project 1: Aiursoft.ClickhouseSdk (Core SDK)

The core SDK provides the base infrastructure for ClickHouse communication.

### Installation

```bash
dotnet add package Aiursoft.ClickhouseSdk
```

### Basic Usage

1. **Define your Entity**:
   Create a class representing your data row.

2. **Create your DbContext**:
   Inherit from `ClickhouseDbContext` and define your sets.

```csharp
using Aiursoft.ClickhouseSdk;
using Aiursoft.ClickhouseSdk.Abstractions;

public class MyDbContext : ClickhouseDbContext
{
    public ClickhouseSet<MyEntity> MyEntities { get; }

    public MyDbContext(IOptionsMonitor<ClickhouseOptions> options) : base(options)
    {
        // Define the mapping from your entity to ClickHouse row objects
        MyEntities = new ClickhouseSet<MyEntity>(GetConnection, "MyTableName", entity => new object[] 
        {
            entity.Id,
            entity.Name,
            entity.CreatedAt
        });
    }

    public override async Task SaveChangesAsync()
    {
        await MyEntities.SaveChangesAsync();
    }
}
```

3. **Initialize the Schema**:
   Call `InitClickhouseTableAsync<T>` during application startup to ensure the database and table exist.

```csharp
await host.Services.InitClickhouseTableAsync<MyEntity>("CreatedAt"); // ORDER BY column
```

---

## Project 2: Aiursoft.ClickhouseLoggerProvider (Logging)

A specialized provider that redirects standard `ILogger` output to ClickHouse.

### Installation

```bash
dotnet add package Aiursoft.ClickhouseLoggerProvider
```

### Registration

Register the provider in your application builder.

```csharp
builder.Logging.AddClickhouse(options => 
{
    // Configure EVERYTHING in one string!
    // Database 'MyLogs' and Table 'AppLogs' will be created automatically.
    options.ConnectionString = "Host=localhost;Protocol=http;Port=8123;User=default;Password=password;Database=MyLogs;Table=AppLogs";
});
```

### Initialization

Ensure the logging table is initialized at startup.

```csharp
await host.Services.InitLoggingTableAsync();
```

### Usage

Just use the standard `ILogger` as usual. Logs are buffered and flushed to ClickHouse every 2 seconds in the background.

```csharp
logger.LogInformation("Hello ClickHouse!");
```

## How to Contribute

We welcome contributions! Please follow these steps:

1. Fork the repository.
2. Create a feature branch.
3. Ensure all code passes `lint.sh` and all tests pass.
4. Submit a Pull Request.

## License

This project is licensed under the MIT License.
