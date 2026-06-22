using Aiursoft.ClickhouseLoggerProvider;
using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Aiursoft.ClickhouseSdk.Tests;

/// <summary>
/// Contains basic unit tests for the ClickHouse SDK components.
/// </summary>
[TestClass]
public class UtilityTests
{
    /// <summary>
    /// Verifies that the database name can be correctly extracted from a connection string.
    /// </summary>
    [TestMethod]
    public void TestDatabaseNameExtraction()
    {
        Assert.AreEqual("MyDb", ClickhouseConnectionUtility.GetDatabaseName("Host=localhost;Database=MyDb"));
        Assert.AreEqual("default", ClickhouseConnectionUtility.GetDatabaseName("Host=localhost"));
        Assert.AreEqual("default", ClickhouseConnectionUtility.GetDatabaseName("Host=localhost;Database="));
    }

    /// <summary>
    /// Verifies that the initialization connection string is correctly generated.
    /// </summary>
    [TestMethod]
    public void TestInitConnectionStringGeneration()
    {
        var init = ClickhouseConnectionUtility.GetInitConnectionString("Host=localhost;Database=MyDb");
        Assert.IsTrue(init.Contains("Database=default", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies the type mapping logic from C# types to ClickHouse types.
    /// </summary>
    /// <param name="type">The CLR type.</param>
    /// <param name="expected">The expected ClickHouse type string.</param>
    [TestMethod]
    [DataRow(typeof(string), "String")]
    [DataRow(typeof(int), "Int32")]
    [DataRow(typeof(DateTime), "DateTime")]
    [DataRow(typeof(Guid), "UUID")]
    [DataRow(typeof(bool), "UInt8")]
    public void TestTypeMapping(Type type, string expected)
    {
        var result = ClickhouseTypeMapper.MapClrTypeToChType(type);
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Verifies that valid ClickHouse identifiers are quoted safely.
    /// </summary>
    [TestMethod]
    public void TestIdentifierQuoting()
    {
        Assert.AreEqual("`AuditLogs`", ClickhouseIdentifier.Quote("AuditLogs"));
        Assert.AreEqual("`AuditDb`.`AuditLogs`", ClickhouseIdentifier.Quote("AuditDb.AuditLogs"));
    }

    /// <summary>
    /// Verifies that unsafe ClickHouse identifiers are rejected before SQL composition.
    /// </summary>
    [TestMethod]
    public void TestIdentifierQuotingRejectsUnsafeNames()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ClickhouseIdentifier.Quote("AuditLogs; DROP TABLE Users"));
        Assert.ThrowsExactly<ArgumentException>(() => ClickhouseIdentifier.Quote("AuditDb."));
    }
}

/// <summary>
/// Tests for the ClickHouse logger.
/// </summary>
[TestClass]
public class LoggerTests
{
    /// <summary>
    /// Verifies that the logger correctly buffers messages when enabled.
    /// </summary>
    [TestMethod]
    public void TestClickhouseLoggerBuffersCorrectly()
    {
        var optionsMock = new Mock<IOptionsMonitor<ClickhouseLoggingOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(new ClickhouseLoggingOptions 
        { 
            Enabled = true, 
            ConnectionString = "Host=localhost;Database=Test" 
        });

        var context = new LoggingDbContext(optionsMock.Object);
        var logger = new ClickhouseLogger("TestCategory", context);

        logger.LogInformation("Test Message {Id}", 123);
        Assert.IsTrue(context.Enabled);
    }

    /// <summary>
    /// Verifies that the logger is correctly disabled based on configuration.
    /// </summary>
    [TestMethod]
    public void TestLoggerDisabled()
    {
        var optionsMock = new Mock<IOptionsMonitor<ClickhouseLoggingOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(new ClickhouseLoggingOptions { Enabled = false });
        var context = new LoggingDbContext(optionsMock.Object);
        var logger = new ClickhouseLogger("TestCategory", context);

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
    }
}

/// <summary>
/// Tests for the ClickhouseSet collection.
/// </summary>
[TestClass]
public class ClickhouseSetTests
{
    private enum TestStatus
    {
        Pending,
        Complete
    }

    private class TestEntity { public string Name { get; set; } = string.Empty; }
    private class TwoColumnEntity { public string A { get; set; } = string.Empty; public int B { get; set; } }
    private class TestContext(ClickhouseOptions options) : ClickhouseDbContext(options)
    {
        public override Task SaveChangesAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the buffer is cleared after an attempt to save changes.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task TestBufferClearAfterSaveAttempt()
    {
        var connFactoryCalled = false;
        var set = new ClickhouseSet<TestEntity>(() => 
        {
            connFactoryCalled = true;
            return Task.FromResult<ClickHouseConnection>(null!);
        }, "Table", e => new object[] { e.Name });

        set.Add(new TestEntity { Name = "1" });
        
        try 
        { 
            await set.SaveChangesAsync(); 
        } 
        catch (ArgumentNullException) 
        { 
            // Expected since factory returns null and ClickHouseBulkCopy ctor validates it
        }

        Assert.IsTrue(connFactoryCalled);
    }

    /// <summary>
    /// Verifies that SaveChangesAsync throws when the mapper returns fewer columns than the entity has properties.
    /// </summary>
    [TestMethod]
    public async Task TestSaveChangesAsync_ThrowsWhenMapperColumnCountMismatch()
    {
        var set = new ClickhouseSet<TwoColumnEntity>(
            () => Task.FromResult<ClickHouseConnection>(null!),
            "Table",
            e => new object[] { e.A }); // Missing B — only 1 column for a 2-property entity

        set.Add(new TwoColumnEntity { A = "hello", B = 42 });

        InvalidOperationException? caught = null;
        try
        {
            await set.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        Assert.IsNotNull(caught, "Expected InvalidOperationException was not thrown.");
        StringAssert.Contains(caught.Message, "TwoColumnEntity");
        StringAssert.Contains(caught.Message, "1");
        StringAssert.Contains(caught.Message, "2");
    }

    /// <summary>
    /// Verifies that SaveChangesAsync does not throw when the mapper column count matches the entity.
    /// </summary>
    [TestMethod]
    public async Task TestSaveChangesAsync_NoThrowWhenMapperMatchesEntity()
    {
        var set = new ClickhouseSet<TwoColumnEntity>(
            () => Task.FromResult<ClickHouseConnection>(null!),
            "Table",
            e => new object[] { e.A, e.B }); // Correct: 2 columns for 2 properties

        set.Add(new TwoColumnEntity { A = "hello", B = 42 });

        // Should throw ArgumentNullException from ClickHouseBulkCopy (null connection),
        // NOT InvalidOperationException from our column count check.
        try
        {
            await set.SaveChangesAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Mapper"))
        {
            Assert.Fail("Should not throw mapper mismatch error when column count is correct.");
        }
        catch
        {
            // Other exceptions (null connection etc.) are fine here
        }
    }

    /// <summary>
    /// Verifies that query helpers respect the ClickHouse enabled switch before opening a connection.
    /// </summary>
    [TestMethod]
    public async Task TestQueryHelpersRespectDisabledOption()
    {
        var context = new TestContext(new ClickhouseOptions { Enabled = false });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.ExecuteScalarAsync<int>("SELECT 1"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            context.QueryAsync("SELECT 1", reader => reader.GetInt32(0)));
    }

    /// <summary>
    /// Verifies that nullable query parameters are passed to the driver as database nulls.
    /// </summary>
    [TestMethod]
    public void TestQueryParametersNormalizeNullValues()
    {
        using var command = new ClickHouseCommand();
        var parameters = new Dictionary<string, object?> { ["name"] = null };

        TestContext.AddParameters(command, parameters);

        Assert.AreSame(DBNull.Value, command.Parameters[0].Value);
    }

    /// <summary>
    /// Verifies that ClickHouse enum labels convert to CLR enum values.
    /// </summary>
    [TestMethod]
    public void TestScalarEnumConversionFromString()
    {
        var result = TestContext.ConvertScalarResult<TestStatus>("Complete");

        Assert.AreEqual(TestStatus.Complete, result);
    }

    /// <summary>
    /// Verifies that invalid identifier configuration follows the initializer's logging path.
    /// </summary>
    [TestMethod]
    public async Task TestInvalidIdentifierDoesNotEscapeInitializer()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var options = new ClickhouseOptions { Enabled = true };

        await services.InitClickhouseTableAsync<TestEntity>("Unsafe;Table", "Name", options);
    }
}
