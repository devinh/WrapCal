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

    public const double MinDimension = 0.1;

    public const double MaxDimension = 1000;

    public const int MaxNameLength = 50;

    /// <summary>
    /// Per-field problems with the box rows, so the page can show each against the input that
    /// caused it. <c>DataAnnotationsValidator</c> cannot reach inside the list.
    /// </summary>
    public IReadOnlyList<BoxProblem> ValidateBoxes()
    {
        List<BoxProblem> problems = [];

        foreach (BoxInput box in Boxes)
        {
            CheckDimension(box, nameof(BoxInput.Length), "Length", box.Length);
            CheckDimension(box, nameof(BoxInput.Width), "Width", box.Width);
            CheckDimension(box, nameof(BoxInput.Height), "Height", box.Height);

            if (box.Name is not null && box.Name.Trim().Length > MaxNameLength)
            {
                problems.Add(new BoxProblem(
                    box,
                    nameof(BoxInput.Name),
                    $"Name must be {MaxNameLength} characters or fewer."));
            }
        }

        return problems;

        void CheckDimension(BoxInput box, string field, string label, double value)
        {
            if (value is < MinDimension or > MaxDimension)
            {
                problems.Add(new BoxProblem(
                    box,
                    field,
                    $"{label} must be between {MinDimension} and {MaxDimension}."));
            }
        }
    }

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
/// One row of the boxes table. Deliberately carries no validation attributes:
/// <c>DataAnnotationsValidator</c> does not recurse into a collection, so attributes here are
/// silently ignored. The rules live in <see cref="PlanWrapInput.ValidateBoxes"/> instead, which
/// the page feeds into its own <c>ValidationMessageStore</c>.
/// </summary>
public sealed class BoxInput
{
    public string? Name { get; set; }

    public double Length { get; set; } = 9;

    public double Width { get; set; } = 6;

    public double Height { get; set; } = 1.5;
}

/// <summary>A validation problem on one field of one box row.</summary>
public sealed record BoxProblem(BoxInput Box, string Field, string Message);
