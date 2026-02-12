# MQTT Topic Structure - Complete Design

## 🎯 Design Philosophy

**Principles:**
1. **Hierarchical** - Clear parent/child relationships
2. **Standard patterns** - Follow MQTT best practices
3. **Self-documenting** - Topics explain themselves
4. **Extensible** - Easy to add new features
5. **Home Assistant compatible** - Works with HA MQTT discovery

## 📂 Topic Hierarchy

```
diseqc/
├── command/              # Commands TO the device (subscribe)
│   ├── goto              # Position commands
│   ├── manual/           # Manual control
│   └── config/           # Configuration changes
├── status/               # Status FROM the device (publish)
│   ├── state             # Current operational state
│   ├── position          # Current position
│   └── signal            # Signal quality (if available)
├── config/               # Configuration (publish, retained)
│   ├── satellites        # Satellite database
│   └── settings          # Device settings
└── availability          # Online/offline status (LWT)
```

## 📋 Complete Topic Reference

### 1. Command Topics (Subscribe)

#### Position Control
```
diseqc/command/goto/angle
  Payload: "45.5"
  Description: Go to specific angle (-80 to +80)
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/goto/angle -m "45.5"

diseqc/command/goto/satellite
  Payload: "astra_19.2e"
  Description: Go to named satellite
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/goto/satellite -m "astra_19.2e"

diseqc/command/halt
  Payload: "" (any)
  Description: Stop rotor movement immediately
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/halt -m ""
```

#### Manual Control
```
diseqc/command/manual/step_east
  Payload: "1" to "128"
  Description: Step East N steps
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/manual/step_east -m "5"

diseqc/command/manual/step_west
  Payload: "1" to "128"
  Description: Step West N steps
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/manual/step_west -m "1"

diseqc/command/manual/drive_east
  Payload: "" (any)
  Description: Continuous East (send halt to stop)
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/manual/drive_east -m ""

diseqc/command/manual/drive_west
  Payload: "" (any)
  Description: Continuous West (send halt to stop)
  QoS: 1
  Example: mosquitto_pub -t diseqc/command/manual/drive_west -m ""
```

#### Configuration Commands
```
diseqc/command/config/save
  Payload: "" (any)
  Description: Save current config to flash
  QoS: 1

diseqc/command/config/reset
  Payload: "" (any)
  Description: Reset to factory defaults
  QoS: 1

diseqc/command/config/reload
  Payload: "" (any)
  Description: Reload config from flash
  QoS: 1
```

#### Calibration
```
diseqc/command/calibrate/reference
  Payload: "" (any)
  Description: Set current position as reference (0°)
  QoS: 1

diseqc/command/calibrate/east_limit
  Payload: "80" (degrees)
  Description: Set East limit
  QoS: 1

diseqc/command/calibrate/west_limit
  Payload: "-80" (degrees)
  Description: Set West limit
  QoS: 1
```

### 2. Status Topics (Publish)

#### Device State
```
diseqc/status/state
  Payload: "idle" | "moving" | "calibrating" | "error"
  Description: Current operational state
  QoS: 1
  Retained: Yes
  Example: "idle"

diseqc/status/position/angle
  Payload: "45.5" (degrees)
  Description: Current rotor angle
  QoS: 0
  Retained: Yes
  Example: "45.5"

diseqc/status/position/satellite
  Payload: "astra_19.2e" or "unknown"
  Description: Current satellite (if at known position)
  QoS: 0
  Retained: Yes
  Example: "astra_19.2e"

diseqc/status/busy
  Payload: "true" | "false"
  Description: Is rotor busy (moving or transmitting)
  QoS: 0
  Retained: Yes
  Example: "false"
```

#### Signal Quality (Optional)
```
diseqc/status/signal/quality
  Payload: "0" to "100" (percent)
  Description: Signal quality if tuner connected
  QoS: 0
  Retained: No

diseqc/status/signal/strength
  Payload: "0" to "100" (percent)
  Description: Signal strength if tuner connected
  QoS: 0
  Retained: No
```

#### Health & Diagnostics
```
diseqc/status/health/uptime
  Payload: "3600" (seconds)
  Description: Device uptime
  QoS: 0
  Retained: No

diseqc/status/health/temperature
  Payload: "35.5" (celsius)
  Description: MCU temperature
  QoS: 0
  Retained: No

diseqc/status/health/last_error
  Payload: "DiSEqC timeout" or ""
  Description: Last error message
  QoS: 1
  Retained: Yes
```

### 3. Configuration Topics (Publish, Retained)

