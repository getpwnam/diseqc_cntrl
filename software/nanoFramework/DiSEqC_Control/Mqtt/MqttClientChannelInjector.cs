using System;
using System.Reflection;
using nanoFramework.M2Mqtt;

namespace DiSEqC_Control.Mqtt
{
    internal static class MqttClientChannelInjector
    {
        private const string MqttClientChannelFieldName = "_channel";

        public static bool TryInject(MqttClient mqttClient, IMqttNetworkChannel channel, out string error)
        {
            if (mqttClient == null)
            {
                error = "mqttClient is null";
                return false;
            }

            if (channel == null)
            {
                error = "channel is null";
                return false;
            }

            try
            {
                FieldInfo channelField = typeof(MqttClient).GetField(MqttClientChannelFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (channelField == null)
                {
                    error = "MqttClient private channel field not found";
                    return false;
                }

                channel.ValidateServerCertificate = mqttClient.Settings.ValidateServerCertificate;
                channelField.SetValue(mqttClient, channel);

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
