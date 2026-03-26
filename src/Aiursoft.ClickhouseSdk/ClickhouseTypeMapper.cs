namespace Aiursoft.ClickhouseSdk;

/// <summary>
/// A utility class for mapping .NET CLR types to ClickHouse database types.
/// </summary>
public static class ClickhouseTypeMapper
{
    /// <summary>
    /// Maps a given .NET <see cref="Type"/> to its corresponding ClickHouse type string.
    /// </summary>
    /// <param name="type">The CLR type to map.</param>
    /// <returns>A string representation of the ClickHouse type.</returns>
    public static string MapClrTypeToChType(Type type)
    {
        return type switch
        {
            _ when type == typeof(string) => "String",
            _ when type == typeof(int) => "Int32",
            _ when type == typeof(uint) => "UInt32",
            _ when type == typeof(long) => "Int64",
            _ when type == typeof(ulong) => "UInt64",
            _ when type == typeof(float) => "Float32",
            _ when type == typeof(double) => "Float64",
            _ when type == typeof(bool) => "UInt8",
            _ when type == typeof(DateTime) => "DateTime",
            _ when type == typeof(Guid) => "UUID",
            _ => "String"
        };
    }
}
