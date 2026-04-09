using System;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace DiSEqC_Control.Mqtt
{
    internal interface IMqttClientFacade
    {
        event IMqttClient.MqttMsgPublishEventHandler MessageReceived;
        event IMqttClient.ConnectionClosedEventHandler ConnectionClosed;

        bool IsConnected { get; }

        MqttReasonCode Connect(
            string clientId,
            string username,
            string password,
            bool willRetain,
            MqttQoSLevel willQos,
            bool willFlag,
            string willTopic,
            string willMessage,
            bool cleanSession,
            ushort keepAlivePeriod);

        void Subscribe(string[] topics, MqttQoSLevel[] qosLevels);

        void Publish(
            string topic,
            byte[] payload,
            MqttQoSLevel qosLevel,
            bool retained);
    }
}
