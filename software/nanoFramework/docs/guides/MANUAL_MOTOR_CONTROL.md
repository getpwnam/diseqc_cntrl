# Manual Motor Control - Usage Guide

## 🎮 Manual Control Functions Added

You can now manually control the satellite dish rotor with step-by-step or continuous movement!

## 📋 DiSEqC 1.2 Manual Commands

### Available Commands

| Command | DiSEqC Bytes | Description |
|---------|--------------|-------------|
| **StepEast** | `E0 31 68 XX` | Move East by XX steps |
| **StepWest** | `E0 31 69 XX` | Move West by XX steps |
| **DriveEast** | `E0 31 68 00` | Continuous East (until Halt) |
| **DriveWest** | `E0 31 69 00` | Continuous West (until Halt) |
| **Halt** | `E0 31 60` | Stop movement |

### Step Size
- Typically **1 step ≈ 1 degree** (rotor dependent)
- Maximum: 128 steps per command
- For larger movements, use multiple step commands or GotoAngle

## 💻 C# API Reference

### Low-Level API (DiSEqC.Native)

```csharp
using DiseqC.Native;

// Step movements (incremental)
DiSEqC.StepEast(1);      // Move 1 step East (~1°)
DiSEqC.StepEast(5);      // Move 5 steps East (~5°)
DiSEqC.StepWest(1);      // Move 1 step West (~1°)
DiSEqC.StepWest(10);     // Move 10 steps West (~10°)

// Continuous movements (hold)
DiSEqC.DriveEast();      // Start moving East
Thread.Sleep(2000);      // Move for 2 seconds
DiSEqC.Halt();           // Stop

DiSEqC.DriveWest();      // Start moving West
Thread.Sleep(1000);      // Move for 1 second
DiSEqC.Halt();           // Stop

// Check status
bool busy = DiSEqC.IsBusy();
```

### High-Level API (RotorManager)

```csharp
using DiseqC.Manager;

var rotor = new RotorManager();

// Single step movements
rotor.StepEast();        // Move 1 step East (default)
rotor.StepWest();        // Move 1 step West (default)
rotor.StepEast(5);       // Move 5 steps East
rotor.StepWest(3);       // Move 3 steps West

// Continuous movements
rotor.DriveEast();       // Start continuous East
Thread.Sleep(2000);
rotor.Halt();            // Stop

rotor.DriveWest();       // Start continuous West
Thread.Sleep(1500);
rotor.Halt();            // Stop
```

## 🎯 Usage Examples

### Example 1: Fine-Tuning Position

```csharp
using DiseqC.Manager;

var rotor = new RotorManager();

// Go to approximate position
rotor.GotoAngle(45.0f);
while (rotor.IsBusy()) Thread.Sleep(100);

// Fine-tune manually
rotor.StepEast();  // Nudge slightly East
Thread.Sleep(500);

rotor.StepWest(2); // Adjust slightly West
Thread.Sleep(500);
```

### Example 2: Button-Based Manual Control

```csharp
// Button press handlers
void OnEastButtonPressed()
{
    if (!rotor.IsBusy())
    {
        rotor.StepEast(1);  // Single step
    }
}

void OnWestButtonPressed()
{
    if (!rotor.IsBusy())
    {
        rotor.StepWest(1);  // Single step
    }
}

// Long press for continuous
void OnEastButtonHeld()
{
    rotor.DriveEast();  // Start continuous
}

void OnButtonReleased()
{
    rotor.Halt();  // Stop on release
}
```

### Example 3: MQTT Manual Control

```csharp
void OnMqttMessage(object sender, MqttMsgPublishEventArgs e)
{
    string topic = e.Topic;
    string payload = Encoding.UTF8.GetString(e.Message);
    
    switch (topic)
    {
        case "diseqc/manual/step_east":
            rotor.StepEast(byte.Parse(payload));  // Payload: "1" or "5"
            break;
            
        case "diseqc/manual/step_west":
            rotor.StepWest(byte.Parse(payload));
            break;
            
        case "diseqc/manual/drive_east":
            rotor.DriveEast();
            break;
            
        case "diseqc/manual/drive_west":
            rotor.DriveWest();
            break;
            
        case "diseqc/manual/halt":
            rotor.Halt();
            break;
    }
}
```

### Example 4: Web Interface Control

```csharp
// REST API endpoints
[Route("api/rotor/step/east")]
public IActionResult StepEast([FromQuery] byte steps = 1)
{
    if (rotor.IsBusy())
        return BadRequest("Rotor is busy");
    
    rotor.StepEast(steps);
    return Ok($"Stepped East {steps} step(s)");
}

[Route("api/rotor/step/west")]
public IActionResult StepWest([FromQuery] byte steps = 1)
{
    if (rotor.IsBusy())
        return BadRequest("Rotor is busy");
    
    rotor.StepWest(steps);
    return Ok($"Stepped West {steps} step(s)");
}

[Route("api/rotor/halt")]
public IActionResult Halt()
{
    rotor.Halt();
    return Ok("Rotor halted");
}
```

