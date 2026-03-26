using Aiursoft.ClickhouseLoggerProvider;
using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;

namespace Aiursoft.ClickhouseSdk.Tests;

/// <summary>
/// Contains basic unit tests for the ClickHouse SDK components.
/// </summary>
[TestClass]
public class BasicTests
{
    /// <summary>
    /// Verifies that the ClickHouseOptions has correct default values.
    /// </summary>
    [TestMethod]
    public void TestOptionsDefaultValues()
    {
        var options = new ClickhouseOptions();
        Assert.IsTrue(options.Enabled);
        Assert.AreEqual(string.Empty, options.ConnectionString);
    }

    /// <summary>
    /// Verifies that custom connection string parameters (like 'Table') can be correctly extracted.
    /// </summary>
    [TestMethod]
    public void TestTableParameterExtraction()
    {
        const string connectionString = "Host=localhost;Database=MyLogs;Table=CustomTable;User=default";
        var builder = new ClickHouseConnectionStringBuilder(connectionString);
        
        var hasTable = builder.TryGetValue("Table", out var tableObj);
        
        Assert.IsTrue(hasTable);
        Assert.AreEqual("CustomTable", tableObj?.ToString());
    }

    /// <summary>
    /// Verifies that custom connection string parameters can be removed for compatibility.
    /// </summary>
    [TestMethod]
    public void TestConnectionStringCleaning()
    {
        const string connectionString = "Host=localhost;Table=CustomTable;User=default";
        var builder = new ClickHouseConnectionStringBuilder(connectionString);
        
        builder.Remove("Table");
        
        Assert.IsFalse(builder.TryGetValue("Table", out _));
        Assert.IsFalse(builder.ConnectionString.Contains("Table="));
    }

    /// <summary>
    /// Verifies basic LogEntry property assignments.
    /// </summary>
    [TestMethod]
    public void TestLogEntryCreation()
    {
        var now = DateTime.UtcNow;
        var entry = new LogEntry
        {
            LogLevel = "Information",
            Category = "TestCategory",
            Message = "Test Message",
            Exception = "Test Exception",
            EventTime = now
        };

        Assert.AreEqual("Information", entry.LogLevel);
        Assert.AreEqual("TestCategory", entry.Category);
        Assert.AreEqual("Test Message", entry.Message);
        Assert.AreEqual("Test Exception", entry.Exception);
        Assert.AreEqual(now, entry.EventTime);
    }
}

/// <summary>
/// Tests for the ClickhouseSet class.
/// </summary>
[TestClass]
public class ClickhouseSetTests
{
    private class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Verifies the local buffering logic of the ClickhouseSet.
    /// </summary>
    [TestMethod]
    public void TestLocalBufferAdd()
    {
        // This test only verifies that adding to the set does not throw exceptions.
        // Mocking the connection factory for a more comprehensive test would be better but is complex here.
        var set = new ClickhouseSet<TestEntity>(
            () => Task.FromResult<ClickHouseConnection>(null!), 
            "TestTable", 
            e => new object[] { e.Name });

        set.Add(new TestEntity { Name = "Item 1" });
        set.Add(new TestEntity { Name = "Item 2" });
        
        Assert.IsNotNull(set);
    }
}
