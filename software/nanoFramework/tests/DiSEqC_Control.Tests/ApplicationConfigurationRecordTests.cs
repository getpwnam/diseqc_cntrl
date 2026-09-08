using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApplicationConfigurationRecordTests
{
    [Fact]
    public void RoundTripPreservesDiseqcPositioningConfiguration()
    {
        MqttConfiguration expected = MqttConfiguration.CreateDefaults();
        expected.DiseqcEastLimitMicrodegrees = 50_000_000;
        expected.DiseqcWestLimitMicrodegrees = 45_000_000;
        expected.DiseqcEastStepMicrodegrees = 112_658;
        expected.DiseqcWestStepMicrodegrees = 112_658;
        expected.DiseqcGotoOffsetMicrodegrees = -3_380_000;

        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, 7, out byte[] record, out string encodeError), encodeError);
        Assert.Equal(ApplicationConfigurationRecord.SchemaVersion, record[4]);
        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out MqttConfiguration actual, out uint generation, out string decodeError), decodeError);

        Assert.Equal((uint)7, generation);
        Assert.Equal(expected.ToPayload(), actual.ToPayload());
    }

    [Fact]
    public void LegacyPayloadLoadsPositioningDefaults()
    {
        const string legacyPayload =
            "enabled=false\n" +
            "broker=\n" +
            "port=1883\n" +
            "client_id=\n" +
            "hostname=cubley-test\n" +
            "username=\n" +
            "password=\n" +
            "topic_prefix=diseqc\n" +
            "keepalive_seconds=60\n" +
            "reconnect_seconds=5";

        Assert.True(MqttConfiguration.TryParsePayload(legacyPayload, out MqttConfiguration actual, out string decodeError), decodeError);
        Assert.Equal("cubley-test", actual.Hostname);
        Assert.Equal(0, actual.DiseqcEastLimitMicrodegrees);
        Assert.Equal(0, actual.DiseqcWestLimitMicrodegrees);
        Assert.Equal(0, actual.DiseqcEastStepMicrodegrees);
        Assert.Equal(0, actual.DiseqcWestStepMicrodegrees);
        Assert.Equal(0, actual.DiseqcGotoOffsetMicrodegrees);
    }

    [Fact]
    public void SchemaTwoRecordHeaderRemainsReadable()
    {
        MqttConfiguration expected = MqttConfiguration.CreateDefaults();
        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, 3, out byte[] record, out string encodeError), encodeError);
        record[4] = 2;

        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out _, out uint generation, out string decodeError), decodeError);
        Assert.Equal((uint)3, generation);
    }
}