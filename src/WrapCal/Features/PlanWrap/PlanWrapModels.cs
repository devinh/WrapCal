namespace WrapCal.Features.PlanWrap;

/// <summary>The unit a roll width, box dimension, or reported length is expressed in.</summary>
public enum LengthUnit
{
    Centimeters,
    Inches
}

/// <summary>
/// Which way the unused paper is drawn beyond the last cut. Purely presentational — the plan
/// always measures Y from the starting end regardless of how it is drawn.
/// </summary>
public enum RollDirection
{
    Down,
    Up,
    Left,
    Right
}

/// <summary>
/// Converts between centimetres and inches. The planner does all its arithmetic in
/// centimetres, converting once on the way in and once on the way out.
/// </summary>
public static class UnitConverter
{
    public const double CmPerInch = 2.54;

    public static double ToCm(double value, LengthUnit unit) =>
        unit == LengthUnit.Inches ? value * CmPerInch : value;

    public static double FromCm(double cm, LengthUnit unit) =>
        unit == LengthUnit.Inches ? cm / CmPerInch : cm;
}

/// <summary>A box to wrap. Dimensions are in the options' input unit.</summary>
public sealed record GiftBox(string Name, double Length, double Width, double Height);

/// <summary>
/// <paramref name="RollWidth"/> and <paramref name="OverlapMargin"/> are in
/// <paramref name="InputUnit"/>. Everything is converted to centimetres for the maths and
/// converted back to <paramref name="OutputUnit"/> at the end.
/// <para>
/// <paramref name="OverlapMargin"/> is how far the paper laps over itself where it meets. Both
/// sheet dimensions have exactly one such seam — the wrap-around has one where its two ends
/// meet, and the along-the-box dimension has one where the two folded end flaps meet — so the
/// margin is added once to each. With 0 the paper would meet exactly and not overlap at all.
/// </para>
/// <para>
/// <paramref name="SearchIterations"/> is how many times the planner may nudge the arrangement
/// it already has in search of a shorter roll. Zero means "take the first arrangement and stop",
/// which is what tests that pin a specific plan ask for. It is a request, not a promise: the
/// work budget in <see cref="PlanWrapLogic"/> overrules it so a long list cannot block the page
/// it is drawn on.
/// </para>
/// </summary>
public sealed record WrapOptions(
    double RollWidth,
    double OverlapMargin,
    LengthUnit InputUnit = LengthUnit.Centimeters,
    LengthUnit OutputUnit = LengthUnit.Centimeters,
    int SearchIterations = 500,
    int Seed = 1);

/// <summary>
/// One rectangle to cut from the roll. X runs across the roll width, Y down the roll length,
/// with the origin at the corner of the starting end.
/// </summary>
public sealed record CutSection(
    string ItemName,
    double X,
    double Y,
    double Width,
    double Length,
    string Note);

/// <summary>Lengths are in <paramref name="Unit"/>; areas are in that unit squared.</summary>
public sealed record WrapPlan(
    IReadOnlyList<CutSection> Sections,
    double RollWidth,
    double RollLengthUsed,
    double PaperAreaCut,
    double WasteArea,
    double Efficiency,
    LengthUnit Unit);
