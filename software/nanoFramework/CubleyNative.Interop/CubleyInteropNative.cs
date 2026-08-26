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

    public static partial class LNBH26
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
    }

    public static partial class LNBH26
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReadStatusPair(out int status1, out int status2);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetPolarizationForChannel(int channel, int polarization);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetBandForChannel(int channel, int band);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetLowPowerForChannel(int channel, bool lowPower);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetDiseqcInputModeForChannel(int channel, int mode);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetPolarizationForChannel(int channel);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetBandForChannel(int channel);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetLastError();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetLastErrorDetail();
    }

    public static class LNBH26Registers
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReadRegister(int registerAddress, out int registerValue);
    }

    public static class UsbCdcConsole
    {
        // Returns 1 if USB CDC is active and ready, 0 otherwise.
        // Declared as int (not bool) to avoid nanoFramework MetaDataProcessor
        // max-stack=0 bug for BOOLEAN return type on InternalCall methods,
        // which causes SetResult_Boolean to assert in the CLR eval stack.
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeIsEnabled();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReadByte(int timeoutMs);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeWrite(string text);
    }

    public static class LNBH26Tweaks
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetIsetLowForChannel(int channel, bool lowRange);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetIswLowForChannel(int channel, bool lowLimit);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetIsetLowForChannel(int channel);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetIswLowForChannel(int channel);
    }

    public static class ZPersistentConfiguration
    {
        public enum Status
        {
            Ok = 0,
            InvalidParam = 1,
            StorageUnavailable = 2,
            LayoutConflict = 3,
            EraseFailed = 4,
            WriteFailed = 5,
            VerifyFailed = 6
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeRead(byte[] buffer, int offset, int count);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeWrite(byte[] buffer, int offset, int count);
    }
}
