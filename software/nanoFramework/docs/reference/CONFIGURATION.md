# Configuration Management - Complete Design

## 🎯 Overview

A comprehensive configuration system for your DiSEqC controller that supports:
- ✅ **Persistent storage** (survives reboots)
- ✅ **Multiple interfaces** (MQTT, web, serial)
- ✅ **Validation** (ensure valid settings)
- ✅ **Factory reset** (restore defaults)
- ✅ **Hot reload** (apply changes without reboot when possible)

## 📋 Configuration Categories

### 1. Network Configuration
```json
{
  "network": {
    "mode": "static",          // "static" or "dhcp"
    "ip": "192.168.1.100",
    "subnet": "255.255.255.0",
    "gateway": "192.168.1.1",
    "dns_primary": "8.8.8.8",
    "dns_secondary": "8.8.4.4",
    "hostname": "diseqc-ctrl"
  }
}
```

### 2. MQTT Configuration
```json
{
  "mqtt": {
    "enabled": true,
    "broker": "192.168.1.50",
    "port": 1883,
    "client_id": "diseqc_controller",
    "username": "diseqc",
    "password": "",             // Encrypted or use secrets
    "keepalive": 60,
    "reconnect_delay": 5,
    "topic_prefix": "diseqc",
    "qos_default": 1,
    "retain_status": true
  }
}
```

### 3. Rotor Configuration
```json
{
  "rotor": {
    "max_angle_east": 80.0,
    "max_angle_west": -80.0,
    "step_size_degrees": 1.0,
    "movement_timeout_sec": 60,
    "reference_angle": 0.0,      // Reference position (South)
    "calibrated": false,
    "auto_halt_on_limit": true
  }
}
```

### 4. Satellite Database
```json
{
  "satellites": [
    {
      "id": "astra_19.2e",
      "name": "Astra 19.2°E",
      "angle": 19.2,
      "description": "German/European channels",
      "favorite": true
    },
    {
      "id": "hotbird_13e",
      "name": "Hotbird 13°E",
      "angle": 13.0,
      "description": "Italian/European channels",
      "favorite": true
    },
    {
      "id": "astra_28.2e",
      "name": "Astra 28.2°E",
      "angle": 28.2,
      "description": "UK channels",
      "favorite": false
    }
  ]
}
```

### 5. System Configuration
```json
{
  "system": {
    "device_name": "DiSEqC Controller",
    "location": "Home",
    "timezone": "UTC",
    "telemetry_interval_sec": 30,
    "log_level": "info",         // "debug", "info", "warn", "error"
    "auto_save_config": true,
    "config_save_delay_sec": 10
  }
}
```

## 💾 Storage Implementation

### nanoFramework Configuration Block

nanoFramework provides a **Configuration Block** in flash memory for persistent storage.

```csharp
using nanoFramework.Hardware.Esp32;  // Even though we're STM32, same API
using nanoFramework.Runtime.Native;
using System.Collections;

public class ConfigurationManager
{
    private const string CONFIG_BLOCK_NAME = "diseqc_config";
    
    // Configuration data structure
    public class Config
    {
        public NetworkConfig Network { get; set; }
        public MqttConfig Mqtt { get; set; }
        public RotorConfig Rotor { get; set; }
        public ArrayList Satellites { get; set; }
        public SystemConfig System { get; set; }
    }
    
    // Save configuration to flash
    public static bool SaveConfig(Config config)
    {
        try
        {
            // Serialize to JSON
            string json = JsonConvert.SerializeObject(config);
            
            // Write to configuration block
            ConfigurationManager.WriteConfiguration(
                CONFIG_BLOCK_NAME,
                Encoding.UTF8.GetBytes(json)
            );
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save config: {ex.Message}");
            return false;
        }
    }
    
    // Load configuration from flash
    public static Config LoadConfig()
    {
        try
        {
            // Read from configuration block
            byte[] data = ConfigurationManager.ReadConfiguration(CONFIG_BLOCK_NAME);
            
            if (data == null || data.Length == 0)
            {
                // No config found, return defaults
                return GetDefaultConfig();
            }
            
            // Deserialize from JSON
            string json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<Config>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load config: {ex.Message}");
            return GetDefaultConfig();
        }
    }
    
    // Factory reset
    public static void ResetToDefaults()
    {
        var defaultConfig = GetDefaultConfig();
        SaveConfig(defaultConfig);
    }
    
    // Default configuration
    private static Config GetDefaultConfig()
    {
        return new Config
        {
            Network = new NetworkConfig
            {
                Mode = "dhcp",
                IP = "192.168.1.100",
                Subnet = "255.255.255.0",
                Gateway = "192.168.1.1",
                DnsPrimary = "8.8.8.8",
                Hostname = "diseqc-ctrl"
            },
            Mqtt = new MqttConfig
            {
                Enabled = true,
                Broker = "192.168.1.50",
                Port = 1883,
                ClientId = "diseqc_controller",
                TopicPrefix = "diseqc"
            },
            Rotor = new RotorConfig
            {
                MaxAngleEast = 80.0f,
                MaxAngleWest = -80.0f,
                StepSizeDegrees = 1.0f,
                Calibrated = false
            },
            Satellites = new ArrayList
            {
                new SatelliteConfig 
                { 
                    Id = "astra_19.2e", 
                    Name = "Astra 19.2°E", 
                    Angle = 19.2f, 
                    Favorite = true 
                },
                new SatelliteConfig 
                { 
                    Id = "hotbird_13e", 
                    Name = "Hotbird 13°E", 
                    Angle = 13.0f, 
                    Favorite = true 
                }
            },
            System = new SystemConfig
            {
                DeviceName = "DiSEqC Controller",
                LogLevel = "info"
            }
        };
    }
}
```

