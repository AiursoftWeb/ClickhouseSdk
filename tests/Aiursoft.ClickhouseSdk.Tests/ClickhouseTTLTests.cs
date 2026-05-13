using Aiursoft.ClickhouseLoggerProvider;
using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Aiursoft.ClickhouseSdk.Tests;

/// <summary>
/// Unit tests for TTL configuration defaults (no ClickHouse server required).
/// </summary>
[TestClass]
public class ClickhouseTtlDefaultsTests
{
    /// <summary>
    /// Default RetentionDays must be 30 so that new installs and silent upgrades apply a 30-day cap.
    /// </summary>
    [TestMethod]
    public void ClickhouseOptions_DefaultRetentionDays_Is30()
    {
        var options = new ClickhouseOptions();
        Assert.AreEqual(30, options.RetentionDays);
    }

    /// <summary>
    /// ClickhouseLoggingOptions inherits RetentionDays from ClickhouseOptions.
    /// Upgrading apps that never set this value must still get 30 days.
    /// </summary>
    [TestMethod]
    public void ClickhouseLoggingOptions_InheritsRetentionDays_Default30()
    {
        var options = new ClickhouseLoggingOptions();
        Assert.AreEqual(30, options.RetentionDays);
    }

    /// <summary>
    /// Explicit override must be respected.
    /// </summary>
    [TestMethod]
    public void RetentionDays_CanBeOverridden()
    {
        var options = new ClickhouseOptions { RetentionDays = 60 };
        Assert.AreEqual(60, options.RetentionDays);
    }

    /// <summary>
    /// RetentionDays = 0 means "don't manage TTL" — backwards-compatible escape hatch.
    /// </summary>
    [TestMethod]
    public void RetentionDays_Zero_MeansDisabled()
    {
        var options = new ClickhouseOptions { RetentionDays = 0 };
        Assert.AreEqual(0, options.RetentionDays);
    }
}

/// <summary>
/// Integration tests that verify TTL idempotency and safety against a real ClickHouse server.
/// Set CLICKHOUSE_CONNECTION env var to override the default connection string.
/// All tests are skipped when ClickHouse is unreachable.
/// </summary>
[TestClass]
public class ClickhouseTtlIntegrationTests
{
    private static string? _connectionString;
    private static bool _clickhouseAvailable;
    private static string? _skipReason;

    /// <summary>
    /// Probe ClickHouse once per test run so all tests share the result.
    /// If CLICKHOUSE_CONNECTION is not set in the environment, all tests skip instantly
    /// without attempting a connection — safe for CI/CD without ClickHouse.
    /// </summary>
    [ClassInitialize]
    public static async Task CheckClickHouseAvailability(TestContext _)
    {
        _connectionString = Environment.GetEnvironmentVariable("CLICKHOUSE_CONNECTION");

        if (string.IsNullOrEmpty(_connectionString))
        {
            _clickhouseAvailable = false;
            _skipReason = "CLICKHOUSE_CONNECTION environment variable is not set. Skipping integration tests.";
            return;
        }

        try
        {
            await using var conn = new ClickHouseConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteNonQueryAsync();
            _clickhouseAvailable = true;
        }
        catch (Exception ex)
        {
            _clickhouseAvailable = false;
            _skipReason = $"ClickHouse not available at {_connectionString}: {ex.Message}";
        }
    }

    private IServiceProvider BuildServiceProvider(int retentionDays = 30, bool enabled = true)
    {
        var options = new ClickhouseOptions
        {
            ConnectionString = _connectionString!,
            Enabled = enabled,
            RetentionDays = retentionDays
        };

        var optionsMock = new Mock<IOptionsMonitor<ClickhouseOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(optionsMock.Object);

        return services.BuildServiceProvider();
    }

    private string UniqueTableName() => $"ttl_test_{Guid.NewGuid():N}";

