using WrapCal.Common;

namespace WrapCal.UnitTests;

public class ResultTests
{
    [Test]
    public async Task Success_reports_success_and_carries_the_value()
    {
        Result<int> result = Result<int>.Success(42);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Failure_reports_failure_and_carries_the_error()
    {
        Result<int> result = Result<int>.Failure("box.too-wide", "It does not fit.");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Code).IsEqualTo("box.too-wide");
        await Assert.That(result.Error.Message).IsEqualTo("It does not fit.");
    }

    [Test]
    public async Task Reading_Value_from_a_failure_throws()
    {
        // Reading the value of a failure is a programming error, not an expected outcome.
        Result<int> result = Result<int>.Failure("code", "nope");

        bool threw = false;
        try
        {
            _ = result.Value;
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Reading_Error_from_a_success_throws()
    {
        Result<int> result = Result<int>.Success(1);

        bool threw = false;
        try
        {
            _ = result.Error;
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Match_runs_the_success_branch_on_success()
    {
        Result<int> result = Result<int>.Success(7);

        string rendered = result.Match(
            value => $"ok:{value}",
            error => $"bad:{error.Code}");

        await Assert.That(rendered).IsEqualTo("ok:7");
    }

    [Test]
    public async Task Match_runs_the_failure_branch_on_failure()
    {
        Result<int> result = Result<int>.Failure("code", "nope");

        string rendered = result.Match(
            value => $"ok:{value}",
            error => $"bad:{error.Code}");

        await Assert.That(rendered).IsEqualTo("bad:code");
    }

    [Test]
    public async Task Carries_reference_types_as_well_as_values()
    {
        var payload = new List<string> { "a" };
        Result<List<string>> result = Result<List<string>>.Success(payload);

        await Assert.That(result.Value).IsSameReferenceAs(payload);
    }
}