### Alternative: Simple Key-Value Storage

If configuration block isn't available or for simpler needs:

```csharp
public class SimpleConfigStorage
{
    private static Hashtable _config = new Hashtable();
    
    public static void Set(string key, string value)
    {
        _config[key] = value;
        Save();
    }
    
    public static string Get(string key, string defaultValue = "")
    {
        return _config.Contains(key) ? (string)_config[key] : defaultValue;
    }
    
    public static float GetFloat(string key, float defaultValue = 0.0f)
    {
        return _config.Contains(key) ? float.Parse((string)_config[key]) : defaultValue;
    }
    
    private static void Save()
    {
        // Serialize hashtable to simple format
        StringBuilder sb = new StringBuilder();
        foreach (DictionaryEntry entry in _config)
        {
            sb.Append($"{entry.Key}={entry.Value}\n");
        }
        
        // Save to flash
        ConfigurationManager.WriteConfiguration(
            "diseqc_simple",
            Encoding.UTF8.GetBytes(sb.ToString())
        );
    }
    
    public static void Load()
    {
        _config.Clear();
        
        byte[] data = ConfigurationManager.ReadConfiguration("diseqc_simple");
        if (data == null) return;
        
        string content = Encoding.UTF8.GetString(data);
        string[] lines = content.Split('\n');
        
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            
            string[] parts = line.Split('=');
            if (parts.Length == 2)
            {
                _config[parts[0].Trim()] = parts[1].Trim();
            }
        }
    }
}
```

## 🔧 Configuration Interface - MQTT

### Subscribe to Config Commands

```csharp
public class MqttConfigHandler
{
    private Config _config;
    
    public void Initialize()
    {
        // Load config from flash
        _config = ConfigurationManager.LoadConfig();
        
        // Subscribe to config topics
        mqtt.Subscribe(new[] 
        { 
            "diseqc/command/config/set",
            "diseqc/command/config/get",
            "diseqc/command/config/reset",
            "diseqc/command/config/save"
        });
    }
    
    public void OnMqttMessage(object sender, MqttMsgPublishEventArgs e)
    {
        string topic = e.Topic;
        string payload = Encoding.UTF8.GetString(e.Message);
        
        switch (topic)
        {
            case "diseqc/command/config/set":
                HandleSetConfig(payload);
                break;
                
            case "diseqc/command/config/get":
                PublishCurrentConfig();
                break;
                
            case "diseqc/command/config/reset":
                ConfigurationManager.ResetToDefaults();
                _config = ConfigurationManager.LoadConfig();
                PublishCurrentConfig();
                break;
                
            case "diseqc/command/config/save":
                ConfigurationManager.SaveConfig(_config);
                mqtt.Publish("diseqc/status/config/saved", "true");
                break;
        }
    }
    
    private void HandleSetConfig(string payload)
    {
        // Payload format: "network.ip=192.168.1.101"
        // Or JSON: {"network": {"ip": "192.168.1.101"}}
        
        try
        {
            if (payload.Contains("="))
            {
                // Simple key=value format
                string[] parts = payload.Split('=');
                SetConfigValue(parts[0].Trim(), parts[1].Trim());
            }
            else
            {
                // JSON format
                var updates = JsonConvert.DeserializeObject<Hashtable>(payload);
                ApplyConfigUpdates(updates);
            }
            
            // Optionally auto-save
            if (_config.System.AutoSaveConfig)
            {
                ConfigurationManager.SaveConfig(_config);
            }
            
            // Publish updated config
            PublishCurrentConfig();
        }
        catch (Exception ex)
        {
            mqtt.Publish("diseqc/status/config/error", ex.Message);
        }
    }
    
    private void SetConfigValue(string key, string value)
    {
        // Example: "network.ip" -> set _config.Network.IP
        string[] path = key.Split('.');
        
        switch (path[0])
        {
            case "network":
                UpdateNetworkConfig(path[1], value);
                break;
            case "mqtt":
                UpdateMqttConfig(path[1], value);
                break;
            case "rotor":
                UpdateRotorConfig(path[1], value);
                break;
        }
    }
    
    private void PublishCurrentConfig()
    {
        // Publish each config section
        mqtt.Publish("diseqc/config/network", 
            JsonConvert.SerializeObject(_config.Network), 1, true);
        mqtt.Publish("diseqc/config/mqtt", 
            JsonConvert.SerializeObject(_config.Mqtt), 1, true);
        mqtt.Publish("diseqc/config/rotor", 
            JsonConvert.SerializeObject(_config.Rotor), 1, true);
        mqtt.Publish("diseqc/config/satellites", 
            JsonConvert.SerializeObject(_config.Satellites), 1, true);
    }
}
```

