using Microsoft.Extensions.Options;
using StangaNetLib.Jobs.Settings;

namespace StangaNetLib.Jobs.Tests.Settings;

public sealed class JobsSettingsValidatorTests
{
    private static readonly JobsSettingsValidator Validator = new();

    [Fact]
    public void Validate_DefaultSettings_Passes()
    {
        var result = Validator.Validate(null, new JobsSettings());

        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidWorkerCount_Fails(int workerCount)
    {
        var result = Validator.Validate(null, new JobsSettings { WorkerCount = workerCount });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(JobsSettings.WorkerCount));
    }

    [Fact]
    public void Validate_NegativeRetryCount_Fails()
    {
        var result = Validator.Validate(null, new JobsSettings { DefaultRetryCount = -1 });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(JobsSettings.DefaultRetryCount));
    }

    [Fact]
    public void Validate_NegativeRetryDelay_Fails()
    {
        var result = Validator.Validate(null, new JobsSettings { DefaultRetryDelay = TimeSpan.FromSeconds(-1) });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(JobsSettings.DefaultRetryDelay));
    }

    [Fact]
    public void Validate_RecurringCheckIntervalUnderOneSecond_Fails()
    {
        var result = Validator.Validate(null, new JobsSettings { RecurringCheckInterval = TimeSpan.FromMilliseconds(500) });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(JobsSettings.RecurringCheckInterval));
    }

    [Fact]
    public void Validate_ZeroRetryCount_Passes()
    {
        var result = Validator.Validate(null, new JobsSettings { DefaultRetryCount = 0 });

        result.Should().Be(ValidateOptionsResult.Success);
    }
}
