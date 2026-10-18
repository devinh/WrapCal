namespace WrapCal.Features.PlanWrap;

/// <summary>The unit a roll width, box dimension, or reported length is expressed in.</summary>
public enum LengthUnit
{
    Centimeters,
    Inches
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
/// </summary>
public sealed record WrapOptions(
    double RollWidth,
    double OverlapMargin,
    double EndAllowance = 0,
    LengthUnit InputUnit = LengthUnit.Centimeters,
    LengthUnit OutputUnit = LengthUnit.Centimeters,
    int RandomRestarts = 500,
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
    double RollLengthUsed,
    double PaperAreaCut,
    double WasteArea,
    double Efficiency,
    LengthUnit Unit);
