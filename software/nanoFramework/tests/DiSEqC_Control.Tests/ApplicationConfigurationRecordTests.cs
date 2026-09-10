using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApplicationConfigurationRecordTests
{
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
    public void PreviousSchemaRecordPreservesApplicationFields()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "cubley-legacy";
        expected.DiseqcEastLimitMicrodegrees = 36_600_000;
        expected.DiseqcWestLimitMicrodegrees = 40_000_000;
        expected.DiseqcEastStepMicrodegrees = 112_658;
        expected.DiseqcWestStepMicrodegrees = 113_000;
        expected.DiseqcGotoOffsetMicrodegrees = -3_380_000;
        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, 3, out byte[] record, out string encodeError), encodeError);
        record[4] = 3;

        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out ApplicationConfiguration actual, out uint generation, out string decodeError), decodeError);
        Assert.Equal((uint)3, generation);
        Assert.Equal(expected.ToPayload(), actual.ToPayload());
    }

    [Fact]
    public void LegacyPayloadDropsRetiredTransportFields()
    {
        const string payload =
            "enabled=true\n" +
            "broker=example.net\n" +
            "port=1883\n" +
            "client_id=legacy\n" +
            "hostname=cubley-legacy\n" +
            "username=user\n" +
            "password=secret\n" +
            "topic_prefix=diseqc\n" +
            "keepalive_seconds=60\n" +
            "reconnect_seconds=5\n" +
            "de_lim=36600000\n" +
            "dw_lim=40000000\n" +
            "de_step=112658\n" +
            "dw_step=113000\n" +
            "d_offset=-3380000";

        Assert.True(ApplicationConfiguration.TryParseLegacyPayload(
            payload,
            out ApplicationConfiguration actual,
            out string error), error);
        Assert.Equal("cubley-legacy", actual.Hostname);
        Assert.Equal(36_600_000, actual.DiseqcEastLimitMicrodegrees);
        Assert.Equal(40_000_000, actual.DiseqcWestLimitMicrodegrees);
        Assert.Equal(112_658, actual.DiseqcEastStepMicrodegrees);
        Assert.Equal(113_000, actual.DiseqcWestStepMicrodegrees);
        Assert.Equal(-3_380_000, actual.DiseqcGotoOffsetMicrodegrees);
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