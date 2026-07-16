using DiSEqC_Control.Native;

namespace DiSEqC_Control.Mqtt
{
    internal interface IW5500SocketApi
    {
        W5500Socket.Status Open(out int socketHandle);

        W5500Socket.Status Connect(int socketHandle, string host, int port, int timeoutMs);

        W5500Socket.Status Send(int socketHandle, byte[] data, int offset, int count, out int sent);

        W5500Socket.Status Receive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int received);

        W5500Socket.Status Close(int socketHandle);

        bool IsConnected(int socketHandle);
    }
}