#### Satellite Database
```
diseqc/config/satellites
  Payload: JSON array
  QoS: 1
  Retained: Yes
  Example:
  [
    {"name": "astra_19.2e", "angle": 19.2, "description": "Astra 19.2°E"},
    {"name": "hotbird_13e", "angle": 13.0, "description": "Hotbird 13°E"},
    {"name": "astra_28.2e", "angle": 28.2, "description": "Astra 28.2°E"}
  ]
```

#### Device Settings
```
diseqc/config/settings/network
  Payload: JSON object
  QoS: 1
  Retained: Yes
  Example:
  {
    "ip": "192.168.1.100",
    "gateway": "192.168.1.1",
    "dns": "8.8.8.8"
  }

diseqc/config/settings/mqtt
  Payload: JSON object
  QoS: 1
  Retained: Yes
  Example:
  {
    "broker": "192.168.1.50",
    "port": 1883,
    "client_id": "diseqc_controller",
    "username": "diseqc",
    "reconnect_interval": 5
  }

diseqc/config/settings/rotor
  Payload: JSON object
  QoS: 1
  Retained: Yes
  Example:
  {
    "max_angle_east": 80,
    "max_angle_west": -80,
    "step_size_degrees": 1.0,
    "movement_timeout_sec": 60
  }
```

### 4. Availability (LWT - Last Will and Testament)

```
diseqc/availability
  Payload: "online" | "offline"
  Description: Device availability (MQTT LWT)
  QoS: 1
  Retained: Yes
  
  Set as LWT when connecting:
  - Will Topic: diseqc/availability
  - Will Payload: offline
  - Will QoS: 1
  - Will Retain: true
  
  Publish "online" after connection established
```

### 5. Discovery (Home Assistant)

```
homeassistant/select/diseqc/satellite/config
  Payload: JSON (HA discovery)
  QoS: 1
  Retained: Yes
  Example:
  {
    "name": "Satellite Position",
    "unique_id": "diseqc_satellite_select",
    "command_topic": "diseqc/command/goto/satellite",
    "state_topic": "diseqc/status/position/satellite",
    "options": ["astra_19.2e", "hotbird_13e", "astra_28.2e"],
    "availability_topic": "diseqc/availability",
    "device": {
      "identifiers": ["diseqc_controller"],
      "name": "DiSEqC Rotor Controller",
      "model": "STM32F407VGT6",
      "manufacturer": "Custom"
    }
  }

homeassistant/number/diseqc/angle/config
  Payload: JSON (HA discovery)
  QoS: 1
  Retained: Yes
  Example:
  {
    "name": "Rotor Angle",
    "unique_id": "diseqc_angle_number",
    "command_topic": "diseqc/command/goto/angle",
    "state_topic": "diseqc/status/position/angle",
    "min": -80,
    "max": 80,
    "step": 1,
    "unit_of_measurement": "°",
    "availability_topic": "diseqc/availability"
  }
```

## 🎯 Payload Formats

### Simple Values (Recommended for Most)
```
Single value: "45.5"
Boolean: "true" or "false"
Empty: "" (for commands without parameters)
```

**Advantages:**
- Easy to publish from command line
- Lower bandwidth
- Simple to parse

### JSON (For Complex Data)
```json
{
  "angle": 45.5,
  "satellite": "astra_19.2e",
  "timestamp": 1234567890
}
```

**Advantages:**
- Multiple fields
- Type safety
- Extensible

## 🔒 QoS Recommendations

| Topic Type | QoS | Retained | Reason |
|------------|-----|----------|--------|
| Commands | 1 | No | Ensure delivery, don't replay old commands |
| Status (state) | 1 | Yes | Important state, subscribers need last value |
| Status (position) | 0/1 | Yes | Frequent updates, last value important |
| Status (signal) | 0 | No | High frequency, real-time only |
| Config | 1 | Yes | Critical data, subscribers need last value |
| Availability | 1 | Yes | LWT requirement |

## 📱 Client Examples

### Python Client
```python
import paho.mqtt.client as mqtt

def on_connect(client, userdata, flags, rc):
    # Subscribe to all status topics
    client.subscribe("diseqc/status/#")
    print("Connected to MQTT broker")

def on_message(client, userdata, msg):
    print(f"{msg.topic}: {msg.payload.decode()}")

client = mqtt.Client("diseqc_monitor")
client.on_connect = on_connect
client.on_message = on_message
client.connect("192.168.1.50", 1883, 60)

# Move to satellite
client.publish("diseqc/command/goto/satellite", "astra_19.2e")

# Or move to angle
client.publish("diseqc/command/goto/angle", "45.5")

client.loop_forever()
```

