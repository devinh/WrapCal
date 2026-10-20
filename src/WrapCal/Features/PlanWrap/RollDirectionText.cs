namespace WrapCal.Features.PlanWrap;

/// <summary>
/// The words for a roll direction, and the two facts behind where the origin sits.
/// <para>
/// Shared rather than restated: the drawing places the dot from <see cref="OriginAtFarEnd"/> and
/// <see cref="OriginAtBottom"/>, and the Cut Plan names the corner from the same two answers — so
/// the sentence cannot come to disagree with the picture. The menu and the caption take their
/// words from here for the same reason.
/// </para>
/// </summary>
internal static class RollDirectionText
{
    /// <summary>
    /// The directions in the order the menu offers them. Stated rather than read off the enum,
    /// whose declaration order is the drawing's (Down first) and not the reader's.
    /// </summary>
    public static IReadOnlyList<RollDirection> MenuOrder { get; } =
        [RollDirection.Up, RollDirection.Down, RollDirection.Left, RollDirection.Right];

    /// <summary>Whether the origin is at the end the roll was cut from, rather than its start.</summary>
    public static bool OriginAtFarEnd(RollDirection direction) => direction is RollDirection.Left;

    /// <summary>
    /// Whether the origin sits along the bottom edge. Every direction but Down draws plan Y
    /// downwards from somewhere above, so only Down puts it at the top.
    /// </summary>
    public static bool OriginAtBottom(RollDirection direction) => direction is not RollDirection.Down;

    /// <summary>
    /// Which corner of the drawing the origin is, e.g. <c>Bottom-Left</c>. Both words are
    /// capitalised because it reads as the name of a corner rather than as prose.
    /// </summary>
    public static string OriginCorner(RollDirection direction) =>
        $"{(OriginAtBottom(direction) ? "Bottom" : "Top")}-"
        + $"{(OriginAtFarEnd(direction) ? "Right" : "Left")}";

    /// <summary>The side the unused paper runs past, in the middle of a sentence: "top".</summary>
    public static string EdgeWord(RollDirection direction) => direction switch
    {
        RollDirection.Down => "bottom",
        RollDirection.Up => "top",
        RollDirection.Left => "left",
        RollDirection.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>What the orientation menu calls it: the side the roll is on.</summary>
    public static string MenuLabel(RollDirection direction) => direction switch
    {
        RollDirection.Down => "Bottom",
        RollDirection.Up => "Top",
        RollDirection.Left => "Left",
        RollDirection.Right => "Right",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}
