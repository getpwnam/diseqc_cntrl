using System;

namespace DiSEqC_Control.Mqtt
{
    internal static class MqttCommandRouter
    {
        private static bool TopicMatches(string topic, string commandSuffix)
        {
            return topic.EndsWith(commandSuffix);
        }

        public static bool TryHandle(
            string topic,
            string payload,
            Action<string> handleGotoAngle,
            Action<string> handleGotoSatellite,
            Action handleHalt,
            Action<string> handleStepEast,
            Action<string> handleStepWest,
            Action handleDriveEast,
            Action handleDriveWest,
            Action<string> handleLnbVoltage,
            Action<string> handleLnbPolarization,
            Action<string> handleLnbTone,
            Action<string> handleLnbBand,
            Action handleCalibrateReference)
        {
            if (topic == null)
            {
                return false;
            }

            if (TopicMatches(topic, "/command/goto/angle"))
            {
                handleGotoAngle(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/goto/satellite"))
            {
                handleGotoSatellite(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/halt"))
            {
                handleHalt();
                return true;
            }

            if (TopicMatches(topic, "/command/manual/step_east"))
            {
                handleStepEast(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/manual/step_west"))
            {
                handleStepWest(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/manual/drive_east"))
            {
                handleDriveEast();
                return true;
            }

            if (TopicMatches(topic, "/command/manual/drive_west"))
            {
                handleDriveWest();
                return true;
            }

            if (TopicMatches(topic, "/command/lnb/voltage"))
            {
                handleLnbVoltage(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/lnb/polarization"))
            {
                handleLnbPolarization(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/lnb/tone"))
            {
                handleLnbTone(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/lnb/band"))
            {
                handleLnbBand(payload);
                return true;
            }

            if (TopicMatches(topic, "/command/calibrate/reference"))
            {
                handleCalibrateReference();
                return true;
            }

            return false;
        }
    }
}
