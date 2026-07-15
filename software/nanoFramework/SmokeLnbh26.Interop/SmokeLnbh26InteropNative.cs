using System.Runtime.CompilerServices;

namespace Cubley.Interop
{
    public static class BringupStatus
    {
        // Managed marker to ensure this assembly contains at least one IL body.
        public static uint NativeShimMarker()
        {
            return 0x4C4E4231u;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSet(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGet();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetLastNativeError();
    }

    public static class LNBH26
    {
        public enum Voltage
        {
            V13 = 0,
            V18 = 1
        }

        public enum Polarization
        {
            Vertical = 0,
            Horizontal = 1
        }

        public enum Band
        {
            Low = 0,
            High = 1
        }

        public enum Status
        {
            Ok = 0,
            InvalidParam = 1,
            NotInitialized = 2,
            IoError = 3
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
        public static extern int NativeSetPolarization(int polarization);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetTone(bool enable);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSetBand(int band);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetVoltage();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool NativeGetTone();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetPolarization();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGetBand();
    }
}
