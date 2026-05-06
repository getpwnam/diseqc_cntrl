using System;

namespace DiSEqC_Control.Mqtt
{
    /// <summary>
    /// MQTT 3.1.1 packet encoder/decoder. Pure logic, no I/O — designed to be unit-testable on the host.
    /// Supports the subset we need: CONNECT, CONNACK, PUBLISH (QoS 0/1), PUBACK, SUBSCRIBE, SUBACK,
    /// PINGREQ, PINGRESP, DISCONNECT.
    /// </summary>
    internal static class MqttPacket
    {
        public const byte TypeConnect = 0x10;
        public const byte TypeConnAck = 0x20;
        public const byte TypePublish = 0x30;
        public const byte TypePubAck = 0x40;
        public const byte TypeSubscribe = 0x82; // reserved bits 0010
        public const byte TypeSubAck = 0x90;
        public const byte TypeUnsubscribe = 0xA2;
        public const byte TypeUnsubAck = 0xB0;
        public const byte TypePingReq = 0xC0;
        public const byte TypePingResp = 0xD0;
        public const byte TypeDisconnect = 0xE0;

        public const byte ConnectFlagCleanSession = 0x02;
        public const byte ConnectFlagWill = 0x04;
        public const byte ConnectFlagWillRetain = 0x20;
        public const byte ConnectFlagPassword = 0x40;
        public const byte ConnectFlagUsername = 0x80;

        // CONNACK return codes (MQTT 3.1.1 §3.2.2.3).
        public const byte ConnAckAccepted = 0x00;
        public const byte ConnAckUnacceptableProtocolVersion = 0x01;
        public const byte ConnAckIdentifierRejected = 0x02;
        public const byte ConnAckServerUnavailable = 0x03;
        public const byte ConnAckBadCredentials = 0x04;
        public const byte ConnAckNotAuthorized = 0x05;

        /// <summary>
        /// Encodes a CONNECT packet for MQTT 3.1.1.
        /// </summary>
        public static byte[] EncodeConnect(
            string clientId,
            ushort keepAliveSeconds,
            bool cleanSession,
            string username,
            string password,
            string willTopic,
            byte[] willPayload,
            byte willQos,
            bool willRetain)
        {
            if (clientId == null) clientId = string.Empty;

            byte[] clientIdBytes = AsciiCodec.GetBytes(clientId);
            byte[] usernameBytes = string.IsNullOrEmpty(username) ? null : AsciiCodec.GetBytes(username);
            byte[] passwordBytes = string.IsNullOrEmpty(password) ? null : AsciiCodec.GetBytes(password);
            byte[] willTopicBytes = string.IsNullOrEmpty(willTopic) ? null : AsciiCodec.GetBytes(willTopic);
            byte[] willPayloadBytes = willPayload;

            byte connectFlags = 0;
            if (cleanSession) connectFlags |= ConnectFlagCleanSession;
            if (willTopicBytes != null)
            {
                connectFlags |= ConnectFlagWill;
                connectFlags |= (byte)((willQos & 0x03) << 3);
                if (willRetain) connectFlags |= ConnectFlagWillRetain;
            }
            if (usernameBytes != null) connectFlags |= ConnectFlagUsername;
            if (passwordBytes != null) connectFlags |= ConnectFlagPassword;

            // Variable header: protocol name "MQTT", protocol level 4, connect flags, keep alive
            // = 2 + 4 + 1 + 1 + 2 = 10 bytes.
            int variableLen = 10;

            int payloadLen = 2 + clientIdBytes.Length;
            if (willTopicBytes != null)
            {
                payloadLen += 2 + willTopicBytes.Length;
                payloadLen += 2 + (willPayloadBytes != null ? willPayloadBytes.Length : 0);
            }
            if (usernameBytes != null) payloadLen += 2 + usernameBytes.Length;
            if (passwordBytes != null) payloadLen += 2 + passwordBytes.Length;

            int remainingLen = variableLen + payloadLen;
            byte[] remainingLenBytes = EncodeRemainingLength(remainingLen);

            byte[] packet = new byte[1 + remainingLenBytes.Length + remainingLen];
            int p = 0;
            packet[p++] = TypeConnect;
            for (int i = 0; i < remainingLenBytes.Length; i++) packet[p++] = remainingLenBytes[i];

            // Protocol name "MQTT".
            packet[p++] = 0x00; packet[p++] = 0x04;
            packet[p++] = (byte)'M'; packet[p++] = (byte)'Q'; packet[p++] = (byte)'T'; packet[p++] = (byte)'T';
            packet[p++] = 0x04; // Protocol level 4 (3.1.1).
            packet[p++] = connectFlags;
            packet[p++] = (byte)((keepAliveSeconds >> 8) & 0xFF);
            packet[p++] = (byte)(keepAliveSeconds & 0xFF);

            p = WriteLengthPrefixed(packet, p, clientIdBytes);
            if (willTopicBytes != null)
            {
                p = WriteLengthPrefixed(packet, p, willTopicBytes);
                p = WriteLengthPrefixed(packet, p, willPayloadBytes ?? new byte[0]);
            }
            if (usernameBytes != null) p = WriteLengthPrefixed(packet, p, usernameBytes);
            if (passwordBytes != null) p = WriteLengthPrefixed(packet, p, passwordBytes);

            return packet;
        }

