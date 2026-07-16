namespace CubleyControl.Contracts
{
    public static class CommandIds
    {
        // DiSEqC commands
        public const string DiseqcRotorGotoAngle = "diseqc.rotor.goto_angle";
        public const string DiseqcRotorGotoSatellite = "diseqc.rotor.goto_satellite";
        public const string DiseqcRotorHalt = "diseqc.rotor.halt";
        public const string DiseqcRotorStepEast = "diseqc.rotor.step_east";
        public const string DiseqcRotorStepWest = "diseqc.rotor.step_west";
        public const string DiseqcRotorDriveEast = "diseqc.rotor.drive_east";
        public const string DiseqcRotorDriveWest = "diseqc.rotor.drive_west";

        public const string DiseqcLnbSetVoltage = "diseqc.lnb.set_voltage";
        public const string DiseqcLnbSetPolarization = "diseqc.lnb.set_polarization";
        public const string DiseqcLnbSetTone = "diseqc.lnb.set_tone";
        public const string DiseqcLnbSetBand = "diseqc.lnb.set_band";

        public const string DiseqcCalibrationSetReference = "diseqc.calibration.set_reference";

        // System commands
        public const string SystemConfigGet = "system.config.get";
        public const string SystemConfigSet = "system.config.set";
        public const string SystemConfigSave = "system.config.save";
        public const string SystemConfigReset = "system.config.reset";
        public const string SystemConfigReload = "system.config.reload";
        public const string SystemConfigFramClear = "system.config.fram_clear";

        public const string SystemCapabilitiesGet = "system.capabilities.get";
        public const string SystemVersionGet = "system.version.get";
    }
}
