namespace WrapCal.Features.PlanWrap;

/// <summary>
/// What the form binds to. Kept separate from <see cref="WrapOptions"/> so the domain records
/// carry no display concerns — the limits below describe what a person may type, not what the
/// planner accepts.
/// </summary>
public sealed class PlanWrapInput
{
    public const double MinRollWidth = 1.0;

    public const double MaxRollWidth = 1000.0;

    public const double MinOverlap = 0.0;

    public const double MaxOverlap = 100.0;

    public const double MinDimension = 0.1;

    public const double MaxDimension = 1000.0;

    public const int MaxNameLength = 50;

    public double RollWidth { get; set; } = 30;

    public double OverlapMargin { get; set; } = 0.5;

    public LengthUnit InputUnit { get; set; } = LengthUnit.Inches;

    public List<BoxInput> Boxes { get; set; } = [new()];

    /// <summary>
    /// Results are reported in the same unit the user typed in. There is deliberately no
    /// separate output-unit field: choosing imperial and being handed centimetres is
    /// surprising, and the form never exposed the control to change it.
    /// </summary>
    public WrapOptions ToOptions() =>
        new(RollWidth, OverlapMargin, InputUnit, InputUnit);

    /// <summary>
    /// Box dimensions themselves are checked by <see cref="PlanWrapLogic"/>, which reports
    /// failures as a <c>Result</c> — that also covers rules needing the roll width, which no
    /// single field can know.
    /// </summary>
    public IReadOnlyList<GiftBox> ToBoxes() =>
        [.. Boxes.Select((box, index) => new GiftBox(
            string.IsNullOrWhiteSpace(box.Name) ? $"Box {index + 1}" : box.Name.Trim(),
            box.Length,
            box.Width,
            box.Height))];
}

/// <summary>
/// One row of the boxes grid. The rules for its fields live in the page's MudForm validation
/// funcs, which apply the limits above — there is no second copy here to drift out of step.
/// </summary>
public sealed class BoxInput
{
    public string? Name { get; set; }

    public double Length { get; set; } = 9;

    public double Width { get; set; } = 6;

    public double Height { get; set; } = 1.5;
}
