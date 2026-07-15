using System.Runtime.CompilerServices;

    


namespace Cubley.Interop
{
    public static class BringupStatus
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSet(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGet();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetLastNativeError();
    }

    public static class DiagnosticsMailbox
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool NativeTryLatchBootProbe(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetBootProbe();
    }

    public static class LNBH26
    {
        public enum Voltage { V13 = 0, V18 = 1 }
        public enum Polarization { Vertical = 0, Horizontal = 1 }
        public enum Band { Low = 0, High = 1 }
        public enum Status { Ok = 0, InvalidParam = 1, NotInitialized = 2, IoError = 3 }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeInit();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetEnable(bool enable);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReadStatus(out int statusRegister);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetVoltage(int voltage);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetTone(bool enable);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetVoltage();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool NativeGetTone();
    }

    public static class StatusLed
    {
        /// <summary>
        /// Initialize PB0 as GPIO output. Must be called before any LED operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeInit();

        /// <summary>
        /// Set PB0 HIGH (LED ON).
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSetHigh();

        /// <summary>
        /// Set PB0 LOW (LED OFF).
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSetLow();

        /// <summary>
        /// Pulse PB0 for bootup marker: count blinks of pulseMs duration each (HIGH then LOW).
        /// Example: Pulse(3, 300) blinks 3x with 300ms on, 300ms off.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativePulse(int count, int pulseMs);
    }

    public static class UsbCdcConsole
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool NativeIsEnabled();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReadByte(int timeoutMs);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeWrite(string text);
    }
}
