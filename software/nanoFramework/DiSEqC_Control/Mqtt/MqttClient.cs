using System;
using System.Threading;

namespace DiSEqC_Control.Mqtt
{
    /// <summary>
    /// Lightweight MQTT 3.1.1 client built directly on the W5500 socket abstraction.
    /// Replaces the previous M2Mqtt-based facade; avoids any dependency on System.Net.
    ///
    /// Supports QoS 0 publish/receive and QoS 0/1 subscribe. PINGREQ keep-alive is
    /// driven by an internal timer thread. A reader thread parses incoming packets
    /// and fires <see cref="MessageReceived"/> for PUBLISH frames.
    /// </summary>
    internal sealed class MqttClient
    {
        public delegate void MessageReceivedEventHandler(string topic, byte[] payload);
        public delegate void ConnectionClosedEventHandler();

        private readonly W5500MqttNetworkChannelCore _channel;
        private readonly object _writeLock = new object();
        private readonly object _stateLock = new object();

        private Thread _readerThread;
        private Thread _keepAliveThread;
        private bool _running;
        private bool _connected;
        private ushort _nextPacketId = 1;
        private int _keepAliveSeconds;
        private long _lastWriteTicksMs;

        public event MessageReceivedEventHandler MessageReceived;
        public event ConnectionClosedEventHandler ConnectionClosed;

        public MqttClient(W5500MqttNetworkChannelCore channel)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            _channel = channel;
        }

        public bool IsConnected
        {
            get { lock (_stateLock) { return _connected; } }
        }

        /// <summary>
        /// Opens the TCP connection, sends CONNECT, waits for CONNACK.
        /// Returns the CONNACK return code (0 = accepted).
        /// Throws InvalidOperationException on socket-level failure or timeout.
        /// </summary>
        public byte Connect(
            string clientId,
            ushort keepAliveSeconds,
            bool cleanSession,
            string username,
            string password,
            string willTopic,
            byte[] willPayload,
            byte willQos,
            bool willRetain,
            int connAckTimeoutMs)
        {
            BringupBeacon(0xD0, 0x00);
            System.Threading.Thread.Sleep(800);
            _channel.Connect();
            BringupBeacon(0xD1, 0x00);
            System.Threading.Thread.Sleep(800);

            byte[] connectPacket = MqttPacket.EncodeConnect(
                clientId, keepAliveSeconds, cleanSession,
                username, password,
                willTopic, willPayload, willQos, willRetain);

            BringupBeacon(0xD2, 0x00);
            System.Threading.Thread.Sleep(800);
            WriteRaw(connectPacket);
            BringupBeacon(0xD3, 0x00);
            System.Threading.Thread.Sleep(800);

            // Wait synchronously for CONNACK before starting reader.
            byte[] fixedAndPayload = ReadOnePacket(connAckTimeoutMs);
            BringupBeacon(0xD4, (byte)(fixedAndPayload == null ? 0xFF : fixedAndPayload.Length));
            System.Threading.Thread.Sleep(800);
            if (fixedAndPayload == null)
            {
                throw new InvalidOperationException("Timed out waiting for CONNACK");
            }

            byte type = (byte)(fixedAndPayload[0] & 0xF0);
            if (type != MqttPacket.TypeConnAck)
            {
                throw new InvalidOperationException("Expected CONNACK, got 0x" + type.ToString("X2"));
            }

            // Variable-header bytes start after the fixed header. ReadOnePacket returns
            // [fixedHeaderByte][... varint length bytes already stripped ...][variable+payload].
            // Convention: fixedAndPayload[0] = fixed header byte, fixedAndPayload[1..] = variable+payload.
            byte returnCode = fixedAndPayload[2];

            lock (_stateLock)
            {
                _connected = (returnCode == MqttPacket.ConnAckAccepted);
                _running = _connected;
                _keepAliveSeconds = keepAliveSeconds;
                _lastWriteTicksMs = NowMs();
            }

            if (_connected)
            {
                StartBackgroundThreads();
            }

            return returnCode;
        }

        /// <summary>
        /// Subscribes to a single topic at the given QoS. Fire-and-forget at the API
        /// level — the SUBACK is consumed by the reader thread.
        /// </summary>
        public void Subscribe(string topic, byte qos)
        {
            EnsureConnected();
            ushort id = NextPacketId();
            byte[] packet = MqttPacket.EncodeSubscribe(id, topic, qos);
            WriteRaw(packet);
        }

        /// <summary>
        /// Publishes a message. Only QoS 0 is fully supported here (no PUBACK wait).
        /// </summary>
        public void Publish(string topic, byte[] payload, byte qos, bool retain)
        {
            EnsureConnected();
            ushort id = qos > 0 ? NextPacketId() : (ushort)0;
            byte[] packet = MqttPacket.EncodePublish(topic, payload, qos, retain, id);
            WriteRaw(packet);
        }

        public void Disconnect()
        {
            lock (_stateLock)
            {
                if (!_connected) return;
                _connected = false;
                _running = false;
            }

            try
            {
                WriteRaw(MqttPacket.EncodeBare(MqttPacket.TypeDisconnect));
            }
            catch
            {
                // Best-effort.
            }

            try { _channel.Close(); } catch { }
            RaiseConnectionClosed();
        }

