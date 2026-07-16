namespace DiSEqC_Control.Mqtt
{
    internal static class MqttCommandRouter
    {
        private static bool TopicMatches(string topic, string commandSuffix)
        {
            return topic.EndsWith(commandSuffix);
        }

        public static bool TryHandle(string topic, string payload, IMqttCommandSink sink)
        {
            if (topic == null || sink == null)
            {
                return false;
            }

            if (TopicMatches(topic, "/command/goto/angle")) { sink.HandleGotoAngle(payload); return true; }
            if (TopicMatches(topic, "/command/goto/satellite")) { sink.HandleGotoSatellite(payload); return true; }
            if (TopicMatches(topic, "/command/halt")) { sink.HandleHalt(); return true; }
            if (TopicMatches(topic, "/command/manual/step_east")) { sink.HandleStepEast(payload); return true; }
            if (TopicMatches(topic, "/command/manual/step_west")) { sink.HandleStepWest(payload); return true; }
            if (TopicMatches(topic, "/command/manual/drive_east")) { sink.HandleDriveEast(); return true; }
            if (TopicMatches(topic, "/command/manual/drive_west")) { sink.HandleDriveWest(); return true; }
            if (TopicMatches(topic, "/command/lnb/voltage")) { sink.HandleLnbVoltage(payload); return true; }
            if (TopicMatches(topic, "/command/lnb/polarization")) { sink.HandleLnbPolarization(payload); return true; }
            if (TopicMatches(topic, "/command/lnb/tone")) { sink.HandleLnbTone(payload); return true; }
            if (TopicMatches(topic, "/command/lnb/band")) { sink.HandleLnbBand(payload); return true; }
            if (TopicMatches(topic, "/command/calibrate/reference")) { sink.HandleCalibrateReference(); return true; }

            return false;
        }
    }
}
