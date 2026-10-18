using System.ComponentModel.DataAnnotations;

namespace WrapCal.Features.PlanWrap;

/// <summary>
/// What the form binds to. Kept separate from <see cref="WrapOptions"/> so the domain records
/// carry no display concerns — validation attributes belong on the input model, not the domain.
/// </summary>
public sealed class PlanWrapInput
{
    [Range(1.0, 1000.0, ErrorMessage = "Roll Width must be between 1 and 1000.")]
    public double RollWidth { get; set; } = 30;

    [Range(0.0, 100.0, ErrorMessage = "Overlap Margin must be between 0 and 100.")]
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
    /// failures as a <c>Result</c> — DataAnnotations cannot validate inside the list.
    /// </summary>
    public IReadOnlyList<GiftBox> ToBoxes() =>
        [.. Boxes.Select((box, index) => new GiftBox(
            string.IsNullOrWhiteSpace(box.Name) ? $"Box {index + 1}" : box.Name.Trim(),
            box.Length,
            box.Width,
            box.Height))];
}

public sealed class BoxInput
{
    public string? Name { get; set; }

    public double Length { get; set; } = 9;

    public double Width { get; set; } = 6;

    public double Height { get; set; } = 1.5;
}
