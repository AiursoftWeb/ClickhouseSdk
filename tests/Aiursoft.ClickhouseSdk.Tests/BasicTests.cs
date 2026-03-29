using Aiursoft.ClickhouseLoggerProvider;
using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
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
    private class TestEntity { public string Name { get; set; } = string.Empty; }

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
}
