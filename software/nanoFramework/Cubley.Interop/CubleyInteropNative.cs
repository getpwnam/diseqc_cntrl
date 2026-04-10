using System.Runtime.CompilerServices;

namespace Cubley.Interop
{
    public static class BringupStatus
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSet(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGet();
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
    }
}
