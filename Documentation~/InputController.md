# Input Controller Documentation

## Overview

The Dopple Input SDK provides two input controller variants for gyroscope-based games:

- **RotationInputController**: Outputs rotation angle (0-360 degrees) for paddle/steering games
- **DirectionInputController**: Outputs 2D direction vector for character movement games

Both controllers share common functionality through `InputControllerBase`:
- Multi-source input (native gyro, BridgeClient, mouse)
- Auto-detection of best available input source
- One Euro Filter for smoothing
- External gyro provider support

## Gyro Coordinate System

### Device Orientation

The SDK uses Unity's **GravitySensor** (New Input System) which provides:
- Gravity vector in device-local coordinates
- When device is upright (portrait): `gravity = (0, -9.8, 0)`
- Magnitude approximately 9.8 m/s^2

### Coordinate Axes

```
Device held upright (portrait mode):

    +Y (top of phone)
     ^
     |
     |
     +---> +X (right side of phone)
    /
   v
  +Z (toward user)

Gravity points DOWN when upright: (0, -9.8, 0)
```

---

## Z-Axis Rotation Extraction (RotationInputController)

Used for games like Breakout where tilting rotates an object.

### Algorithm

1. **Project gravity onto XY plane** (removes forward/back tilt component)
   ```csharp
   projectedGravity = Vector3.ProjectOnPlane(gravity, Vector3.forward)
   ```

2. **Calculate rotation from "down" to projected gravity**
   ```csharp
   rotation = Quaternion.FromToRotation(Vector3.down, projectedGravity.normalized)
   ```

3. **Extract Z euler angle**
   ```csharp
   angle = rotation.eulerAngles.z
   ```

### Visual Representation

```
       Y (up)
       |
       |   /  <- projectedGravity when tilted right
       |  /
       | / angle
       |/_______ X (right)

Tilting device right: angle increases (positive)
Tilting device left:  angle decreases (negative, wraps to ~360)
```

### Code Example

```csharp
// Get rotation angle from RotationInputController
float angle = RotationInputController.Instance.CurrentRotationAngle;

// Apply to paddle rotation
paddle.rotation = Quaternion.Euler(0, 0, angle);
```

---

## X/Z Direction Extraction (DirectionInputController)

Used for games where tilting moves a character in world space.

### Algorithm

1. **Calibrate neutral position** (store `referenceGravity` when player holds device)
   ```csharp
   referenceGravity = gravitySensor.gravity.ReadValue();
   ```

2. **Calculate delta from reference**
   ```csharp
   deltaGravity = currentGravity - referenceGravity
   ```

3. **Map delta to movement axes**
   ```csharp
   movementX = deltaGravity.y   // Tilt left/right
   movementZ = -deltaGravity.x  // Tilt forward/back (inverted)
   ```

### Why These Mappings?

When holding phone upright:
- Tilting **left/right** changes `gravity.y` (phone tips, gravity X shifts)
- Tilting **forward/back** changes `gravity.x` (phone tips, gravity Y shifts)

The inversion on Z is because tilting forward (top of phone away from you) decreases gravity.x, but we want forward movement to be positive.

### Code Example

```csharp
// Get movement direction from DirectionInputController
Vector2 input = DirectionInputController.Instance.MovementInput;

// Convert to 3D movement
Vector3 movement = new Vector3(input.x, 0, input.y);
character.Move(movement * speed * Time.deltaTime);
```

---

## Input Mode Selection

### Auto Mode Priority

| Platform | Priority Order |
|----------|----------------|
| WebGL in WebView | BridgeClientGyro > MouseDelta |
| Android/iOS | Gyro (native) > MouseDelta |
| Editor | MouseDelta (for testing) |

### Manual Mode Selection

```csharp
// Force a specific input mode
inputController.SetInputMode(InputMode.Gyro);
inputController.SetInputMode(InputMode.MouseDelta);
inputController.SetInputMode(InputMode.BridgeClientGyro);
```

---

## BridgeClient (WebGL WebView)

When running WebGL builds inside a native app's WebView:

1. Parent app provides gyro data via `window.BridgeClient.GyroDataBridge`
2. Uses shared memory for zero-copy data transfer (14 floats = 56 bytes)
3. Parent app can control movement enable/disable

