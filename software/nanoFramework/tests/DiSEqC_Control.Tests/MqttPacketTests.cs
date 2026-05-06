using System.Text;
using DiSEqC_Control.Mqtt;

namespace DiSEqC_Control.Tests;

public class MqttPacketTests
{
    [Theory]
    [InlineData(0, new byte[] { 0x00 })]
    [InlineData(127, new byte[] { 0x7F })]
    [InlineData(128, new byte[] { 0x80, 0x01 })]
    [InlineData(16383, new byte[] { 0xFF, 0x7F })]
    [InlineData(16384, new byte[] { 0x80, 0x80, 0x01 })]
    [InlineData(2097151, new byte[] { 0xFF, 0xFF, 0x7F })]
    [InlineData(2097152, new byte[] { 0x80, 0x80, 0x80, 0x01 })]
    [InlineData(268435455, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })]
    public void RemainingLength_RoundTrips(int value, byte[] expected)
    {
        byte[] encoded = MqttPacket.EncodeRemainingLength(value);
        Assert.Equal(expected, encoded);

        // Pad so DecodeRemainingLength has somewhere to read.
        byte[] buffer = new byte[encoded.Length];
        System.Array.Copy(encoded, buffer, encoded.Length);
        int decoded = MqttPacket.DecodeRemainingLength(buffer, 0, out int consumed);
        Assert.Equal(value, decoded);
        Assert.Equal(encoded.Length, consumed);
    }

    [Fact]
    public void EncodeConnect_MinimalAnonymous_HasExpectedShape()
    {
        byte[] packet = MqttPacket.EncodeConnect(
            clientId: "diseqc",
            keepAliveSeconds: 60,
            cleanSession: true,
            username: null,
            password: null,
            willTopic: null,
            willPayload: null,
            willQos: 0,
            willRetain: false);

        // Fixed header byte = CONNECT (0x10). Remaining length single byte for short payload.
        Assert.Equal(MqttPacket.TypeConnect, packet[0]);

        int rl = MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);
        Assert.Equal(1, rlBytes);
        Assert.Equal(packet.Length - 2, rl);

        int p = 1 + rlBytes;
        // Protocol name "MQTT".
        Assert.Equal(0x00, packet[p]); Assert.Equal(0x04, packet[p + 1]);
        Assert.Equal((byte)'M', packet[p + 2]);
        Assert.Equal((byte)'Q', packet[p + 3]);
        Assert.Equal((byte)'T', packet[p + 4]);
        Assert.Equal((byte)'T', packet[p + 5]);
        // Protocol level 4 (MQTT 3.1.1).
        Assert.Equal(0x04, packet[p + 6]);
        // Connect flags: only clean session.
        Assert.Equal(MqttPacket.ConnectFlagCleanSession, packet[p + 7]);
        // Keep alive 60.
        Assert.Equal(0x00, packet[p + 8]);
        Assert.Equal(0x3C, packet[p + 9]);

        // Client id.
        int clientLen = (packet[p + 10] << 8) | packet[p + 11];
        Assert.Equal(6, clientLen);
        string clientId = Encoding.UTF8.GetString(packet, p + 12, clientLen);
        Assert.Equal("diseqc", clientId);
    }

    [Fact]
    public void EncodeConnect_WithWillAndCredentials_SetsFlagsAndAppendsFields()
    {
        byte[] packet = MqttPacket.EncodeConnect(
            clientId: "c1",
            keepAliveSeconds: 30,
            cleanSession: true,
            username: "user",
            password: "pass",
            willTopic: "diseqc/availability",
            willPayload: Encoding.UTF8.GetBytes("offline"),
            willQos: 1,
            willRetain: true);

        int rl = MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);
        int p = 1 + rlBytes;
        byte connectFlags = packet[p + 7];

        Assert.Equal(MqttPacket.ConnectFlagCleanSession
                     | MqttPacket.ConnectFlagWill
                     | MqttPacket.ConnectFlagWillRetain
                     | MqttPacket.ConnectFlagUsername
                     | MqttPacket.ConnectFlagPassword
                     | (1 << 3),
                     connectFlags);

        Assert.Equal(packet.Length - 1 - rlBytes, rl);
    }

    [Fact]
    public void EncodePublish_Qos0_NoPacketId()
    {
        byte[] payload = Encoding.UTF8.GetBytes("online");
        byte[] packet = MqttPacket.EncodePublish("diseqc/availability", payload, qos: 0, retain: true, packetId: 0);

        Assert.Equal(MqttPacket.TypePublish | 0x01, packet[0]);

        int rl = MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);
        int p = 1 + rlBytes;
        int topicLen = (packet[p] << 8) | packet[p + 1];
        Assert.Equal("diseqc/availability".Length, topicLen);
        string topic = Encoding.UTF8.GetString(packet, p + 2, topicLen);
        Assert.Equal("diseqc/availability", topic);

        int payloadOffset = p + 2 + topicLen;
        Assert.Equal(payload.Length, packet.Length - payloadOffset);
        Assert.Equal(rl, packet.Length - 1 - rlBytes);
    }

    [Fact]
    public void EncodePublish_Qos1_IncludesPacketId()
    {
        byte[] packet = MqttPacket.EncodePublish("t", new byte[] { 0xAA }, qos: 1, retain: false, packetId: 0x1234);
        Assert.Equal(MqttPacket.TypePublish | 0x02, packet[0]);

        int rl = MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);
        int p = 1 + rlBytes;
        int topicLen = (packet[p] << 8) | packet[p + 1];
        int idOffset = p + 2 + topicLen;
        Assert.Equal(0x12, packet[idOffset]);
        Assert.Equal(0x34, packet[idOffset + 1]);
    }

    [Fact]
    public void DecodePublish_RoundTripsQos0()
    {
        byte[] packet = MqttPacket.EncodePublish("hello", Encoding.UTF8.GetBytes("world"), qos: 0, retain: false, packetId: 0);
        byte fixedHeader = packet[0];
        MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);

        byte[] variable = new byte[packet.Length - 1 - rlBytes];
        System.Array.Copy(packet, 1 + rlBytes, variable, 0, variable.Length);

        MqttPacket.DecodePublish(fixedHeader, variable, out string topic, out byte[] payload, out byte qos, out ushort packetId);

        Assert.Equal("hello", topic);
        Assert.Equal("world", Encoding.UTF8.GetString(payload, 0, payload.Length));
        Assert.Equal(0, qos);
        Assert.Equal(0, packetId);
    }

    [Fact]
    public void DecodePublish_RoundTripsQos1WithPacketId()
    {
        byte[] packet = MqttPacket.EncodePublish("t/q1", new byte[] { 1, 2, 3 }, qos: 1, retain: true, packetId: 0x4242);
        byte fixedHeader = packet[0];
        MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);

        byte[] variable = new byte[packet.Length - 1 - rlBytes];
        System.Array.Copy(packet, 1 + rlBytes, variable, 0, variable.Length);

        MqttPacket.DecodePublish(fixedHeader, variable, out string topic, out byte[] payload, out byte qos, out ushort packetId);

        Assert.Equal("t/q1", topic);
        Assert.Equal(new byte[] { 1, 2, 3 }, payload);
        Assert.Equal(1, qos);
        Assert.Equal(0x4242, packetId);
    }

    [Fact]
    public void EncodeSubscribe_HasReservedFlagsAndQosByte()
    {
        byte[] packet = MqttPacket.EncodeSubscribe(packetId: 7, topic: "diseqc/command/#", qos: 1);

        Assert.Equal(0x82, packet[0]); // SUBSCRIBE control packet type 8 with reserved bits = 0010.

        int rl = MqttPacket.DecodeRemainingLength(packet, 1, out int rlBytes);
        int p = 1 + rlBytes;

        Assert.Equal(0x00, packet[p]);
        Assert.Equal(0x07, packet[p + 1]);

        int topicLen = (packet[p + 2] << 8) | packet[p + 3];
        Assert.Equal("diseqc/command/#".Length, topicLen);

        Assert.Equal(0x01, packet[packet.Length - 1]);
        Assert.Equal(packet.Length - 1 - rlBytes, rl);
    }

    [Fact]
    public void EncodePubAck_IsFourBytes()
    {
        byte[] packet = MqttPacket.EncodePubAck(packetId: 0xBEEF);
        Assert.Equal(new byte[] { MqttPacket.TypePubAck, 0x02, 0xBE, 0xEF }, packet);
    }

    [Theory]
    [InlineData(MqttPacket.TypePingReq)]
    [InlineData(MqttPacket.TypeDisconnect)]
    public void EncodeBare_IsTwoBytes(byte type)
    {
        byte[] packet = MqttPacket.EncodeBare(type);
        Assert.Equal(new byte[] { type, 0x00 }, packet);
    }

    [Fact]
    public void DecodeConnAckReturnCode_ReturnsByte1()
    {
        byte[] payload = new byte[] { 0x00, MqttPacket.ConnAckBadCredentials };
        Assert.Equal(MqttPacket.ConnAckBadCredentials, MqttPacket.DecodeConnAckReturnCode(payload));
    }
}
