using System.Runtime.CompilerServices;

    


namespace Cubley.Interop
{
    public static class DiagMailbox
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

        public enum Register
        {
            Status1 = 0,
            Status2 = 1,
            Data1 = 2,
            Data2 = 3,
            Data3 = 4,
            Data4 = 5,
        }

        [System.Flags]
        public enum Status1Flags
        {
            OlfA = 1 << 0,
            OlfB = 1 << 1,
            VmonA = 1 << 2,
            VmonB = 1 << 3,
            PdoA = 1 << 4,
            PdoB = 1 << 5,
            Otf = 1 << 6,
            Png = 1 << 7,
        }

        [System.Flags]
        public enum Status2Flags
        {
            TdetA = 1 << 0,
            TdetB = 1 << 1,
            TmonA = 1 << 2,
            TmonB = 1 << 3,
            ImonA = 1 << 4,
            ImonB = 1 << 5,
        }

        [System.Flags]
        public enum Data2Flags
        {
            TenA = 1 << 0,
            LpmA = 1 << 1,
            ExtmA = 1 << 2,
            TenB = 1 << 4,
            LpmB = 1 << 5,
            ExtmB = 1 << 6,
        }

        [System.Flags]
        public enum Data3Flags
        {
            IsetA = 1 << 0,
            IswA = 1 << 1,
            PclA = 1 << 2,
            TimerA = 1 << 3,
            IsetB = 1 << 4,
            IswB = 1 << 5,
            PclB = 1 << 6,
            TimerB = 1 << 7,
        }

        [System.Flags]
        public enum Data4Flags
        {
            EnImonA = 1 << 0,
            Olr = 1 << 3,
            EnImonB = 1 << 4,
            Therm = 1 << 6,
            Comp = 1 << 7,
        }

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

    public static class LNBH26Registers
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReadRegister(int registerAddress, out int registerValue);
    }

    public static class Fram24C128
    {
        public enum Status { Ok = 0, InvalidParam = 1, NotInitialized = 2, IoError = 3 }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeInit();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeWrite(int address, byte[] buffer, int offset, int count);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeRead(int address, byte[] buffer, int offset, int count);
    }
}
