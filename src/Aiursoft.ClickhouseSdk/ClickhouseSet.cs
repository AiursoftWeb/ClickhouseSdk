using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;

namespace Aiursoft.ClickhouseSdk;

/// <summary>
/// A collection of entities stored in ClickHouse. 
/// Use <see cref="Add"/> to buffer data locally and <see cref="SaveChangesAsync"/> to bulk upload.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class ClickhouseSet<T>(
    Func<Task<ClickHouseConnection>> connectionFactory, 
    string tableName, 
    Func<T, object[]> mapper) where T : class
{
    private readonly List<T> _local = new();

    /// <summary>
    /// Adds an entity to the local buffer.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    public void Add(T entity)
    {
        lock (_local)
        {
            _local.Add(entity);
        }
    }

    /// <summary>
    /// Flushes all buffered entities to ClickHouse using high-performance bulk copy.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SaveChangesAsync()
    {
        T[] items;
        lock (_local)
        {
            if (_local.Count == 0)
            {
                return;
            }
            items = _local.ToArray();
            _local.Clear();
        }

        var connection = await connectionFactory();
        using var bulkCopy = new ClickHouseBulkCopy(connection)
        {
            DestinationTableName = tableName,
            BatchSize = items.Length
        };

        await bulkCopy.InitAsync();
        await bulkCopy.WriteToServerAsync(items.Select(mapper));
    }
}
