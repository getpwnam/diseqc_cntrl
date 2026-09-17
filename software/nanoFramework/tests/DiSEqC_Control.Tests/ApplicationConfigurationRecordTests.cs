using System;
using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApplicationConfigurationRecordTests
{
    [Fact]
    public void MaximumValidValuesUse237PayloadBytes()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk";
        expected.ApiToken = new string('A', ApplicationConfiguration.MaximumApiTokenLength);
        expected.DiseqcEastLimitMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcWestLimitMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcEastStepMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcWestStepMicrodegrees = ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;
        expected.DiseqcGotoOffsetMicrodegrees = -ApplicationConfiguration.MaximumDiseqcAngleMicrodegrees;

        Assert.Equal(ApplicationConfiguration.MaximumHostnameLength, expected.Hostname.Length);
        Assert.Equal(237, expected.ToPayload().Length);
        Assert.True(ApplicationConfigurationRecord.TryEncode(expected, uint.MaxValue, out byte[] record, out string encodeError), encodeError);
        Assert.Equal(ApplicationConfigurationRecord.RecordSize, record.Length);
        Assert.Equal(237, record[6] | (record[7] << 8));
        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out ApplicationConfiguration actual, out _, out string decodeError), decodeError);
        Assert.Equal(expected.ToPayload(), actual.ToPayload());
    }

    [Fact]
    public void RoundTripPreservesDiseqcPositioningConfiguration()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "cubley-test";
        expected.ApiToken = "0123456789abcdef0123456789ABCDEF";
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

    [Theory]
    [InlineData("short")]
    [InlineData("0123456789abcdef0123456789abcde!")]
    public void InvalidApiTokenIsRejected(string token)
    {
        ApplicationConfiguration configuration = ApplicationConfiguration.CreateDefaults();
        configuration.ApiToken = token;

        Assert.False(configuration.TryValidate(out string error));
        Assert.Equal("api_token_invalid", error);
    }

    [Fact]
    public void PreviousSchemaRecordIsAcceptedForMigration()
    {
        ApplicationConfiguration expected = ApplicationConfiguration.CreateDefaults();
        expected.Hostname = "cubley-test";
        string payload =
            "hostname=" + expected.Hostname + "\n" +
            "de_lim=" + expected.DiseqcEastLimitMicrodegrees.ToString() + "\n" +
            "dw_lim=" + expected.DiseqcWestLimitMicrodegrees.ToString() + "\n" +
            "de_step=" + expected.DiseqcEastStepMicrodegrees.ToString() + "\n" +
            "dw_step=" + expected.DiseqcWestStepMicrodegrees.ToString() + "\n" +
            "d_offset=" + expected.DiseqcGotoOffsetMicrodegrees.ToString();
        byte[] record = BuildPreviousSchemaRecord(payload, 4, 3);

        Assert.True(ApplicationConfigurationRecord.TryDecode(record, out ApplicationConfiguration actual, out uint generation, out string decodeError), decodeError);
        Assert.Equal((uint)3, generation);
        Assert.Equal(expected.Hostname, actual.Hostname);
        Assert.Equal(string.Empty, actual.ApiToken);
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

    private static byte[] BuildPreviousSchemaRecord(string payload, byte version, uint generation)
    {
        byte[] payloadBytes = new byte[payload.Length];
        for (int index = 0; index < payload.Length; index++)
        {
            payloadBytes[index] = (byte)payload[index];
        }

        byte[] record = new byte[ApplicationConfigurationRecord.RecordSize];
        for (int index = 0; index < record.Length; index++)
        {
            record[index] = 0xFF;
        }

        record[0] = (byte)'C';
        record[1] = (byte)'C';
        record[2] = (byte)'F';
        record[3] = (byte)'G';
        record[4] = version;
        record[5] = 0;
        record[6] = (byte)payloadBytes.Length;
        record[7] = (byte)(payloadBytes.Length >> 8);
        record[8] = (byte)generation;
        record[9] = (byte)(generation >> 8);
        record[10] = (byte)(generation >> 16);
        record[11] = (byte)(generation >> 24);

        uint crc = CalculateCrc32(payloadBytes);
        record[12] = (byte)crc;
        record[13] = (byte)(crc >> 8);
        record[14] = (byte)(crc >> 16);
        record[15] = (byte)(crc >> 24);

        Array.Copy(payloadBytes, 0, record, ApplicationConfigurationRecord.HeaderSize, payloadBytes.Length);
        return record;
    }

    private static uint CalculateCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        for (int index = 0; index < data.Length; index++)
        {
            crc ^= data[index];
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        return ~crc;
    }
}