### Data Structure

```csharp
struct BridgeClientGyroData {
    Vector3 gravity;        // Raw gravity from parent
    Vector3 smoothGravity;  // Pre-filtered by parent
    Quaternion orientation; // Device orientation
    bool gyroEnabled;       // Is gyro active
    bool allowMovement;     // Can game respond to input
}
```

### Coordinate Conversion

The parent app uses old Input System (`Input.gyro.gravity`) which has different coordinates:
```csharp
// In DirectionInputController.UpdateBridgeClientGyroInput():
// Convert from old to new Input System coordinates
gravity = new Vector3(-gravity.y, gravity.x, gravity.z);
```

---

## Calibration

### Rotation Calibration

```csharp
// Set current position as 0 degrees
RotationInputController.Instance.CalibrateGyro();

// Set current position to match a specific angle (e.g., current paddle angle)
RotationInputController.Instance.CalibrateGyroToAngle(currentPaddleAngle);
```

### Direction Calibration

```csharp
// Set current position as neutral (no movement)
DirectionInputController.Instance.CalibrateGyro();

// Happens automatically on first valid gravity reading
```

---

## Smoothing (One Euro Filter)

The One Euro Filter reduces gyro jitter while maintaining responsiveness at high speeds.

### Parameters

- **minCutoff** (0.1-10): Lower = less jitter, more lag at slow movements
- **beta** (0-1): Higher = less lag at fast movements

### Configuration

```csharp
// Enable/disable smoothing
inputController.SetGyroSmoothingEnabled(true);

// Adjust parameters
inputController.SetSmoothingParameters(minCutoff: 1.0f, beta: 0.5f);

// Reset filter (useful after calibration)
inputController.ResetSmoothingFilter();
```

### Recommended Settings

| Use Case | minCutoff | beta |
|----------|-----------|------|
| Precise control (puzzle) | 0.5 | 0.3 |
| Action game (default) | 1.0 | 0.5 |
| Fast-paced (racing) | 2.0 | 0.7 |

---

## External Gyro Provider

Implement `IGyroProvider` to use custom gyro sources:

```csharp
public class MyCustomGyro : MonoBehaviour, IGyroProvider
{
    public Quaternion GetRotatingObjectRotation()
    {
        // Return device orientation quaternion
        return myCustomOrientation;
    }

    public Vector3 GetGravity()
    {
        // Return gravity vector
        return myCustomGravity;
    }

    public bool IsAvailable => myGyroIsReady;
}

// Assign in inspector or at runtime:
inputController.SetExternalGyroProvider(myCustomGyro);
```

---

## Migration from Game-Specific Controllers

### From BreakoutInputController

```csharp
// Before
float angle = BreakoutInputController.Instance.CurrentRotationAngle;
BreakoutInputController.Instance.CalibrateGyroToAngle(targetAngle);

// After
float angle = RotationInputController.Instance.CurrentRotationAngle;
RotationInputController.Instance.CalibrateGyroToAngle(targetAngle);
```

### From BugsInputController

```csharp
// Before
Vector2 input = BugsInputController.Instance.MovementInput;
BugsInputController.Instance.CalibrateGyro();

// After
Vector2 input = DirectionInputController.Instance.MovementInput;
DirectionInputController.Instance.CalibrateGyro();
```

---

## Troubleshooting

### Gyro not working on Android

1. Check if GravitySensor is available: `InputSystem.devices`
2. Ensure accelerometerFrequency is set (Project Settings > Player > Resolution)
3. Try calling `InputSystem.EnableDevice(GravitySensor.current)`

### Gyro not working on WebGL

1. BridgeClient only works inside a WebView with parent app support
2. Check browser console for `[BridgeClientGyro]` messages
3. Ensure HTTPS (required for DeviceOrientation API)

### Movement feels laggy

1. Reduce smoothingMinCutoff (try 0.5)
2. Increase smoothingBeta (try 0.7)
3. Or disable smoothing: `SetGyroSmoothingEnabled(false)`

### Movement has jitter

1. Increase smoothingMinCutoff (try 2.0)
2. Decrease smoothingBeta (try 0.3)
3. Check if device has quality gyroscope
