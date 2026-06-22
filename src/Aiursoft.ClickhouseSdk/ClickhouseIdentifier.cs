using System.Text.RegularExpressions;

namespace Aiursoft.ClickhouseSdk;

/// <summary>
/// Formats ClickHouse identifiers for safe SQL composition.
/// </summary>
public static class ClickhouseIdentifier
{
    private static readonly Regex IdentifierPartRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Quotes a table, database, or column identifier.
    /// </summary>
    /// <param name="identifier">The identifier to quote. Dotted identifiers are quoted part by part.</param>
    /// <returns>The quoted identifier.</returns>
    public static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("ClickHouse identifier cannot be empty.", nameof(identifier));
        }

        var parts = identifier.Split('.');
        if (parts.Any(part => !IdentifierPartRegex.IsMatch(part)))
        {
            throw new ArgumentException($"Invalid ClickHouse identifier: {identifier}", nameof(identifier));
        }

        return string.Join(".", parts.Select(part => $"`{part}`"));
    }
}
