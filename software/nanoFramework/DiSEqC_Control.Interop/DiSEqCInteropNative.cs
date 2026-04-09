using System.Runtime.CompilerServices;

namespace diseqc_interop
{
    // Keep declarations in native method-table order to align with lookup indices.
    public static class DiseqC
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeGotoAngle(float angle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeTransmit(byte[] data);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeHalt();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeDriveEast();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeDriveWest();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeStepEast(byte steps);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int NativeStepWest(byte steps);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool NativeIsBusy();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern float NativeGetCurrentAngle();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSetBringupStatus(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetBringupStatus();
    }

    public static class W5500Socket
    {
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
    }
}