    private async Task<string> GetEngineFull(ClickHouseConnection conn, string table)
    {
        var db = ClickhouseConnectionUtility.GetDatabaseName(_connectionString!);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT engine_full FROM system.tables WHERE database = '{db}' AND name = '{table}'";
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }

    private async Task DropTableIfExists(ClickHouseConnection conn, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS {table}";
        await cmd.ExecuteNonQueryAsync();
    }

    private void AssertSkipIfUnavailable()
    {
        if (!_clickhouseAvailable)
        {
            Assert.Inconclusive(_skipReason ?? "ClickHouse not available.");
        }
    }

    /// <summary>
    /// Scenario: New user creates a fresh table.
    /// Expect: table exists and TTL = 30 days on EventTime.
    /// </summary>
    [TestMethod]
    public async Task NewTable_GetsTTLApplied()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 30);

        try
        {
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            var engineFull = await GetEngineFull(conn, table);

            StringAssert.Contains(engineFull, "TTL", "Engine definition should include TTL clause.");
            StringAssert.Contains(engineFull, "30", "TTL should be 30 days.");
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: Old user upgrades SDK — table exists but has NO TTL.
    /// Expect: after Init, table gets TTL = 30 days.
    /// </summary>
    [TestMethod]
    public async Task ExistingTable_NoTTL_GetsTTLApplied()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 30);

        try
        {
            // Simulate old user: create table without TTL (ttlColumn: null)
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: null);

            await using (var conn = new ClickHouseConnection(_connectionString!))
            {
                await conn.OpenAsync();
                var oldEngine = await GetEngineFull(conn, table);
                Assert.IsFalse(oldEngine.Contains("TTL"), "Table should have no TTL before upgrade.");
            }

            // User upgrades SDK — now calls with ttlColumn set
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using (var conn = new ClickHouseConnection(_connectionString!))
            {
                await conn.OpenAsync();
                var newEngine = await GetEngineFull(conn, table);
                StringAssert.Contains(newEngine, "TTL", "TTL should be applied after upgrade.");
            }
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: Old user upgrades SDK — table has a stale TTL (e.g. 90 days).
    /// Expect: Init overwrites it to the new default (30 days).
    /// </summary>
    [TestMethod]
    public async Task ExistingTable_DifferentTTL_GetsCorrected()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 30);

        try
        {
            // Simulate old user with 90-day TTL
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            // Manually set TTL to 90 days (simulating old app)
            await using (var conn = new ClickHouseConnection(_connectionString!))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE {table} MODIFY TTL Created + INTERVAL 90 DAY";
                await cmd.ExecuteNonQueryAsync();

                var oldEngine = await GetEngineFull(conn, table);
                StringAssert.Contains(oldEngine, "90", "Should have 90-day TTL before correction.");
            }

            // Upgrade: Init again (same call, same RetentionDays = 30)
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using (var conn = new ClickHouseConnection(_connectionString!))
            {
                await conn.OpenAsync();
                var newEngine = await GetEngineFull(conn, table);
                StringAssert.Contains(newEngine, "TTL", "TTL clause must still be present.");
                Assert.IsFalse(newEngine.Contains("90"), "Stale 90-day TTL must be removed.");
                StringAssert.Contains(newEngine, "30", "TTL must be corrected to 30 days.");
            }
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: Table already has the correct TTL. Init again — must be harmless.
    /// </summary>
    [TestMethod]
    public async Task ExistingTable_AlreadyCorrectTTL_Idempotent()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 30);

        try
        {
            // First init sets TTL to 30
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            string engineBefore;
            await using (var conn = new ClickHouseConnection(_connectionString!))
            {
                await conn.OpenAsync();
                engineBefore = await GetEngineFull(conn, table);
                StringAssert.Contains(engineBefore, "30", "TTL should be 30 days.");
            }

            // Second init: same TTL — no-op semantically
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using (var conn = new ClickHouseConnection(_connectionString!))
            {
                await conn.OpenAsync();
                var engineAfter = await GetEngineFull(conn, table);
                Assert.AreEqual(engineBefore, engineAfter, "Engine definition must be unchanged after re-applying same TTL.");
            }
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: User overrides RetentionDays to 60.
    /// Expect: TTL = 60 days, not 30.
    /// </summary>
    [TestMethod]
    public async Task CustomRetentionDays_Respected()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 60);

        try
        {
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            var engineFull = await GetEngineFull(conn, table);

            StringAssert.Contains(engineFull, "TTL", "Engine definition should include TTL clause.");
            StringAssert.Contains(engineFull, "60", "TTL must match user override of 60 days.");
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: User does NOT pass ttlColumn (old code path).
    /// Expect: TTL is skipped — no breaking change.
    /// </summary>
    [TestMethod]
    public async Task TtlColumnNull_TTLSkipped()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 30);

        try
        {
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: null);

            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            var engineFull = await GetEngineFull(conn, table);

            Assert.IsFalse(engineFull.Contains("TTL"), "TTL must NOT be set when ttlColumn is null (backward compat).");
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: RetentionDays = 0 disables TTL management explicitly.
    /// Expect: no TTL, even with ttlColumn provided.
    /// </summary>
    [TestMethod]
    public async Task RetentionDaysZero_TTLSkipped()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 0);

        try
        {
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            var engineFull = await GetEngineFull(conn, table);

            Assert.IsFalse(engineFull.Contains("TTL"), "TTL must NOT be set when RetentionDays is 0.");
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// Scenario: Multiple concurrent or sequential Init calls on the same table.
    /// Expect: No errors, table ends in correct state.
    /// </summary>
    [TestMethod]
    public async Task MultipleInits_AreHarmless()
    {
        AssertSkipIfUnavailable();

        var table = UniqueTableName();
        var provider = BuildServiceProvider(retentionDays: 30);

        try
        {
            // Run Init 3 times in a row
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");
            await provider.InitClickhouseTableAsync<TestEntity>(table, "Created", ttlColumn: "Created");

            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            var engineFull = await GetEngineFull(conn, table);

            StringAssert.Contains(engineFull, "TTL", "TTL must be set.");
            StringAssert.Contains(engineFull, "30", "TTL must be 30 days.");

            // Verify columns exist (schema evolution didn't break anything)
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT count() FROM system.columns WHERE database = '{ClickhouseConnectionUtility.GetDatabaseName(_connectionString!)}' AND table = '{table}'";
            var colCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.AreEqual(2, colCount, "Table should have exactly 2 columns (Name, Created).");
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }

    /// <summary>
    /// The Logging path (InitLoggingTableAsync) must pass ttlColumn so logging tables get TTL automatically.
    /// </summary>
    [TestMethod]
    public async Task InitLoggingTableAsync_AppliesTTL()
    {
        AssertSkipIfUnavailable();

        var table = $"log_test_{Guid.NewGuid():N}";

        var options = new ClickhouseLoggingOptions
        {
            ConnectionString = _connectionString!,
            Enabled = true,
            RetentionDays = 30,
            TableName = table
        };

        var optionsMock = new Mock<IOptionsMonitor<ClickhouseLoggingOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(optionsMock.Object);

        var provider = services.BuildServiceProvider();

        try
        {
            await provider.InitLoggingTableAsync();

            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            var engineFull = await GetEngineFull(conn, table);

            StringAssert.Contains(engineFull, "TTL", "Logging table must get TTL automatically.");
            StringAssert.Contains(engineFull, "EventTime", "Logging TTL must be on EventTime column.");
            StringAssert.Contains(engineFull, "30", "Logging TTL must be 30 days.");
        }
        finally
        {
            await using var conn = new ClickHouseConnection(_connectionString!);
            await conn.OpenAsync();
            await DropTableIfExists(conn, table);
        }
    }
}

/// <summary>
/// Minimal entity for TTL tests — only needs a DateTime column to anchor the TTL.
/// </summary>
public class TestEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.UtcNow;
}
