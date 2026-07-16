using DiSEqC_Control.Mqtt;
using System.Collections.Generic;

namespace DiSEqC_Control.Tests;

public class MqttConfigCommandProcessorTests
{
    [Fact]
    public void TryHandle_ConfigGet_PublishesEffectiveConfig()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/get", string.Empty, config, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.EffectiveConfigCalls);
        Assert.Empty(sink.Errors);
        Assert.Empty(sink.Statuses);
    }

    [Fact]
    public void TryHandle_ConfigSet_UpdatesConfigurationAndPublishesStatus()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/set", "mqtt.port=1884", config, sink);

        Assert.True(handled);
        Assert.Equal(1884, config.MqttPort);
        Assert.Equal(1, sink.EffectiveConfigCalls);
        Assert.Single(sink.Statuses);
        Assert.Equal("config/updated", sink.Statuses[0].Subtopic);
        Assert.Equal("mqtt.port", sink.Statuses[0].Value);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public void TryHandle_ConfigSet_WithInvalidPayload_PublishesError()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/set", "invalid_payload", config, sink);

        Assert.True(handled);
        Assert.Single(sink.Errors);
        Assert.Equal("Config set payload must be key=value", sink.Errors[0]);
    }

    [Fact]
    public void TryHandle_ConfigSet_WithInvalidValue_PublishesValidationError()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/set", "mqtt.port=70000", config, sink);

        Assert.True(handled);
        Assert.Single(sink.Errors);
        Assert.Equal("mqtt.port must be 1..65535", sink.Errors[0]);
    }

    [Fact]
    public void TryHandle_UnknownTopic_ReturnsFalse()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/manual/step_east", "5", config, sink);

        Assert.False(handled);
        Assert.Empty(sink.Statuses);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public void TryHandle_NearMatchTopicWithExtraSuffix_ReturnsFalse()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/get/extra", string.Empty, config, sink);

        Assert.False(handled);
        Assert.Empty(sink.Statuses);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public void TryHandle_ConfigSave_InvokesSaveHandler()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/save", string.Empty, config, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.SaveCalls);
    }

    [Fact]
    public void TryHandle_ConfigReset_InvokesResetHandler()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/reset", string.Empty, config, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.ResetCalls);
    }

    [Fact]
    public void TryHandle_ConfigReload_InvokesReloadHandler()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/reload", string.Empty, config, sink);

        Assert.True(handled);
        Assert.Equal(1, sink.ReloadCalls);
    }

    [Fact]
    public void TryHandle_ConfigFramClear_PassesPayloadToHandler()
    {
        var config = RuntimeConfiguration.CreateDefaults();
        var sink = new ConfigSink();

        bool handled = MqttConfigCommandProcessor.TryHandle("diseqc/command/config/fram_clear", "ERASE", config, sink);

        Assert.True(handled);
        Assert.Equal("ERASE", sink.FramClearPayload);
    }

    private sealed class ConfigSink : IMqttConfigSink
    {
        public readonly List<StatusEntry> Statuses = new();
        public readonly List<string> Errors = new();
        public int EffectiveConfigCalls;
        public int SaveCalls;
        public int ResetCalls;
        public int ReloadCalls;
        public string FramClearPayload;

        public void PublishStatus(string subtopic, string value) => Statuses.Add(new StatusEntry(subtopic, value));
        public void PublishError(string message) => Errors.Add(message);
        public void PublishEffectiveConfig() => EffectiveConfigCalls++;
        public void HandleConfigSave() => SaveCalls++;
        public void HandleConfigReset() => ResetCalls++;
        public void HandleConfigReload() => ReloadCalls++;
        public void HandleConfigFramClear(string token) => FramClearPayload = token;
    }

    private sealed class StatusEntry
    {
        public string Subtopic { get; }
        public string Value { get; }

        public StatusEntry(string subtopic, string value)
        {
            Subtopic = subtopic;
            Value = value;
        }
    }
}