        private void StartBackgroundThreads()
        {
            _readerThread = new Thread(ReaderLoop);
            _readerThread.Start();

            _keepAliveThread = new Thread(KeepAliveLoop);
            _keepAliveThread.Start();
        }

        private void ReaderLoop()
        {
            try
            {
                while (true)
                {
                    lock (_stateLock) { if (!_running) return; }

                    byte[] packet = ReadOnePacket(_keepAliveSeconds > 0 ? (_keepAliveSeconds * 1000 * 2) : 5000);
                    if (packet == null) continue;

                    HandleIncoming(packet);
                }
            }
            catch (Exception)
            {
                // Drop the connection on any read/parse error.
                lock (_stateLock) { _connected = false; _running = false; }
                try { _channel.Close(); } catch { }
                RaiseConnectionClosed();
            }
        }

        private void KeepAliveLoop()
        {
            // Send PINGREQ when more than half the keep-alive interval has elapsed
            // since the last write, so the broker never sees us go silent.
            int interval = _keepAliveSeconds > 0 ? (_keepAliveSeconds * 1000) / 2 : 0;
            if (interval <= 0) return;

            while (true)
            {
                Thread.Sleep(interval);
                lock (_stateLock) { if (!_running) return; }

                long now = NowMs();
                long last;
                lock (_stateLock) { last = _lastWriteTicksMs; }

                if ((now - last) >= interval)
                {
                    try
                    {
                        WriteRaw(MqttPacket.EncodeBare(MqttPacket.TypePingReq));
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }

        private void HandleIncoming(byte[] packet)
        {
            byte type = (byte)(packet[0] & 0xF0);
            if (type == MqttPacket.TypePublish)
            {
                int variableLen = packet.Length - 1;
                byte[] variablePayload = new byte[variableLen];
                Array.Copy(packet, 1, variablePayload, 0, variableLen);

                MqttPacket.DecodePublish(packet[0], variablePayload, out string topic, out byte[] payload, out byte qos, out ushort packetId);

                if (qos == 1)
                {
                    try { WriteRaw(MqttPacket.EncodePubAck(packetId)); } catch { }
                }

                MessageReceivedEventHandler handler = MessageReceived;
                if (handler != null)
                {
                    try { handler(topic, payload); } catch { }
                }
            }
            // CONNACK / SUBACK / PUBACK / PINGRESP / UNSUBACK: consume silently.
        }

        /// <summary>
        /// Reads one full MQTT packet from the channel.
        /// Returns a byte array where index 0 is the fixed-header byte and indices 1..N-1
        /// are the variable header + payload. The remaining-length varint is stripped.
        /// Returns null on timeout (no header byte received).
        /// </summary>
        private byte[] ReadOnePacket(int timeoutMs)
        {
            byte[] one = new byte[1];
            int got = _channel.Receive(one, timeoutMs);
            if (got == 0) return null;

            byte fixedHeader = one[0];

            // Read remaining-length varint, one byte at a time.
            byte[] lenBytes = new byte[4];
            int lenCount = 0;
            int multiplier = 1;
            int value = 0;
            byte b;
            do
            {
                int rd = _channel.Receive(one, timeoutMs);
                if (rd == 0) throw new InvalidOperationException("Truncated remaining-length");
                b = one[0];
                lenBytes[lenCount++] = b;
                value += (b & 0x7F) * multiplier;
                multiplier *= 128;
                if (multiplier > 128 * 128 * 128 * 128)
                {
                    throw new InvalidOperationException("Malformed remaining length");
                }
            } while ((b & 0x80) != 0);

            byte[] body = new byte[value];
            int offset = 0;
            while (offset < value)
            {
                byte[] chunk = new byte[value - offset];
                int rd = _channel.Receive(chunk, timeoutMs);
                if (rd == 0) throw new InvalidOperationException("Truncated MQTT body");
                Array.Copy(chunk, 0, body, offset, rd);
                offset += rd;
            }

            byte[] packet = new byte[1 + value];
            packet[0] = fixedHeader;
            if (value > 0) Array.Copy(body, 0, packet, 1, value);
            return packet;
        }

        private void WriteRaw(byte[] packet)
        {
            lock (_writeLock)
            {
                _channel.Send(packet);
            }
            lock (_stateLock) { _lastWriteTicksMs = NowMs(); }
        }

        private ushort NextPacketId()
        {
            lock (_stateLock)
            {
                ushort id = _nextPacketId;
                _nextPacketId = (ushort)(_nextPacketId + 1);
                if (_nextPacketId == 0) _nextPacketId = 1;
                return id;
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected) throw new InvalidOperationException("MQTT client is not connected");
        }

        private void RaiseConnectionClosed()
        {
            ConnectionClosedEventHandler handler = ConnectionClosed;
            if (handler != null)
            {
                try { handler(); } catch { }
            }
        }

        private static long NowMs()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        private static void BringupBeacon(byte stage, byte detail)
        {
            try
            {
                uint word = ((uint)0xD5 << 24) | ((uint)stage << 16) | detail;
                Cubley.Interop.BringupStatus.NativeSet(word);
            }
            catch
            {
            }
        }
    }
}