### Example 5: Automatic Scanning

```csharp
// Scan for satellite signal by stepping
async Task ScanForSignal()
{
    const int STEPS_PER_SCAN = 1;
    const int MAX_STEPS = 30;  // Scan ±30 degrees
    
    // Start from current position
    for (int i = 0; i < MAX_STEPS; i++)
    {
        rotor.StepEast(STEPS_PER_SCAN);
        await Task.Delay(1000);  // Wait for movement + signal check
        
        float signalQuality = ReadSignalQuality();  // Your signal reading
        
        if (signalQuality > 80.0f)
        {
            Console.WriteLine($"Signal found! Quality: {signalQuality}%");
            return;
        }
    }
    
    // Scan back West
    for (int i = 0; i < MAX_STEPS * 2; i++)
    {
        rotor.StepWest(STEPS_PER_SCAN);
        await Task.Delay(1000);
        
        float signalQuality = ReadSignalQuality();
        
        if (signalQuality > 80.0f)
        {
            Console.WriteLine($"Signal found! Quality: {signalQuality}%");
            return;
        }
    }
    
    Console.WriteLine("Signal not found in scan range");
}
```

## ⚠️ Important Notes

### Safety Considerations

1. **Check Busy State**
   ```csharp
   if (!rotor.IsBusy())
   {
       rotor.StepEast();  // Only if not already moving
   }
   ```

2. **Always Halt Continuous Movement**
   ```csharp
   rotor.DriveEast();
   // ALWAYS call Halt() to stop!
   Thread.Sleep(2000);
   rotor.Halt();  // ✅ Don't forget!
   ```

3. **Respect Motor Limits**
   - Most rotors have software/hardware limits
   - Don't drive past ±80 degrees
   - The rotor will typically auto-halt at limits

4. **Don't Spam Commands**
   ```csharp
   rotor.StepEast();
   while (rotor.IsBusy()) Thread.Sleep(50);  // Wait for completion
   rotor.StepEast();  // Now safe to send next command
   ```

### Step Size Calibration

Different rotors have different step sizes:
- **Most DiSEqC 1.2 rotors**: 1 step ≈ 1 degree
- **High-precision rotors**: 1 step ≈ 0.5 degree
- **Check your rotor manual** for exact step size

### Command Timing

- Each step command takes ~67ms to transmit
- Rotor movement time varies (typically 50-200ms per step)
- Add delays between commands for rotor to complete movement

## 🧪 Testing Manual Control

### Test 1: Single Step

```csharp
rotor.GotoAngle(0);  // Go to reference position
Thread.Sleep(5000);  // Wait to complete

rotor.StepEast();    // Step East once
Thread.Sleep(2000);  // Wait

// Verify position changed (signal meter, compass, etc.)
```

### Test 2: Multiple Steps

```csharp
for (int i = 0; i < 5; i++)
{
    rotor.StepEast();
    Thread.Sleep(1000);  // Wait between steps
}

// Should have moved ~5 degrees East
```

### Test 3: Continuous Drive

```csharp
Console.WriteLine("Starting continuous East...");
rotor.DriveEast();

Thread.Sleep(3000);  // Drive for 3 seconds

Console.WriteLine("Stopping...");
rotor.Halt();

// Rotor should have moved continuously for 3 seconds
```

## 📊 MQTT Topics for Manual Control

### Subscribe Topics (Commands)

```
diseqc/manual/step_east      → Payload: "1" to "128" (number of steps)
diseqc/manual/step_west      → Payload: "1" to "128"
diseqc/manual/drive_east     → Payload: any (starts continuous)
diseqc/manual/drive_west     → Payload: any (starts continuous)
diseqc/manual/halt           → Payload: any (stops movement)
```

### Publish Topics (Status)

```
diseqc/status                → "stepping_east" / "stepping_west" / "driving_east" / "driving_west" / "idle"
diseqc/position              → Current angle (if tracked)
```

## 🎮 UI Control Example

```html
<!-- Simple web UI -->
<div class="rotor-control">
    <button onclick="stepWest()">◀ Step West</button>
    <button onclick="halt()">■ HALT</button>
    <button onclick="stepEast()">Step East ▶</button>
    
    <br>
    
    <button onmousedown="driveWest()" onmouseup="halt()">◀◀ Hold West</button>
    <button onmousedown="driveEast()" onmouseup="halt()">Hold East ▶▶</button>
</div>

<script>
function stepEast() {
    fetch('/api/rotor/step/east?steps=1');
}

function stepWest() {
    fetch('/api/rotor/step/west?steps=1');
}

function driveEast() {
    fetch('/api/rotor/drive/east');
}

function driveWest() {
    fetch('/api/rotor/drive/west');
}

function halt() {
    fetch('/api/rotor/halt');
}
</script>
```

---

**You can now manually control your satellite dish rotor with precision!** 🎯

For automatic positioning, use `GotoAngle()`. For fine-tuning and manual control, use the step/drive functions above.

