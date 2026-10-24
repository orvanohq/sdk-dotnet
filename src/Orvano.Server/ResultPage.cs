using System.Text.Json.Serialization;

namespace Orvano.Server;

/// <summary>
/// One page of a list (spec 0014 PAG-2). Pass <see cref="NextCursor"/> as the <c>cursor</c> for the next page, or use
/// the list method's <c>All</c> sibling, which walks every page (spec 0002 LIB-5).
/// </summary>
/// <typeparam name="T">The type of one item.</typeparam>
public sealed record ResultPage<T>
{
    /// <summary>The items on this page, in the list's order.</summary>
    [JsonPropertyName("items")]
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>The cursor for the next page, or null on the last page.</summary>
    [JsonPropertyName("nextCursor")]
    public required string? NextCursor { get; init; }

    /// <summary>How many items the whole list has, or null when the operation does not count them.</summary>
    [JsonPropertyName("total")]
    public required int? Total { get; init; }
}
