using System;
using nanoFramework.M2Mqtt;

namespace DiSEqC_Control.Mqtt
{
    internal sealed class W5500MqttNetworkChannel : IMqttNetworkChannel
    {
        private readonly W5500MqttNetworkChannelCore _core;

        public W5500MqttNetworkChannel(string remoteHost, int remotePort, int connectTimeoutMs, int receiveTimeoutMs)
        {
            _core = new W5500MqttNetworkChannelCore(
                remoteHost,
                remotePort,
                connectTimeoutMs,
                receiveTimeoutMs,
                new W5500SocketApi());
        }

        public bool DataAvailable => _core.DataAvailable;

        public bool ValidateServerCertificate { get; set; }

        public void Connect()
        {
            _core.Connect();
        }

        public int Send(byte[] buffer)
        {
            return _core.Send(buffer);
        }

        public int Receive(byte[] buffer)
        {
            return _core.Receive(buffer);
        }

        public int Receive(byte[] buffer, int timeout)
        {
            return _core.Receive(buffer, timeout);
        }

        public void Close()
        {
            _core.Close();
        }

        public void Accept()
        {
            throw new NotSupportedException("W5500 MQTT channel supports only outbound client connections");
        }
    }
}
