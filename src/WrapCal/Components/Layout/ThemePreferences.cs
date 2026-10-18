using System.Text.Json.Serialization;

namespace WrapCal.Components.Layout;

/// <summary>
/// The theme cookies as <c>theme.js</c> reads them. Either is <c>null</c> when the browser has
/// never been given a preference to remember — a first visit, or one since the cookies were
/// cleared.
/// </summary>
public sealed record ThemePreferences(
    [property: JsonPropertyName("choice")] string? Choice,
    [property: JsonPropertyName("system")] string? System);
