using DiSEqC_Control.Mqtt;

namespace DiSEqC_Control.Tests;

public class MqttCommandRouterTests
{
    [Fact]
    public void TryHandle_GotoAngle_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/goto/angle", "19.2", sink);

        Assert.True(handled);
        Assert.Equal("19.2", sink.GotoAnglePayload);
    }

    [Fact]
    public void TryHandle_GotoSatellite_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/goto/satellite", "astra_19.2e", sink);

        Assert.True(handled);
        Assert.Equal("astra_19.2e", sink.GotoSatellitePayload);
    }

    [Fact]
    public void TryHandle_Halt_InvokesHandler()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/halt", string.Empty, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.HaltCalls);
    }

    [Fact]
    public void TryHandle_StepEast_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/manual/step_east", "4", sink);

        Assert.True(handled);
        Assert.Equal("4", sink.StepEastPayload);
    }

    [Fact]
    public void TryHandle_StepWest_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/manual/step_west", "5", sink);

        Assert.True(handled);
        Assert.Equal("5", sink.StepWestPayload);
    }

    [Fact]
    public void TryHandle_DriveEast_InvokesHandler()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/manual/drive_east", string.Empty, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.DriveEastCalls);
    }

    [Fact]
    public void TryHandle_DriveWest_InvokesHandler()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/manual/drive_west", string.Empty, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.DriveWestCalls);
    }

    [Fact]
    public void TryHandle_LnbVoltage_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/lnb/voltage", "13", sink);

        Assert.True(handled);
        Assert.Equal("13", sink.LnbVoltagePayload);
    }

    [Fact]
    public void TryHandle_LnbPolarization_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/lnb/polarization", "vertical", sink);

        Assert.True(handled);
        Assert.Equal("vertical", sink.LnbPolarizationPayload);
    }

    [Fact]
    public void TryHandle_LnbTone_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/lnb/tone", "on", sink);

        Assert.True(handled);
        Assert.Equal("on", sink.LnbTonePayload);
    }

    [Fact]
    public void TryHandle_LnbBand_RoutesPayload()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/lnb/band", "high", sink);

        Assert.True(handled);
        Assert.Equal("high", sink.LnbBandPayload);
    }

    [Fact]
    public void TryHandle_CalibrateReference_InvokesHandler()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/calibrate/reference", string.Empty, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.CalibrateReferenceCalls);
    }

    [Fact]
    public void TryHandle_UnknownTopic_ReturnsFalse()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/status/state", "idle", sink);

        Assert.False(handled);
    }

    [Fact]
    public void TryHandle_NearMatchTopicWithExtraSuffix_ReturnsFalse()
    {
        var sink = new CommandSink();

        bool handled = Route("diseqc/command/goto/angle/extra", "19.2", sink);

        Assert.False(handled);
        Assert.Null(sink.GotoAnglePayload);
    }

    private static bool Route(string topic, string payload, CommandSink sink)
    {
        return MqttCommandRouter.TryHandle(topic, payload, sink);
    }

    private sealed class CommandSink : IMqttCommandSink
    {
        public string GotoAnglePayload;
        public string GotoSatellitePayload;
        public int HaltCalls;
        public string StepEastPayload;
        public string StepWestPayload;
        public int DriveEastCalls;
        public int DriveWestCalls;
        public string LnbVoltagePayload;
        public string LnbPolarizationPayload;
        public string LnbTonePayload;
        public string LnbBandPayload;
        public int CalibrateReferenceCalls;

        public void HandleGotoAngle(string payload) => GotoAnglePayload = payload;
        public void HandleGotoSatellite(string payload) => GotoSatellitePayload = payload;
        public void HandleHalt() => HaltCalls++;
        public void HandleStepEast(string payload) => StepEastPayload = payload;
        public void HandleStepWest(string payload) => StepWestPayload = payload;
        public void HandleDriveEast() => DriveEastCalls++;
        public void HandleDriveWest() => DriveWestCalls++;
        public void HandleLnbVoltage(string payload) => LnbVoltagePayload = payload;
        public void HandleLnbPolarization(string payload) => LnbPolarizationPayload = payload;
        public void HandleLnbTone(string payload) => LnbTonePayload = payload;
        public void HandleLnbBand(string payload) => LnbBandPayload = payload;
        public void HandleCalibrateReference() => CalibrateReferenceCalls++;
    }
}
