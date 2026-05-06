namespace DiSEqC_Control.Mqtt
{
    /// <summary>
    /// Sink for rotor + LNB commands. Implemented by the application; supplied to
    /// <see cref="MqttCommandRouter.TryHandle"/>. Defined as a single interface to keep the
    /// router's parameter count low — nanoFramework's CLR fails to load assemblies that
    /// declare static methods with too many delegate parameters.
    /// </summary>
    public interface IMqttCommandSink
    {
        void HandleGotoAngle(string payload);
        void HandleGotoSatellite(string payload);
        void HandleHalt();
        void HandleStepEast(string payload);
        void HandleStepWest(string payload);
        void HandleDriveEast();
        void HandleDriveWest();
        void HandleLnbVoltage(string payload);
        void HandleLnbPolarization(string payload);
        void HandleLnbTone(string payload);
        void HandleLnbBand(string payload);
        void HandleCalibrateReference();
    }

    /// <summary>
    /// Sink for config commands. Same rationale as <see cref="IMqttCommandSink"/>.
    /// </summary>
    public interface IMqttConfigSink
    {
        void PublishStatus(string subtopic, string value);
        void PublishError(string message);
        void PublishEffectiveConfig();
        void HandleConfigSave();
        void HandleConfigReset();
        void HandleConfigReload();
        void HandleConfigFramClear(string token);
    }
}