### Node-RED Flow
```json
[
  {
    "id": "mqtt_in",
    "type": "mqtt in",
    "topic": "diseqc/status/#",
    "broker": "mqtt_broker",
    "name": "DiSEqC Status"
  },
  {
    "id": "debug",
    "type": "debug",
    "name": "Show Status"
  }
]
```

### Command Line (Mosquitto)
```bash
# Subscribe to all status
mosquitto_sub -h 192.168.1.50 -t 'diseqc/status/#' -v

# Go to satellite
mosquitto_pub -h 192.168.1.50 -t diseqc/command/goto/satellite -m "astra_19.2e"

# Manual step
mosquitto_pub -h 192.168.1.50 -t diseqc/command/manual/step_east -m "5"

# Check availability
mosquitto_sub -h 192.168.1.50 -t diseqc/availability -v
```

## 🏠 Home Assistant Integration

### configuration.yaml
```yaml
mqtt:
  select:
    - name: "Satellite Position"
      command_topic: "diseqc/command/goto/satellite"
      state_topic: "diseqc/status/position/satellite"
      options:
        - "astra_19.2e"
        - "hotbird_13e"
        - "astra_28.2e"
      availability_topic: "diseqc/availability"
      
  number:
    - name: "Rotor Angle"
      command_topic: "diseqc/command/goto/angle"
      state_topic: "diseqc/status/position/angle"
      min: -80
      max: 80
      step: 1
      unit_of_measurement: "°"
      availability_topic: "diseqc/availability"
      
  binary_sensor:
    - name: "Rotor Busy"
      state_topic: "diseqc/status/busy"
      payload_on: "true"
      payload_off: "false"
      availability_topic: "diseqc/availability"
```

## 📊 Topic Usage Statistics

Typical message frequency (per minute):

| Topic Pattern | Frequency | Bandwidth |
|---------------|-----------|-----------|
| `command/*` | 0-10 | Low |
| `status/state` | 1-5 | Low |
| `status/position/*` | 5-20 | Low |
| `status/signal/*` | 60+ | Medium |
| `config/*` | <1 | Very Low |
| `availability` | <1 | Very Low |

**Total bandwidth**: <1 KB/min (very light on network)

## 🔄 Message Flow Example

### Scenario: Move to Satellite

```
1. User publishes:
   Topic: diseqc/command/goto/satellite
   Payload: "astra_19.2e"

2. Device publishes:
   Topic: diseqc/status/state
   Payload: "moving"
   
   Topic: diseqc/status/busy
   Payload: "true"

3. Device sends DiSEqC command to rotor...

4. During movement, device publishes:
   Topic: diseqc/status/position/angle
   Payload: "18.5" (every 500ms)

5. Movement complete, device publishes:
   Topic: diseqc/status/state
   Payload: "idle"
   
   Topic: diseqc/status/position/angle
   Payload: "19.2"
   
   Topic: diseqc/status/position/satellite
   Payload: "astra_19.2e"
   
   Topic: diseqc/status/busy
   Payload: "false"
```

## 🛡️ Security Considerations

### Authentication
```csharp
// In C# code
var options = new MqttClientOptionsBuilder()
    .WithTcpServer("192.168.1.50", 1883)
    .WithCredentials("diseqc", "your_password")  // Use strong password
    .WithClientId("diseqc_controller")
    .WithWillTopic("diseqc/availability")
    .WithWillPayload("offline")
    .WithWillRetain(true)
    .Build();
```

### Access Control (Mosquitto ACL)
```
# /etc/mosquitto/acl.conf
# Read-only monitoring user
user monitor
topic read diseqc/status/#
topic read diseqc/availability

# Control user
user controller
topic read diseqc/#
topic write diseqc/command/#

# Admin user
user admin
topic readwrite diseqc/#
```

## 📝 Best Practices

1. **Always use availability** - Clients know when device is offline
2. **Retain important state** - New clients get current state immediately
3. **Use QoS 1 for commands** - Ensure delivery
4. **Keep payloads simple** - JSON only when necessary
5. **Namespace properly** - Use `diseqc/` prefix consistently
6. **Document units** - Include in topic name or description
7. **Version your API** - Consider `diseqc/v1/` if you expect breaking changes

---

**Complete MQTT topic structure ready!** 🎯

This design gives you:
- ✅ Clean hierarchy
- ✅ Home Assistant integration
- ✅ Command/status separation
- ✅ Extensible for future features
- ✅ Standard MQTT practices

Next: Configuration management design!

