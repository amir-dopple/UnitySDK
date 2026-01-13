namespace Dopple.InputSDK
{
    /// <summary>
    /// Input modes for gyro/input controllers.
    /// Determines which input source to use for reading device orientation.
    /// </summary>
    public enum InputMode
    {
        /// <summary>
        /// Auto-detect best available input source.
        /// Priority: BridgeClientGyro (WebGL WebView) > Gyro (native) > MouseDelta (fallback)
        /// </summary>
        Auto,

        /// <summary>
        /// Native gyro via Unity Input System GravitySensor.
        /// Works on Android/iOS devices with hardware gyroscope.
        /// </summary>
        Gyro,

        /// <summary>
        /// WebView BridgeClient gyroscope.
        /// Receives gyro data from parent Unity app hosting this WebGL build in a WebView.
        /// Only available on WebGL platform when running inside a compatible parent app.
        /// </summary>
        BridgeClientGyro,

        /// <summary>
        /// Mouse X position maps to output value.
        /// Useful for testing in editor or desktop builds.
        /// </summary>
        MousePosition,

        /// <summary>
        /// Mouse delta movement changes output value.
        /// Useful for testing in editor - drag to change angle/direction.
        /// </summary>
        MouseDelta,

        /// <summary>
        /// Scroll wheel changes output value.
        /// Alternative input method for testing.
        /// </summary>
        ScrollWheel
    }
}
