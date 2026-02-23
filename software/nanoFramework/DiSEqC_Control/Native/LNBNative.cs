using System;
using System.Runtime.CompilerServices;

namespace DiSEqC_Control.Native
{
    /// <summary>
    /// LNB (Low Noise Block) Control
    /// Controls voltage (13V/18V) and 22kHz tone for satellite reception
    /// </summary>
    public static class LNB
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
            NotInitialized = 2
        }

        /// <summary>
        /// Set LNB voltage (13V or 18V)
        /// </summary>
        /// <param name="voltage">Voltage selection</param>
        /// <returns>Status code</returns>
        public static Status SetVoltage(Voltage voltage)
        {
            int result = NativeSetVoltage((int)voltage);
            return (Status)result;
        }

        /// <summary>
        /// Set LNB polarization (convenience method)
        /// </summary>
        /// <param name="polarization">Polarization selection</param>
        /// <returns>Status code</returns>
        public static Status SetPolarization(Polarization polarization)
        {
            int result = NativeSetPolarization((int)polarization);
            return (Status)result;
        }

        /// <summary>
        /// Enable or disable 22kHz tone
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        /// <returns>Status code</returns>
        public static Status SetTone(bool enable)
        {
            int result = NativeSetTone(enable);
            return (Status)result;
        }

        /// <summary>
        /// Set LNB band (convenience method)
        /// </summary>
        /// <param name="band">Band selection</param>
        /// <returns>Status code</returns>
        public static Status SetBand(Band band)
        {
            int result = NativeSetBand((int)band);
            return (Status)result;
        }

        /// <summary>
        /// Get current voltage setting
        /// </summary>
        /// <returns>Current voltage</returns>
        public static Voltage GetVoltage()
        {
            int result = NativeGetVoltage();
            return (Voltage)result;
        }

        /// <summary>
        /// Get current 22kHz tone state
        /// </summary>
        /// <returns>True if tone is enabled</returns>
        public static bool GetTone()
        {
            return NativeGetTone();
        }

        /// <summary>
        /// Get current polarization
        /// </summary>
        /// <returns>Current polarization</returns>
        public static Polarization GetPolarization()
        {
            int result = NativeGetPolarization();
            return (Polarization)result;
        }

        /// <summary>
        /// Get current band
        /// </summary>
        /// <returns>Current band</returns>
        public static Band GetBand()
        {
            int result = NativeGetBand();
            return (Band)result;
        }

        /* Native method declarations */
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeSetVoltage(int voltage);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeSetPolarization(int polarization);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeSetTone(bool enable);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeSetBand(int band);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeGetVoltage();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern bool NativeGetTone();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeGetPolarization();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeGetBand();
    }
}
