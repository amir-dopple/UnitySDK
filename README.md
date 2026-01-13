# Dopple Input SDK

Cross-platform input SDK providing gyroscope control, vibration haptics, and WebView bridge communication for Unity games.

## Features

- **Input Controllers**: Gyro-based input with rotation (paddle games) or direction (character movement) output
- **VibrationManager**: Cross-platform haptic feedback (Android native, WebGL browser API, BridgeClient)
- **BridgeClient**: WebView communication for WebGL games hosted in native app WebViews
- **One Euro Filter**: Smooth gyro input with speed-adaptive filtering
- **OrientationPostProcessor**: Removes screen orientation lock from Android builds

## Installation

### Via Package Manager (Recommended)

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dopple.input-sdk": "file:../../Packages/com.dopple.input-sdk"
  }
}
```

Or if using git:
```json
{
  "dependencies": {
    "com.dopple.input-sdk": "https://github.com/dopple/input-sdk.git"
  }
}
```

## Quick Start

### Rotation Input (Breakout-style)

```csharp
using Dopple.InputSDK;

public class PaddleController : MonoBehaviour
{
    void Update()
    {
        float angle = RotationInputController.Instance.CurrentRotationAngle;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
```

### Direction Input (Character Movement)

```csharp
using Dopple.InputSDK;

public class CharacterController : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        Vector2 input = DirectionInputController.Instance.MovementInput;
        Vector3 movement = new Vector3(input.x, 0, input.y) * speed * Time.deltaTime;
        transform.Translate(movement);
    }
}
```

### Vibration

```csharp
using Dopple.InputSDK.Vibration;

public class GameManager : MonoBehaviour
{
    public VibrationCurveEffect hitEffect;

    void OnHit()
    {
        VibrationManager.Instance.PlayCurve(hitEffect);
        // Or simple vibration:
        // VibrationManager.Instance.Vibrate(100); // 100ms
    }
}
```

## Components

### RotationInputController

Outputs rotation angle (0-360 degrees). Best for:
- Paddle games (Breakout)
- Steering wheel controls
- Rotating objects

### DirectionInputController

Outputs 2D movement direction (Vector2). Best for:
- Character movement
- Racing games
- Top-down shooters

### VibrationManager

Cross-platform vibration with AnimationCurve support:
- Android: Native VibrationEffect API with amplitude control
- WebGL: Browser Vibration API + BridgeClient for variable intensity
- Editor: Debug logging

### BridgeClient

WebView communication for WebGL builds:
- **BridgeClientGyroManager**: Receives gyro data from parent app
- **BridgeClientHapticsManager**: Sends haptic commands to parent app

## Documentation

See [Documentation~/InputController.md](Documentation~/InputController.md) for detailed documentation on:
- Gyro coordinate system
- Input mode selection
- Calibration
- Smoothing parameters
- Migration guide

## Requirements

- Unity 2021.3+
- Unity Input System 1.4.0+
- For WebGL: Parent app with BridgeClient support (optional)

## License

Copyright Dopple. All rights reserved.
