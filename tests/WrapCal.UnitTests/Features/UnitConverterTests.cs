using WrapCal.Features.PlanWrap;

namespace WrapCal.UnitTests;

public class UnitConverterTests
{
    [Test]
    public async Task Centimeters_pass_through_untouched()
    {
        await Assert.That(UnitConverter.ToCm(12.5, LengthUnit.Centimeters)).IsEqualTo(12.5);
        await Assert.That(UnitConverter.FromCm(12.5, LengthUnit.Centimeters)).IsEqualTo(12.5);
    }

    [Test]
    public async Task Inches_are_scaled_by_two_point_four_centimeters()
    {
        await Assert.That(UnitConverter.ToCm(1, LengthUnit.Inches)).IsEqualTo(2.54);
        await Assert.That(UnitConverter.ToCm(10, LengthUnit.Inches)).IsEqualTo(25.4);
    }

    [Test]
    public async Task Centimeters_are_divided_back_into_inches()
    {
        await Assert.That(UnitConverter.FromCm(2.54, LengthUnit.Inches)).IsEqualTo(1.0);
        await Assert.That(Math.Round(UnitConverter.FromCm(30, LengthUnit.Inches), 4)).IsEqualTo(11.811);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(30)]
    [Arguments(2.5)]
    public async Task Converting_out_and_back_returns_the_original(double value)
    {
        double roundTripped = UnitConverter.ToCm(UnitConverter.FromCm(value, LengthUnit.Inches), LengthUnit.Inches);

        await Assert.That(Math.Round(roundTripped, 10)).IsEqualTo(Math.Round(value, 10));
    }
}