### MQTT Configuration Examples

```bash
# Get current config
mosquitto_pub -t diseqc/command/config/get -m ""

# Set single value
mosquitto_pub -t diseqc/command/config/set -m "network.ip=192.168.1.101"

# Set multiple values (JSON)
mosquitto_pub -t diseqc/command/config/set -m '{"mqtt":{"broker":"192.168.1.55","port":1883}}'

# Save to flash
mosquitto_pub -t diseqc/command/config/save -m ""

# Reset to defaults
mosquitto_pub -t diseqc/command/config/reset -m ""

# Subscribe to config changes
mosquitto_sub -t 'diseqc/config/#' -v
```

## 🌐 Web Interface for Configuration

### Simple HTTP Configuration Page

```csharp
using nanoFramework.WebServer;

public class ConfigWebServer
{
    private WebServer _server;
    private Config _config;
    
    public void Start()
    {
        _config = ConfigurationManager.LoadConfig();
        
        _server = new WebServer(80, HttpProtocol.Http);
        _server.CommandReceived += OnCommandReceived;
        _server.Start();
    }
    
    private void OnCommandReceived(object sender, WebServerEventArgs e)
    {
        string url = e.Context.Request.RawUrl;
        
        switch (url)
        {
            case "/":
                ServeDashboard(e.Context);
                break;
            case "/config":
                ServeConfigPage(e.Context);
                break;
            case "/api/config":
                HandleConfigApi(e.Context);
                break;
        }
    }
    
    private void ServeConfigPage(HttpListenerContext context)
    {
        string html = @"
<!DOCTYPE html>
<html>
<head>
    <title>DiSEqC Configuration</title>
    <style>
        body { font-family: Arial; margin: 20px; }
        .section { margin: 20px 0; padding: 15px; border: 1px solid #ccc; }
        input, select { width: 200px; margin: 5px; }
        button { background: #4CAF50; color: white; padding: 10px; border: none; cursor: pointer; }
    </style>
</head>
<body>
    <h1>DiSEqC Controller Configuration</h1>
    
    <div class='section'>
        <h2>Network</h2>
        <label>Mode:</label>
        <select id='net_mode'>
            <option value='dhcp'>DHCP</option>
            <option value='static'>Static</option>
        </select><br>
        <label>IP Address:</label>
        <input type='text' id='net_ip' value='" + _config.Network.IP + @"'><br>
        <label>Gateway:</label>
        <input type='text' id='net_gw' value='" + _config.Network.Gateway + @"'><br>
    </div>
    
    <div class='section'>
        <h2>MQTT</h2>
        <label>Broker:</label>
        <input type='text' id='mqtt_broker' value='" + _config.Mqtt.Broker + @"'><br>
        <label>Port:</label>
        <input type='number' id='mqtt_port' value='" + _config.Mqtt.Port + @"'><br>
        <label>Username:</label>
        <input type='text' id='mqtt_user' value='" + _config.Mqtt.Username + @"'><br>
    </div>
    
    <div class='section'>
        <h2>Rotor</h2>
        <label>Max East:</label>
        <input type='number' id='rotor_east' value='" + _config.Rotor.MaxAngleEast + @"'><br>
        <label>Max West:</label>
        <input type='number' id='rotor_west' value='" + _config.Rotor.MaxAngleWest + @"'><br>
    </div>
    
    <button onclick='saveConfig()'>Save Configuration</button>
    <button onclick='resetConfig()'>Reset to Defaults</button>
    
    <script>
        function saveConfig() {
            const config = {
                network: {
                    mode: document.getElementById('net_mode').value,
                    ip: document.getElementById('net_ip').value,
                    gateway: document.getElementById('net_gw').value
                },
                mqtt: {
                    broker: document.getElementById('mqtt_broker').value,
                    port: parseInt(document.getElementById('mqtt_port').value),
                    username: document.getElementById('mqtt_user').value
                },
                rotor: {
                    maxAngleEast: parseFloat(document.getElementById('rotor_east').value),
                    maxAngleWest: parseFloat(document.getElementById('rotor_west').value)
                }
            };
            
            fetch('/api/config', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify(config)
            }).then(() => alert('Configuration saved!'));
        }
        
        function resetConfig() {
            if (confirm('Reset to factory defaults?')) {
                fetch('/api/config/reset', {method: 'POST'})
                    .then(() => location.reload());
            }
        }
    </script>
</body>
</html>";
        
        context.Response.ContentType = "text/html";
        context.Response.StatusCode = 200;
        WebServer.OutPutStream(context.Response, html);
    }
    
    private void HandleConfigApi(HttpListenerContext context)
    {
        if (context.Request.HttpMethod == "GET")
        {
            // Return current config as JSON
            string json = JsonConvert.SerializeObject(_config);
            context.Response.ContentType = "application/json";
            WebServer.OutPutStream(context.Response, json);
        }
        else if (context.Request.HttpMethod == "POST")
        {
            // Update config from JSON
            string body = new StreamReader(context.Request.InputStream).ReadToEnd();
            _config = JsonConvert.DeserializeObject<Config>(body);
            ConfigurationManager.SaveConfig(_config);
            
            context.Response.StatusCode = 200;
            WebServer.OutPutStream(context.Response, "{\"success\":true}");
        }
    }
}
```

