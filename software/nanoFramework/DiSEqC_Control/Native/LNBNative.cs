using CubleyLnb = Cubley.Interop.LNBH26;

namespace DiSEqC_Control.Native
{
    /// <summary>
    /// LNB (Low Noise Block) Control
    /// Controls voltage (13V/18V) and 22kHz tone for satellite reception
    /// </summary>
    public static class LNBH26
    {
        /// <summary>
        /// LNB Voltage levels
        /// </summary>
        public enum Voltage
        {
            V13 = 0,  // 13V - Vertical polarization
            V18 = 1   // 18V - Horizontal polarization
        }

        /// <summary>
        /// LNB Polarization
        /// </summary>
        public enum Polarization
        {
            Vertical = 0,     // 13V
            Horizontal = 1    // 18V
        }

        /// <summary>
        /// LNB Frequency Band
        /// </summary>
        public enum Band
        {
            Low = 0,   // 10.7-11.7 GHz (no 22kHz tone)
            High = 1   // 11.7-12.75 GHz (22kHz tone ON)
        }

        /// <summary>
        /// LNB Status codes
        /// </summary>
        public enum Status
        {
            Ok = 0,
            InvalidParam = 1,
            NotInitialized = 2,
            IoError = 3
        }

        public static Status Init()
        {
            return (Status)CubleyLnb.NativeInit();
        }

        public static Status SetEnable(bool enable)
        {
            return (Status)CubleyLnb.NativeSetEnable(enable);
        }

        public static Status ReadStatus(out int statusRegister)
        {
            return (Status)CubleyLnb.NativeReadStatus(out statusRegister);
        }

        /// <summary>
        /// Set LNB voltage (13V or 18V)
        /// </summary>
        /// <param name="voltage">Voltage selection</param>
        /// <returns>Status code</returns>
        public static Status SetVoltage(Voltage voltage)
        {
            return (Status)CubleyLnb.NativeSetVoltage((int)voltage);
        }

        /// <summary>
        /// Set LNB polarization (convenience method)
        /// </summary>
        /// <param name="polarization">Polarization selection</param>
        /// <returns>Status code</returns>
        public static Status SetPolarization(Polarization polarization)
        {
            return (Status)CubleyLnb.NativeSetPolarization((int)polarization);
        }

        /// <summary>
        /// Enable or disable 22kHz tone
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        /// <returns>Status code</returns>
        public static Status SetTone(bool enable)
        {
            return (Status)CubleyLnb.NativeSetTone(enable);
        }

        /// <summary>
        /// Set LNB band (convenience method)
        /// </summary>
        /// <param name="band">Band selection</param>
        /// <returns>Status code</returns>
        public static Status SetBand(Band band)
        {
            return (Status)CubleyLnb.NativeSetBand((int)band);
        }

        /// <summary>
        /// Get current voltage setting
        /// </summary>
        /// <returns>Current voltage</returns>
        public static Voltage GetVoltage()
        {
            return (Voltage)CubleyLnb.NativeGetVoltage();
        }

        /// <summary>
        /// Get current 22kHz tone state
        /// </summary>
        /// <returns>True if tone is enabled</returns>
        public static bool GetTone()
        {
            return CubleyLnb.NativeGetTone();
        }

        /// <summary>
        /// Get current polarization
        /// </summary>
        /// <returns>Current polarization</returns>
        public static Polarization GetPolarization()
        {
            return (Polarization)CubleyLnb.NativeGetPolarization();
        }

        /// <summary>
        /// Get current band
        /// </summary>
        /// <returns>Current band</returns>
        public static Band GetBand()
        {
            return (Band)CubleyLnb.NativeGetBand();
        }
    }
}
