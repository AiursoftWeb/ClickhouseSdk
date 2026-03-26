using ClickHouse.Client.ADO;

namespace Aiursoft.ClickhouseSdk.Tests;

[TestClass]
public class ConnectionStringTests
{
    [TestMethod]
    public void TestTableParameterExtraction()
    {
        var connectionString = "Host=localhost;Database=MyLogs;Table=CustomTable;User=default";
        var builder = new ClickHouseConnectionStringBuilder(connectionString);
        
        var hasTable = builder.TryGetValue("Table", out var tableObj);
        
        Assert.IsTrue(hasTable);
        Assert.AreEqual("CustomTable", tableObj?.ToString());
    }

    [TestMethod]
    public void TestConnectionStringCleaning()
    {
        var connectionString = "Host=localhost;Table=CustomTable;User=default";
        var builder = new ClickHouseConnectionStringBuilder(connectionString);
        
        builder.Remove("Table");
        
        Assert.IsFalse(builder.TryGetValue("Table", out _));
        Assert.IsFalse(builder.ConnectionString.Contains("Table="));
    }
}

[TestClass]
public class ClickhouseSetTests
{
    public class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [TestMethod]
    public void TestLocalBuffer()
    {
        var set = new ClickhouseSet<TestEntity>(
            () => Task.FromResult<ClickHouseConnection>(null!), 
            "TestTable", 
            e => new object[] { e.Name });

        set.Add(new TestEntity { Name = "Item 1" });
        set.Add(new TestEntity { Name = "Item 2" });
    }
}