        /// <summary>
        /// Decodes a CONNACK packet payload (after the fixed header).
        /// </summary>
        public static byte DecodeConnAckReturnCode(byte[] variablePayload)
        {
            if (variablePayload == null || variablePayload.Length < 2)
            {
                throw new InvalidOperationException("CONNACK payload too short");
            }
            return variablePayload[1];
        }

        /// <summary>
        /// Encodes a PUBLISH packet. QoS must be 0 or 1.
        /// </summary>
        public static byte[] EncodePublish(string topic, byte[] payload, byte qos, bool retain, ushort packetId)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (qos > 1) throw new ArgumentException("Only QoS 0 and 1 are supported");

            byte[] topicBytes = AsciiCodec.GetBytes(topic);
            int payloadLen = payload != null ? payload.Length : 0;

            int variableLen = 2 + topicBytes.Length + (qos > 0 ? 2 : 0);
            int remainingLen = variableLen + payloadLen;
            byte[] remainingLenBytes = EncodeRemainingLength(remainingLen);

            byte fixedHeader = TypePublish;
            fixedHeader |= (byte)((qos & 0x03) << 1);
            if (retain) fixedHeader |= 0x01;

            byte[] packet = new byte[1 + remainingLenBytes.Length + remainingLen];
            int p = 0;
            packet[p++] = fixedHeader;
            for (int i = 0; i < remainingLenBytes.Length; i++) packet[p++] = remainingLenBytes[i];

            p = WriteLengthPrefixed(packet, p, topicBytes);
            if (qos > 0)
            {
                packet[p++] = (byte)((packetId >> 8) & 0xFF);
                packet[p++] = (byte)(packetId & 0xFF);
            }
            if (payloadLen > 0)
            {
                Array.Copy(payload, 0, packet, p, payloadLen);
            }

            return packet;
        }

        /// <summary>
        /// Decodes a PUBLISH packet's variable header and payload (after the fixed header).
        /// </summary>
        public static void DecodePublish(byte fixedHeader, byte[] variablePayload, out string topic, out byte[] payload, out byte qos, out ushort packetId)
        {
            qos = (byte)((fixedHeader >> 1) & 0x03);
            int p = 0;
            int topicLen = (variablePayload[p] << 8) | variablePayload[p + 1];
            p += 2;
            topic = AsciiCodec.GetString(variablePayload, p, topicLen);
            p += topicLen;

            if (qos > 0)
            {
                packetId = (ushort)((variablePayload[p] << 8) | variablePayload[p + 1]);
                p += 2;
            }
            else
            {
                packetId = 0;
            }

            int payloadLen = variablePayload.Length - p;
            payload = new byte[payloadLen];
            if (payloadLen > 0)
            {
                Array.Copy(variablePayload, p, payload, 0, payloadLen);
            }
        }

