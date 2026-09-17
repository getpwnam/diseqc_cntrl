using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApplicationConfigurationRecordTests
{
    [Fact]
    public void MaximumValidValuesUse162PayloadBytes()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk";
        expected.DiseqcEastLimitMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcWestLimitMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcEastStepMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcWestStepMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcGotoOffsetMicrodegrees = -ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;

        Assert.Equal(ApplicationConfiguration.MaximumHostnameLength, expected.Hostname.Length);
        Assert.Equal(162, expected.ToPayload().Length);
        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, uint.MaxValue, out byte[] record, out string encodeError), encodeError);
        Assert.Equal(ApplicationConfigurationRecord.RecordSize, record.Length);
        Assert.Equal(162, record[6] | (record[7] << 8));
        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out ApplicationConfiguration actual, out _, out string decodeError), decodeError);
        Assert.Equal(expected.ToPayload(), actual.ToPayload());
    }

    [Fact]
    public void RoundTripPreservesDiseqcPositioningConfiguration()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "cubley-test";
        expected.DiseqcEastLimitMicrodegrees = 50_000_000;
        expected.DiseqcWestLimitMicrodegrees = 45_000_000;
        expected.DiseqcEastStepMicrodegrees = 112_658;
        expected.DiseqcWestStepMicrodegrees = 112_658;
        expected.DiseqcGotoOffsetMicrodegrees = -3_380_000;

        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, 7, out byte[] record, out string encodeError), encodeError);
        Assert.Equal(ApplicationConfigurationRecord.SchemaVersion, record[4]);
        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out ApplicationConfiguration actual, out uint generation, out string decodeError), decodeError);

        Assert.Equal((uint)7, generation);
        Assert.Equal(expected.ToPayload(), actual.ToPayload());
    }

    [Fact]
    public void PayloadRejectsRemovedTransportFields()
    {
        Assert.False(ApplicationConfiguration.TryParsePayload(
            "hostname=cubley-test\nbroker=example.net",
            out _,
            out string error));
        Assert.Equal("payload_key_unknown", error);
    }

    [Fact]
    public void PreviousSchemaRecordIsRejected()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "cubley-test";
        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, 3, out byte[] record, out string encodeError), encodeError);
        record[4] = 3;

        Assert.False(ApplicationConfigurationRecord.TryDecode(record, out _, out _, out string decodeError));
        Assert.Equal("record_version_unsupported", decodeError);
    }

    [Fact]
    public void UnsupportedSchemaRecordIsRejected()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, 2, out byte[] record, out string encodeError), encodeError);
        record[4] = 2;

        Assert.False(ApplicationConfigurationRecord.TryDecode(record, out _, out _, out string decodeError));
        Assert.Equal("record_version_unsupported", decodeError);
    }
}