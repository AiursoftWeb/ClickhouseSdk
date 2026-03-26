using Aiursoft.ClickhouseLoggerProvider;
using Aiursoft.ClickhouseSdk.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        Assert.AreEqual("AppLogs", options.TableName);
    }

    /// <summary>
    /// Verifies that the database name can be correctly extracted from a connection string.
    /// </summary>
    [TestMethod]
    public void TestDatabaseNameExtraction()
    {
        const string connectionString = "Host=localhost;Database=MyDatabase;User=default";
        var dbName = ClickhouseConnectionUtility.GetDatabaseName(connectionString);
        Assert.AreEqual("MyDatabase", dbName);
    }

    /// <summary>
    /// Verifies that the initialization connection string is correctly generated.
    /// </summary>
    [TestMethod]
    public void TestInitConnectionStringGeneration()
    {
        const string connectionString = "Host=localhost;Database=MyDatabase;User=default";
        var initConn = ClickhouseConnectionUtility.GetInitConnectionString(connectionString);
        Assert.IsTrue(initConn.Contains("Database=default"));
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
