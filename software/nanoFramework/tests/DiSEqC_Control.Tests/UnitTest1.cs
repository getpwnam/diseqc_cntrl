namespace DiSEqC_Control.Tests;

public class RuntimeConfigurationTests
{
    [Fact]
    public void Defaults_AreStable()
    {
        var config = RuntimeConfiguration.CreateDefaults();

        Assert.True(config.UseDhcp);
        Assert.Equal("192.168.1.100", config.StaticIp);
        Assert.Equal("255.255.255.0", config.StaticSubnetMask);
        Assert.Equal("192.168.1.1", config.StaticGateway);
        Assert.Equal("02:08:DC:00:00:01", config.NetworkMac);
        Assert.Equal("192.168.1.50", config.MqttBroker);
        Assert.Equal(1883, config.MqttPort);
        Assert.Equal("diseqc_controller", config.MqttClientId);
        Assert.Equal("diseqc", config.MqttTopicPrefix);
        Assert.Equal("diseqc-ctrl", config.DeviceName);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void TrySetValue_ParsesNetworkUseDhcpVariants(string value, bool expected)
    {
        var config = RuntimeConfiguration.CreateDefaults();

        var ok = config.TrySetValue("network.use_dhcp", value, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expected, config.UseDhcp);
    }

    [Fact]
    public void TrySetValue_RejectsInvalidIp()
    {
        var config = RuntimeConfiguration.CreateDefaults();

        var ok = config.TrySetValue("network.static_ip", "300.1.2.3", out var error);

        Assert.False(ok);
        Assert.Equal("network.static_ip is not a valid IPv4 address", error);
    }

    [Theory]
    [InlineData("02:08:DC:00:00:01")]
    [InlineData("aa:bb:cc:dd:ee:ff")]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    public void TrySetValue_AcceptsValidMac(string value)
    {
        var config = RuntimeConfiguration.CreateDefaults();

        var ok = config.TrySetValue("network.mac", value, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(value, config.NetworkMac);
    }

    [Theory]
    [InlineData("02:08:DC:00:00")]
    [InlineData("02-08-DC-00-00-01")]
    [InlineData("GG:08:DC:00:00:01")]
    [InlineData("0208DC000001")]
    [InlineData("")]
    public void TrySetValue_RejectsInvalidMac(string value)
    {
        var config = RuntimeConfiguration.CreateDefaults();

        var ok = config.TrySetValue("network.mac", value, out var error);

        Assert.False(ok);
        Assert.Equal("network.mac is not a valid MAC address (format: XX:XX:XX:XX:XX:XX)", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    public void TrySetValue_RejectsInvalidPort(string value)
    {
        var config = RuntimeConfiguration.CreateDefaults();

        var ok = config.TrySetValue("mqtt.port", value, out var error);

        Assert.False(ok);
        Assert.Equal("mqtt.port must be 1..65535", error);
    }

    [Fact]
    public void TrySetValue_RejectsUnknownKey()
    {
        var config = RuntimeConfiguration.CreateDefaults();

        var ok = config.TrySetValue("system.unknown", "x", out var error);

        Assert.False(ok);
        Assert.Equal("Unknown config key: system.unknown", error);
    }

    [Fact]
    public void KeyValueRoundTrip_PreservesConfiguredValues()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        config.TrySetValue("network.use_dhcp", "false", out _);
        config.TrySetValue("network.static_ip", "10.1.2.3", out _);
        config.TrySetValue("network.mac", "12:34:56:78:9A:BC", out _);
        config.TrySetValue("mqtt.port", "1884", out _);
        config.TrySetValue("mqtt.client_id", "unit-test-client", out _);
        config.TrySetValue("system.location", "lab", out _);

        var content = config.ToKeyValueLines();
        var parsed = RuntimeConfiguration.TryParseKeyValueLines(content, out var rehydrated, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.False(rehydrated.UseDhcp);
        Assert.Equal("10.1.2.3", rehydrated.StaticIp);
        Assert.Equal("12:34:56:78:9A:BC", rehydrated.NetworkMac);
        Assert.Equal(1884, rehydrated.MqttPort);
        Assert.Equal("unit-test-client", rehydrated.MqttClientId);
        Assert.Equal("lab", rehydrated.DeviceLocation);
    }

    [Fact]
    public void TryParseKeyValueLines_RejectsInvalidLine()
    {
        var parsed = RuntimeConfiguration.TryParseKeyValueLines("network.use_dhcp=true\ninvalidline", out _, out var error);

        Assert.False(parsed);
        Assert.Equal("Invalid persisted config line: invalidline", error);
    }
}

public class ParityHelperTests
{
    [Theory]
    [InlineData(0, ParityHelper.Parity.EVEN)]
    [InlineData(1, ParityHelper.Parity.ODD)]
    [InlineData(2, ParityHelper.Parity.ODD)]
    [InlineData(3, ParityHelper.Parity.EVEN)]
    [InlineData(0xFF, ParityHelper.Parity.EVEN)]
    [InlineData(0x7F, ParityHelper.Parity.ODD)]
    public void ParityEvenBit_ReturnsExpectedParity(int value, ParityHelper.Parity expected)
    {
        var actual = ParityHelper.ParityEvenBit(value);

        Assert.Equal(expected, actual);
    }
}