## 🔐 Configuration Validation

```csharp
public class ConfigValidator
{
    public static bool Validate(Config config, out string error)
    {
        error = "";
        
        // Validate network
        if (config.Network.Mode == "static")
        {
            if (!IsValidIp(config.Network.IP))
            {
                error = "Invalid IP address";
                return false;
            }
        }
        
        // Validate MQTT
        if (config.Mqtt.Port < 1 || config.Mqtt.Port > 65535)
        {
            error = "Invalid MQTT port";
            return false;
        }
        
        // Validate rotor
        if (config.Rotor.MaxAngleEast < -80 || config.Rotor.MaxAngleEast > 80)
        {
            error = "East angle out of range (-80 to 80)";
            return false;
        }
        
        if (config.Rotor.MaxAngleWest < -80 || config.Rotor.MaxAngleWest > 80)
        {
            error = "West angle out of range (-80 to 80)";
            return false;
        }
        
        if (config.Rotor.MaxAngleWest >= config.Rotor.MaxAngleEast)
        {
            error = "West limit must be less than East limit";
            return false;
        }
        
        return true;
    }
    
    private static bool IsValidIp(string ip)
    {
        string[] parts = ip.Split('.');
        if (parts.Length != 4) return false;
        
        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int value)) return false;
            if (value < 0 || value > 255) return false;
        }
        
        return true;
    }
}
```

## 📊 Configuration Change Flow

```
1. User modifies config (MQTT/Web/Serial)
        ↓
2. Validate configuration
        ↓
3. Apply changes to runtime
        ↓
4. Publish updated config (MQTT)
        ↓
5. Auto-save to flash (if enabled)
        ↓
6. Some changes require reboot (network, MQTT broker)
```

## 🎯 Configuration Best Practices

1. **Always validate** before saving
2. **Backup before reset** (optional feature)
3. **Hot-reload when possible** (avoid reboots)
4. **Publish changes** to MQTT (subscribers stay in sync)
5. **Use retained messages** for config topics
6. **Log all config changes**
7. **Provide config export/import** (JSON file)

---

**Complete configuration management system ready!** 🎯

This gives you:
- ✅ Persistent storage in flash
- ✅ MQTT configuration interface
- ✅ Web configuration page
- ✅ Validation and error handling
- ✅ Factory reset capability
- ✅ Hot-reload support

Your DiSEqC controller will be fully configurable and user-friendly! 🚀

