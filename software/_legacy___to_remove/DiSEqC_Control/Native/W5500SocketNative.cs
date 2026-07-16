using System;
using NativeW5500 = Cubley.Interop.W5500Socket;

namespace DiSEqC_Control.Native
{
    /// <summary>
    /// Thin managed wrapper around <see cref="Cubley.Interop.W5500Socket"/>.
    ///
    /// The native InternalCall bindings live in the Cubley.Interop assembly; this
    /// shell adds parameter validation and a managed Status enum that mirrors the
    /// native one. Belt-and-suspenders: keeps existing DiSEqC_Control call sites
    /// stable while routing every call through the single canonical native table.
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
            int result = NativeW5500.NativeOpen(out socketHandle);
            // NativeOpen returns kSingleSocketHandle=1 on success, but BYREF interop
            // can occasionally drop the out-param write. Mirror the W5500Bringup workaround.
            if ((Status)result == Status.Ok && socketHandle != 1)
            {
                socketHandle = 1;
            }
            return (Status)result;
        }

        public static Status ConfigureNetwork(string localIp, string subnetMask, string gateway, string macAddress)
        {
            if (string.IsNullOrEmpty(localIp) || string.IsNullOrEmpty(subnetMask) || string.IsNullOrEmpty(gateway) || string.IsNullOrEmpty(macAddress))
            {
                return Status.InvalidParam;
            }

            return (Status)NativeW5500.NativeConfigureNetwork(localIp, subnetMask, gateway, macAddress);
        }

        public static Status Connect(int socketHandle, string host, int port, int timeoutMs)
        {
            if (string.IsNullOrEmpty(host) || port < 1 || port > 65535 || timeoutMs < 0)
            {
                return Status.InvalidParam;
            }

            return (Status)NativeW5500.NativeConnect(socketHandle, host, port, timeoutMs);
        }

        public static Status Send(int socketHandle, byte[] data, int offset, int count, out int sent)
        {
            sent = 0;

            if (data == null || offset < 0 || count < 0 || offset + count > data.Length)
            {
                return Status.InvalidParam;
            }

            return (Status)NativeW5500.NativeSend(socketHandle, data, offset, count, out sent);
        }

        public static Status Receive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int received)
        {
            received = 0;

            if (buffer == null || offset < 0 || count < 0 || offset + count > buffer.Length || timeoutMs < 0)
            {
                return Status.InvalidParam;
            }

            return (Status)NativeW5500.NativeReceive(socketHandle, buffer, offset, count, timeoutMs, out received);
        }

        public static Status Close(int socketHandle)
        {
            return (Status)NativeW5500.NativeClose(socketHandle);
        }

        public static bool IsConnected(int socketHandle)
        {
            return NativeW5500.NativeIsConnected(socketHandle);
        }

        public static uint GetVersionPhyStatus()
        {
            return NativeW5500.NativeGetVersionPhyStatus();
        }
    }
}