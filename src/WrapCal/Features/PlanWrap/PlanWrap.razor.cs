using MudBlazor;
using WrapCal.Common;

namespace WrapCal.Features.PlanWrap;

public partial class PlanWrap
{
    private readonly PlanWrapInput _input = new();

    private MudForm _form = default!;

    private WrapPlan? _plan;
    private string? _error;
    private RollDirection _rollDirection = RollDirection.Up;
    private LengthUnit _previousUnit = LengthUnit.Inches;

    /// <summary>
    /// False while the page is being prerendered, true once the circuit is live. The controls are
    /// disabled until then, so a click that lands too early cannot do anything. Read during
    /// render rather than latched in OnAfterRenderAsync: the prerendered markup is replaced by
    /// the interactive render, which already sees the answer, and there is no second pass to
    /// wait for.
    /// </summary>
    private bool Interactive => RendererInfo.IsInteractive;

    private void AddBox() => _input.Boxes.Add(new BoxInput());

    private void RemoveBox(int index)
    {
        if (_input.Boxes.Count > 1)
        {
            _input.Boxes.RemoveAt(index);
        }
    }

    /// <summary>
    /// Re-expresses every length when the unit changes, so the roll and boxes you described stay
    /// the same physical size and only the numbering changes. Rounded to two decimals — an inch
    /// is 2.54 cm, so conversions rarely land on something worth reading to full precision.
    /// </summary>
    private async Task OnUnitChangedAsync()
    {
        LengthUnit from = _previousUnit;
        LengthUnit to = _input.InputUnit;

        if (from == to)
        {
            return;
        }

        _input.RollWidth = Converted(_input.RollWidth);
        _input.OverlapMargin = Converted(_input.OverlapMargin);

        foreach (BoxInput box in _input.Boxes)
        {
            box.Length = Converted(box.Length);
            box.Width = Converted(box.Width);
            box.Height = Converted(box.Height);
        }

        _previousUnit = to;

        // Any plan on screen is stated in the unit we just left.
        _plan = null;
        _error = null;

        await _form.ValidateAsync();

        double Converted(double value) =>
            Math.Round(UnitConverter.FromCm(UnitConverter.ToCm(value, from), to), 2);
    }

    private async Task SubmitAsync()
    {
        _error = null;

        // MudForm owns the field rules now, including the per-box ones. Nothing is reported
        // until a field is left or the form is submitted, which is Mud's own timing.
        await _form.ValidateAsync();

        if (!_form.IsValid)
        {
            _plan = null;
            return;
        }

        Result<WrapPlan> result = PlanWrapLogic.Plan(_input.ToBoxes(), _input.ToOptions());

        if (result.IsSuccess)
        {
            _plan = result.Value;
        }
        else
        {
            _plan = null;
            _error = result.Error.Message;
        }
    }

    // MudBlazor declares Validation as object (it accepts several delegate shapes), so the
    // delegate type has to be supplied here rather than inferred at the call site.
    private Func<double, string> RollWidthValidation => ValidateRollWidth;

    private Func<double, string> OverlapValidation => ValidateOverlapMargin;

    private Func<string, string> NameValidation => ValidateName;

    private Func<double, string> LengthValidation => v => ValidateDimension(v, "Length");

    private Func<double, string> WidthValidation => v => ValidateDimension(v, "Width");

    private Func<double, string> HeightValidation => v => ValidateDimension(v, "Height");

    // Validation funcs return an empty string when the value is acceptable — MudBlazor shows a
    // field's message only when one is produced.

    private static string ValidateRollWidth(double value) =>
        value is < PlanWrapInput.MinRollWidth or > PlanWrapInput.MaxRollWidth
            ? $"Roll Width must be between {PlanWrapInput.MinRollWidth} and {PlanWrapInput.MaxRollWidth}."
            : string.Empty;

    private static string ValidateOverlapMargin(double value) =>
        value is < PlanWrapInput.MinOverlap or > PlanWrapInput.MaxOverlap
            ? $"Overlap Margin must be between {PlanWrapInput.MinOverlap} and {PlanWrapInput.MaxOverlap}."
            : string.Empty;

    private static string ValidateDimension(double value, string label) =>
        value is < PlanWrapInput.MinDimension or > PlanWrapInput.MaxDimension
            ? $"{label} must be between {PlanWrapInput.MinDimension} and {PlanWrapInput.MaxDimension}."
            : string.Empty;

    private static string ValidateName(string? name) =>
        name is not null && name.Trim().Length > PlanWrapInput.MaxNameLength
            ? $"Name must be {PlanWrapInput.MaxNameLength} characters or fewer."
            : string.Empty;
}
