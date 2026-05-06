using System;
using DiSEqC_Control.Native;

namespace DiSEqC_Control.Mqtt
{
    internal sealed class W5500MqttNetworkChannelCore
    {
        private readonly string _remoteHost;
        private readonly int _remotePort;
        private readonly int _defaultConnectTimeoutMs;
        private readonly int _defaultReceiveTimeoutMs;
        private readonly IW5500SocketApi _socketApi;

        private int _socketHandle = -1;

        public W5500MqttNetworkChannelCore(
            string remoteHost,
            int remotePort,
            int connectTimeoutMs,
            int receiveTimeoutMs,
            IW5500SocketApi socketApi)
        {
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            _defaultConnectTimeoutMs = connectTimeoutMs;
            _defaultReceiveTimeoutMs = receiveTimeoutMs;
            _socketApi = socketApi;
        }

        public bool DataAvailable => _socketHandle >= 0 && _socketApi.IsConnected(_socketHandle);

        public void Connect()
        {
            BringupBeacon(0xC0, 0x00);
            System.Threading.Thread.Sleep(800);
            if (string.IsNullOrEmpty(_remoteHost) || _remotePort < 1 || _remotePort > 65535)
            {
                BringupBeacon(0xC0, 0xFE);
                throw new InvalidOperationException("Invalid remote endpoint for MQTT channel");
            }

            BringupBeacon(0xC1, 0x00);
            System.Threading.Thread.Sleep(800);
            if (_socketHandle >= 0 && _socketApi.IsConnected(_socketHandle))
            {
                BringupBeacon(0xC1, 0x01);
                return;
            }

            BringupBeacon(0xC2, 0x00);
            System.Threading.Thread.Sleep(800);
            Close();

            BringupBeacon(0xC3, 0x00);
            System.Threading.Thread.Sleep(800);
            int socketHandle;
            W5500Socket.Status openStatus = _socketApi.Open(out socketHandle);
            BringupBeacon(0xC4, (byte)(((int)openStatus & 0x0F) | ((socketHandle & 0x0F) << 4)));
            System.Threading.Thread.Sleep(800);
            EnsureSuccess(openStatus, "open W5500 socket");

            BringupBeacon(0xC5, 0x00);
            System.Threading.Thread.Sleep(800);
            W5500Socket.Status connectStatus = _socketApi.Connect(socketHandle, _remoteHost, _remotePort, _defaultConnectTimeoutMs);
            BringupBeacon(0xC6, (byte)connectStatus);
            System.Threading.Thread.Sleep(800);
            EnsureSuccess(connectStatus, "connect W5500 socket");

            _socketHandle = socketHandle;
            BringupBeacon(0xC7, 0x00);
            System.Threading.Thread.Sleep(800);
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

        public int Send(byte[] buffer)
        {
            BringupBeacon(0xE0, (byte)(buffer == null ? 0xFF : buffer.Length));
            System.Threading.Thread.Sleep(800);
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            EnsureConnected();
            BringupBeacon(0xE1, 0x00);
            System.Threading.Thread.Sleep(800);

            int offset = 0;
            int remaining = buffer.Length;

            while (remaining > 0)
            {
                BringupBeacon(0xE4, (byte)remaining);
                System.Threading.Thread.Sleep(800);
                W5500Socket.Status sendStatus = _socketApi.Send(_socketHandle, buffer, offset, remaining, out int sent);
                BringupBeacon(0xE2, (byte)(((int)sendStatus & 0x0F) | ((sent & 0x0F) << 4)));
                System.Threading.Thread.Sleep(800);
                EnsureSuccess(sendStatus, "send W5500 payload");

                if (sent <= 0)
                {
                    throw new InvalidOperationException("W5500 send returned 0 bytes");
                }

                offset += sent;
                remaining -= sent;
            }

            BringupBeacon(0xE3, 0x00);
            System.Threading.Thread.Sleep(400);
            return buffer.Length;
        }

        public int Receive(byte[] buffer)
        {
            return Receive(buffer, _defaultReceiveTimeoutMs);
        }

        public int Receive(byte[] buffer, int timeout)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (timeout < 0)
            {
                timeout = _defaultReceiveTimeoutMs;
            }

            EnsureConnected();

            W5500Socket.Status receiveStatus = _socketApi.Receive(_socketHandle, buffer, 0, buffer.Length, timeout, out int received);
            if (receiveStatus == W5500Socket.Status.Timeout)
            {
                return 0;
            }

            EnsureSuccess(receiveStatus, "receive W5500 payload");
            return received;
        }

        public void Close()
        {
            if (_socketHandle < 0)
            {
                return;
            }

            _socketApi.Close(_socketHandle);
            _socketHandle = -1;
        }

        private void EnsureConnected()
        {
            if (_socketHandle < 0 || !_socketApi.IsConnected(_socketHandle))
            {
                throw new InvalidOperationException("W5500 MQTT channel is not connected");
            }
        }

        private static void EnsureSuccess(W5500Socket.Status status, string operation)
        {
            if (status == W5500Socket.Status.Ok)
            {
                return;
            }

            throw new InvalidOperationException("Failed to " + operation + ": " + status);
        }
    }
}