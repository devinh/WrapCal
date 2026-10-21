namespace WrapCal.Features.PlanWrap;

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

/// <summary>Converts between centimeters and inches; the planner works in centimeters.</summary>
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
/// <paramref name="InputUnit"/>; everything is converted to centimeters for the maths and back to
/// <paramref name="OutputUnit"/> at the end.
/// <para>
/// <paramref name="OverlapMargin"/> is how far the paper laps over itself where it meets, added
/// once to each sheet dimension because each has exactly one seam; at 0 the edges meet without
/// overlapping.
/// </para>
/// <para>
/// <paramref name="SearchIterations"/> caps the search under the work budget. <c>null</c> — the
/// default — uses the whole budget; zero returns the first arrangement, which is what tests
/// pinning a specific plan ask for.
/// </para>
/// </summary>
public sealed record WrapOptions(
    double RollWidth,
    double OverlapMargin,
    LengthUnit InputUnit = LengthUnit.Centimeters,
    LengthUnit OutputUnit = LengthUnit.Centimeters,
    int? SearchIterations = null,
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
