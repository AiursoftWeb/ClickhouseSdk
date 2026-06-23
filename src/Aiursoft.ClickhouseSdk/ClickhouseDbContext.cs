using Aiursoft.ClickhouseSdk.Abstractions;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using Microsoft.Extensions.Options;
using System.Data.Common;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Aiursoft.ClickhouseSdk.Tests")]

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
    protected ClickhouseDbContext(IOptionsMonitor<ClickhouseOptions> options) : this(options.CurrentValue)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickhouseDbContext"/> class.
    /// </summary>
    /// <param name="options">Static options to use.</param>
    protected ClickhouseDbContext(ClickhouseOptions options)
    {
        _config = options;
    }

    /// <summary>
    /// Opens or returns an existing connection to ClickHouse.
    /// </summary>
    /// <returns>A connected <see cref="ClickHouseConnection"/>.</returns>
    protected async Task<ClickHouseConnection> GetConnection()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("Clickhouse is disabled in configuration. Cannot open connection.");
        }

        if (_connection == null)
        {
            _connection = new ClickHouseConnection(_config.ConnectionString);
            await _connection.OpenAsync();
        }
        return _connection;
    }

    /// <summary>
    /// Executes a scalar ClickHouse query and converts the result to the requested type.
    /// </summary>
    /// <typeparam name="T">The expected scalar result type.</typeparam>
    /// <param name="commandText">The SQL command text to execute.</param>
    /// <param name="parameters">Optional command parameters.</param>
    /// <returns>The scalar query result.</returns>
    public async Task<T?> ExecuteScalarAsync<T>(
        string commandText,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var connection = await GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameters(command, parameters);

        var result = await command.ExecuteScalarAsync();
        return ConvertScalarResult<T>(result);
    }

    internal static T? ConvertScalarResult<T>(object? result)
    {
        if (result is null || result is DBNull)
        {
            return default;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType.IsEnum)
        {
            if (result is string enumName)
            {
                return (T)Enum.Parse(targetType, enumName);
            }

            return (T)Enum.ToObject(targetType, result);
        }

        return (T)Convert.ChangeType(result, targetType);
    }

    /// <summary>
    /// Executes a ClickHouse query and maps each row to the requested result type.
    /// </summary>
    /// <typeparam name="T">The row result type.</typeparam>
    /// <param name="commandText">The SQL command text to execute.</param>
    /// <param name="mapper">Maps a data reader row to a result object.</param>
    /// <param name="parameters">Optional command parameters.</param>
    /// <returns>The mapped query results.</returns>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string commandText,
        Func<DbDataReader, T> mapper,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var connection = await GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameters(command, parameters);

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }

        return results;
    }

    internal static void AddParameters(
        ClickHouseCommand command,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            command.AddParameter(parameter.Key, parameter.Value ?? DBNull.Value);
        }
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
