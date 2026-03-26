using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.Options;

namespace Aiursoft.ClickhouseSdk;

/// <summary>
/// Represents a database context for ClickHouse, similar to Entity Framework Core's DbContext.
/// Manages the database connection and entity sets.
/// </summary>
public abstract class ClickhouseDbContext : IAsyncDisposable, IDisposable
{
    private ClickHouseConnection? _connection;
    private readonly ClickhouseOptions _config;

    /// <summary>
    /// Gets a value indicating whether ClickHouse is enabled in the configuration.
    /// </summary>
    public virtual bool Enabled => _config.Enabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickhouseDbContext"/> class.
    /// </summary>
    /// <param name="options">Options monitor for reactive configuration updates.</param>
    protected ClickhouseDbContext(IOptionsMonitor<ClickhouseOptions> options)
    {
        _config = options.CurrentValue;
    }

    /// <summary>
    /// Opens or returns an existing connection to ClickHouse.
    /// </summary>
    /// <returns>A connected <see cref="ClickHouseConnection"/>.</returns>
    protected async Task<ClickHouseConnection> GetConnection()
    {
        if (_connection == null)
        {
            _connection = new ClickHouseConnection(_config.ConnectionString);
            await _connection.OpenAsync();
        }
        return _connection;
    }

    /// <summary>
    /// When overridden in a derived class, flushes all entity sets to ClickHouse.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task SaveChangesAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        GC.SuppressFinalize(this);
    }
}
