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

    public static class W5500Socket
    {
        public enum Status
        {
            Ok = 0,
            InvalidParam = 1,
            NotInitialized = 2,
            Busy = 3,
            Timeout = 4,
            NotSupported = 5,
            IoError = 6
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeOpen(out int socketHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeConfigureNetwork(string localIp, string subnetMask, string gateway, string macAddress);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeConnect(int socketHandle, string host, int port, int timeoutMs);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeSend(int socketHandle, byte[] buffer, int offset, int count, out int bytesSent);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeReceive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int bytesRead);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeClose(int socketHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool NativeIsConnected(int socketHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetPhyStatus();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetVersion();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetVersionPhyStatus();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeSetPhyMode(int modeCode);
    }
    public static class LNBH26
    {
        public enum Voltage { V13 = 0, V18 = 1 }
        public enum Polarization { Vertical = 0, Horizontal = 1 }
        public enum Band { Low = 0, High = 1 }
        public enum Status { Ok = 0, InvalidParam = 1, NotInitialized = 2 }

        public static Status SetVoltage(Voltage voltage)
        {
            int result = NativeSetVoltage((int)voltage);
            return (Status)result;
        }
        public static Status SetPolarization(Polarization polarization)
        {
            int result = NativeSetPolarization((int)polarization);
            return (Status)result;
        }
        public static Status SetTone(bool enable)
        {
            int result = NativeSetTone(enable);
            return (Status)result;
        }
        public static Status SetBand(Band band)
        {
            int result = NativeSetBand((int)band);
            return (Status)result;
        }
        public static Voltage GetVoltage()
        {
            int result = NativeGetVoltage();
            return (Voltage)result;
        }
        public static bool GetTone()
        {
            return NativeGetTone();
        }
        public static Polarization GetPolarization()
        {
            int result = NativeGetPolarization();
            return (Polarization)result;
        }
        public static Band GetBand()
        {
            int result = NativeGetBand();
            return (Band)result;
        }
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
