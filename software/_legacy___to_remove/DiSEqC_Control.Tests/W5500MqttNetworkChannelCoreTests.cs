using DiSEqC_Control.Mqtt;
using DiSEqC_Control.Native;

namespace DiSEqC_Control.Tests;

public class W5500MqttNetworkChannelCoreTests
{
    [Fact]
    public void Connect_OpensAndConnectsSocket()
    {
        var api = new FakeW5500SocketApi();
        var core = new W5500MqttNetworkChannelCore("broker.local", 1883, 1500, 2500, api);

        core.Connect();

        Assert.Equal(1, api.OpenCallCount);
        Assert.Equal(1, api.ConnectCallCount);
        Assert.True(core.DataAvailable);
    }

    [Fact]
    public void Send_UsesLoopUntilAllBytesSent()
    {
        var api = new FakeW5500SocketApi
        {
            SendChunkSize = 2
        };
        var core = new W5500MqttNetworkChannelCore("broker.local", 1883, 1500, 2500, api);
        core.Connect();

        int sent = core.Send(new byte[] { 1, 2, 3, 4, 5 });

        Assert.Equal(5, sent);
        Assert.True(api.SendCallCount >= 3);
    }

    [Fact]
    public void Receive_WhenTimeout_ReturnsZero()
    {
        var api = new FakeW5500SocketApi
        {
            ReceiveStatus = W5500Socket.Status.Timeout
        };
        var core = new W5500MqttNetworkChannelCore("broker.local", 1883, 1500, 2500, api);
        core.Connect();

        int received = core.Receive(new byte[8], 300);

        Assert.Equal(0, received);
    }

    [Fact]
    public void Receive_WhenIoError_ThrowsMappedException()
    {
        var api = new FakeW5500SocketApi
        {
            ReceiveStatus = W5500Socket.Status.IoError
        };
        var core = new W5500MqttNetworkChannelCore("broker.local", 1883, 1500, 2500, api);
        core.Connect();

        var ex = Assert.Throws<InvalidOperationException>(() => core.Receive(new byte[4], 100));

        Assert.Contains("IoError", ex.Message);
    }

    [Fact]
    public void Close_CallsCloseAndMarksDisconnected()
    {
        var api = new FakeW5500SocketApi();
        var core = new W5500MqttNetworkChannelCore("broker.local", 1883, 1500, 2500, api);
        core.Connect();

        core.Close();

        Assert.Equal(1, api.CloseCallCount);
        Assert.False(core.DataAvailable);
    }

    [Fact]
    public void Connect_WithInvalidEndpoint_Throws()
    {
        var api = new FakeW5500SocketApi();
        var core = new W5500MqttNetworkChannelCore(string.Empty, 0, 1500, 2500, api);

        Assert.Throws<InvalidOperationException>(() => core.Connect());
        Assert.Equal(0, api.OpenCallCount);
    }

    private sealed class FakeW5500SocketApi : IW5500SocketApi
    {
        private int _nextSocketHandle = 42;
        private bool _connected;

        public int OpenCallCount { get; private set; }
        public int ConnectCallCount { get; private set; }
        public int SendCallCount { get; private set; }
        public int CloseCallCount { get; private set; }

        public int SendChunkSize { get; set; } = int.MaxValue;
        public W5500Socket.Status ReceiveStatus { get; set; } = W5500Socket.Status.Ok;

        public W5500Socket.Status Open(out int socketHandle)
        {
            OpenCallCount++;
            socketHandle = _nextSocketHandle++;
            return W5500Socket.Status.Ok;
        }

        public W5500Socket.Status Connect(int socketHandle, string host, int port, int timeoutMs)
        {
            ConnectCallCount++;
            _connected = true;
            return W5500Socket.Status.Ok;
        }

        public W5500Socket.Status Send(int socketHandle, byte[] data, int offset, int count, out int sent)
        {
            SendCallCount++;
            sent = count > SendChunkSize ? SendChunkSize : count;
            return W5500Socket.Status.Ok;
        }

        public W5500Socket.Status Receive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int received)
        {
            if (ReceiveStatus != W5500Socket.Status.Ok)
            {
                received = 0;
                return ReceiveStatus;
            }

            received = count > 0 ? 1 : 0;
            if (received > 0)
            {
                buffer[offset] = 0xAA;
            }

            return W5500Socket.Status.Ok;
        }

        public W5500Socket.Status Close(int socketHandle)
        {
            CloseCallCount++;
            _connected = false;
            return W5500Socket.Status.Ok;
        }

        public bool IsConnected(int socketHandle)
        {
            return _connected;
        }
    }
}
