using System;
using System.Runtime.CompilerServices;

namespace DiSEqC_Control.Native
{
    /// <summary>
    /// Native W5500 socket interop contract.
    ///
    /// This is a minimal TCP-like surface intended to be compatible with the
    /// transport expectations of managed MQTT clients.
    /// </summary>
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

        public static Status Open(out int socketHandle)
        {
            int result = NativeOpen(out socketHandle);
            return (Status)result;
        }

        public static Status ConfigureNetwork(string localIp, string subnetMask, string gateway, string macAddress)
        {
            if (string.IsNullOrEmpty(localIp) || string.IsNullOrEmpty(subnetMask) || string.IsNullOrEmpty(gateway) || string.IsNullOrEmpty(macAddress))
            {
                return Status.InvalidParam;
            }

            int result = NativeConfigureNetwork(localIp, subnetMask, gateway, macAddress);
            return (Status)result;
        }

        public static Status Connect(int socketHandle, string host, int port, int timeoutMs)
        {
            if (string.IsNullOrEmpty(host) || port < 1 || port > 65535 || timeoutMs < 0)
            {
                return Status.InvalidParam;
            }

            int result = NativeConnect(socketHandle, host, port, timeoutMs);
            return (Status)result;
        }

        public static Status Send(int socketHandle, byte[] data, int offset, int count, out int sent)
        {
            sent = 0;

            if (data == null || offset < 0 || count < 0 || offset + count > data.Length)
            {
                return Status.InvalidParam;
            }

            int result = NativeSend(socketHandle, data, offset, count, out sent);
            return (Status)result;
        }

        public static Status Receive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int received)
        {
            received = 0;

            if (buffer == null || offset < 0 || count < 0 || offset + count > buffer.Length || timeoutMs < 0)
            {
                return Status.InvalidParam;
            }

            int result = NativeReceive(socketHandle, buffer, offset, count, timeoutMs, out received);
            return (Status)result;
        }

        public static Status Close(int socketHandle)
        {
            int result = NativeClose(socketHandle);
            return (Status)result;
        }

        public static bool IsConnected(int socketHandle)
        {
            return NativeIsConnected(socketHandle);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeOpen(out int socketHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeConfigureNetwork(string localIp, string subnetMask, string gateway, string macAddress);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeConnect(int socketHandle, string host, int port, int timeoutMs);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeSend(int socketHandle, byte[] data, int offset, int count, out int sent);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeReceive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int received);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeClose(int socketHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern bool NativeIsConnected(int socketHandle);
    }
}