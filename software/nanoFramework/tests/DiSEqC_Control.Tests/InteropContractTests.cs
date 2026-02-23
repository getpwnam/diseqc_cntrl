using DiSEqC_Control.Native;
using System.Reflection;

namespace DiSEqC_Control.Tests;

public class W5500SocketContractTests
{
    [Fact]
    public void StatusEnumValues_AreStable()
    {
        Assert.Equal(0, (int)W5500Socket.Status.Ok);
        Assert.Equal(1, (int)W5500Socket.Status.InvalidParam);
        Assert.Equal(2, (int)W5500Socket.Status.NotInitialized);
        Assert.Equal(3, (int)W5500Socket.Status.Busy);
        Assert.Equal(4, (int)W5500Socket.Status.Timeout);
        Assert.Equal(5, (int)W5500Socket.Status.NotSupported);
        Assert.Equal(6, (int)W5500Socket.Status.IoError);
    }

    [Theory]
    [InlineData("NativeOpen")]
    [InlineData("NativeConfigureNetwork")]
    [InlineData("NativeConnect")]
    [InlineData("NativeSend")]
    [InlineData("NativeReceive")]
    [InlineData("NativeClose")]
    [InlineData("NativeIsConnected")]
    public void InternalNativeMethods_HaveExternShape(string methodName)
    {
        var method = typeof(W5500Socket).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsStatic);
        Assert.True(method.IsPrivate);
        Assert.Null(method.GetMethodBody());
    }

    [Fact]
    public void PublicMethodSignatures_AreStable()
    {
        var connect = typeof(W5500Socket).GetMethod(nameof(W5500Socket.Connect), BindingFlags.Public | BindingFlags.Static);
        var configureNetwork = typeof(W5500Socket).GetMethod(nameof(W5500Socket.ConfigureNetwork), BindingFlags.Public | BindingFlags.Static);
        var send = typeof(W5500Socket).GetMethod(nameof(W5500Socket.Send), BindingFlags.Public | BindingFlags.Static);
        var receive = typeof(W5500Socket).GetMethod(nameof(W5500Socket.Receive), BindingFlags.Public | BindingFlags.Static);
        var close = typeof(W5500Socket).GetMethod(nameof(W5500Socket.Close), BindingFlags.Public | BindingFlags.Static);
        var isConnected = typeof(W5500Socket).GetMethod(nameof(W5500Socket.IsConnected), BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(connect);
        Assert.NotNull(configureNetwork);
        Assert.NotNull(send);
        Assert.NotNull(receive);
        Assert.NotNull(close);
        Assert.NotNull(isConnected);

        Assert.Equal(typeof(W5500Socket.Status), connect.ReturnType);
        Assert.Equal(typeof(W5500Socket.Status), configureNetwork.ReturnType);
        Assert.Equal(typeof(W5500Socket.Status), send.ReturnType);
        Assert.Equal(typeof(W5500Socket.Status), receive.ReturnType);
        Assert.Equal(typeof(W5500Socket.Status), close.ReturnType);
        Assert.Equal(typeof(bool), isConnected.ReturnType);

        var configureParams = configureNetwork.GetParameters();
        Assert.Equal(4, configureParams.Length);
        Assert.All(configureParams, p => Assert.Equal(typeof(string), p.ParameterType));
    }
}

public class DiSEqCInteropContractTests
{
    [Fact]
    public void StatusEnumValues_AreStable()
    {
        Assert.Equal(0, (int)DiSEqC.Status.Ok);
        Assert.Equal(1, (int)DiSEqC.Status.Busy);
        Assert.Equal(2, (int)DiSEqC.Status.InvalidParam);
        Assert.Equal(3, (int)DiSEqC.Status.Timeout);
    }

    [Theory]
    [InlineData("NativeGotoAngle")]
    [InlineData("NativeTransmit")]
    [InlineData("NativeHalt")]
    [InlineData("NativeDriveEast")]
    [InlineData("NativeDriveWest")]
    [InlineData("NativeStepEast")]
    [InlineData("NativeStepWest")]
    [InlineData("NativeIsBusy")]
    [InlineData("NativeGetCurrentAngle")]
    public void InternalNativeMethods_HaveExternShape(string methodName)
    {
        var method = typeof(DiSEqC).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsPrivate);
        Assert.True(method.IsStatic);
        Assert.Null(method.GetMethodBody());
    }

    [Fact]
    public void PublicTransmitSignature_IsStable()
    {
        var transmit = typeof(DiSEqC).GetMethod(nameof(DiSEqC.Transmit), BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(transmit);
        Assert.Equal(typeof(DiSEqC.Status), transmit.ReturnType);
        Assert.Single(transmit.GetParameters());
        Assert.Equal(typeof(byte[]), transmit.GetParameters()[0].ParameterType);
    }
}

public class LnbInteropContractTests
{
    [Fact]
    public void StatusEnumValues_AreStable()
    {
        Assert.Equal(0, (int)LNB.Status.Ok);
        Assert.Equal(1, (int)LNB.Status.InvalidParam);
        Assert.Equal(2, (int)LNB.Status.NotInitialized);
    }

    [Fact]
    public void DomainEnumValues_AreStable()
    {
        Assert.Equal(0, (int)LNB.Voltage.V13);
        Assert.Equal(1, (int)LNB.Voltage.V18);
        Assert.Equal(0, (int)LNB.Polarization.Vertical);
        Assert.Equal(1, (int)LNB.Polarization.Horizontal);
        Assert.Equal(0, (int)LNB.Band.Low);
        Assert.Equal(1, (int)LNB.Band.High);
    }

    [Theory]
    [InlineData("NativeSetVoltage")]
    [InlineData("NativeSetPolarization")]
    [InlineData("NativeSetTone")]
    [InlineData("NativeSetBand")]
    [InlineData("NativeGetVoltage")]
    [InlineData("NativeGetTone")]
    [InlineData("NativeGetPolarization")]
    [InlineData("NativeGetBand")]
    public void InternalNativeMethods_HaveExternShape(string methodName)
    {
        var method = typeof(LNB).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsPrivate);
        Assert.True(method.IsStatic);
        Assert.Null(method.GetMethodBody());
    }
}