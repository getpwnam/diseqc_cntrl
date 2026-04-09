using System;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace DiSEqC_Control.Mqtt
{
    internal sealed class MqttClientFacade : IMqttClientFacade
    {
        private readonly MqttClient _client;

        public MqttClientFacade(MqttClient client)
        {
            _client = client;
        }

        public event IMqttClient.MqttMsgPublishEventHandler MessageReceived
        {
            add => _client.MqttMsgPublishReceived += value;
            remove => _client.MqttMsgPublishReceived -= value;
        }

        public event IMqttClient.ConnectionClosedEventHandler ConnectionClosed
        {
            add => _client.ConnectionClosed += value;
            remove => _client.ConnectionClosed -= value;
        }

        public bool IsConnected => _client.IsConnected;

        public MqttReasonCode Connect(
            string clientId,
            string username,
            string password,
            bool willRetain,
            MqttQoSLevel willQos,
            bool willFlag,
            string willTopic,
            string willMessage,
            bool cleanSession,
            ushort keepAlivePeriod)
        {
            return _client.Connect(
                clientId,
                username,
                password,
                willRetain,
                willQos,
                willFlag,
                willTopic,
                willMessage,
                cleanSession,
                keepAlivePeriod);
        }

        public void Subscribe(string[] topics, MqttQoSLevel[] qosLevels)
        {
            _client.Subscribe(topics, qosLevels);
        }

        public void Publish(string topic, byte[] payload, MqttQoSLevel qosLevel, bool retained)
        {
            _client.Publish(topic, payload, null, null, qosLevel, retained);
        }
    }
}
