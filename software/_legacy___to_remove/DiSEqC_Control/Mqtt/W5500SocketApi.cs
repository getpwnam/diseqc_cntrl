using DiSEqC_Control.Native;

namespace DiSEqC_Control.Mqtt
{
    internal sealed class W5500SocketApi : IW5500SocketApi
    {
        public W5500Socket.Status Open(out int socketHandle)
        {
            return W5500Socket.Open(out socketHandle);
        }

        public W5500Socket.Status Connect(int socketHandle, string host, int port, int timeoutMs)
        {
            return W5500Socket.Connect(socketHandle, host, port, timeoutMs);
        }

        public W5500Socket.Status Send(int socketHandle, byte[] data, int offset, int count, out int sent)
        {
            return W5500Socket.Send(socketHandle, data, offset, count, out sent);
        }

        public W5500Socket.Status Receive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int received)
        {
            return W5500Socket.Receive(socketHandle, buffer, offset, count, timeoutMs, out received);
        }

        public W5500Socket.Status Close(int socketHandle)
        {
            return W5500Socket.Close(socketHandle);
        }

        public bool IsConnected(int socketHandle)
        {
            return W5500Socket.IsConnected(socketHandle);
        }
    }
}