        /// <summary>
        /// Encodes a PUBACK packet (response to received QoS 1 PUBLISH).
        /// </summary>
        public static byte[] EncodePubAck(ushort packetId)
        {
            return new byte[] { TypePubAck, 0x02, (byte)((packetId >> 8) & 0xFF), (byte)(packetId & 0xFF) };
        }

        /// <summary>
        /// Encodes a SUBSCRIBE packet for one topic at the given QoS.
        /// </summary>
        public static byte[] EncodeSubscribe(ushort packetId, string topic, byte qos)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            byte[] topicBytes = AsciiCodec.GetBytes(topic);
            int remainingLen = 2 + 2 + topicBytes.Length + 1;
            byte[] remainingLenBytes = EncodeRemainingLength(remainingLen);

            byte[] packet = new byte[1 + remainingLenBytes.Length + remainingLen];
            int p = 0;
            packet[p++] = TypeSubscribe;
            for (int i = 0; i < remainingLenBytes.Length; i++) packet[p++] = remainingLenBytes[i];
            packet[p++] = (byte)((packetId >> 8) & 0xFF);
            packet[p++] = (byte)(packetId & 0xFF);
            p = WriteLengthPrefixed(packet, p, topicBytes);
            packet[p++] = (byte)(qos & 0x03);
            return packet;
        }

        /// <summary>
        /// Encodes a single-byte fixed-header packet (PINGREQ, DISCONNECT).
        /// </summary>
        public static byte[] EncodeBare(byte type)
        {
            return new byte[] { type, 0x00 };
        }

        /// <summary>
        /// Encodes the MQTT remaining-length field (1-4 bytes, base 128 varint).
        /// </summary>
        public static byte[] EncodeRemainingLength(int length)
        {
            if (length < 0) throw new ArgumentException("Remaining length must be non-negative");
            if (length > 268435455) throw new ArgumentException("Remaining length exceeds MQTT maximum");

            // At most 4 bytes.
            byte[] tmp = new byte[4];
            int count = 0;
            int x = length;
            do
            {
                byte b = (byte)(x & 0x7F);
                x >>= 7;
                if (x > 0) b |= 0x80;
                tmp[count++] = b;
            } while (x > 0);

            byte[] result = new byte[count];
            for (int i = 0; i < count; i++) result[i] = tmp[i];
            return result;
        }

        /// <summary>
        /// Decodes the MQTT remaining-length field starting at the given offset.
        /// Returns the parsed value; sets bytesConsumed to the number of bytes read.
        /// </summary>
        public static int DecodeRemainingLength(byte[] buffer, int offset, out int bytesConsumed)
        {
            int multiplier = 1;
            int value = 0;
            int p = offset;
            byte b;
            int read = 0;
            do
            {
                if (p >= buffer.Length)
                {
                    throw new InvalidOperationException("Remaining length truncated");
                }
                b = buffer[p++];
                value += (b & 0x7F) * multiplier;
                multiplier *= 128;
                read++;
                if (multiplier > 128 * 128 * 128 * 128)
                {
                    throw new InvalidOperationException("Malformed remaining length");
                }
            } while ((b & 0x80) != 0);

            bytesConsumed = read;
            return value;
        }

        private static int WriteLengthPrefixed(byte[] dst, int p, byte[] src)
        {
            int len = src.Length;
            dst[p++] = (byte)((len >> 8) & 0xFF);
            dst[p++] = (byte)(len & 0xFF);
            if (len > 0) Array.Copy(src, 0, dst, p, len);
            return p + len;
        }
    }
}
