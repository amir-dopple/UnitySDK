// BridgeClient Haptics Plugin - JavaScript Library
// Exports functions callable from C# via DllImport
// Provides variable intensity haptics via parent Unity app

mergeInto(LibraryManager.library, {
    // Check if BridgeClient.HapticsBridge is available
    BridgeClientHaptics_IsAvailable: function() {
        return Module['BridgeClientHaptics'].checkAvailability() ? 1 : 0;
    },

    // Check and cache availability status (async, fire-and-forget)
    // Call this at startup, then use IsAvailable/IsHapticsReady to check result
    BridgeClientHaptics_CheckStatus: function() {
        Module['BridgeClientHaptics'].getStatus();
    },

    // Check if haptics status has been checked
    BridgeClientHaptics_IsStatusChecked: function() {
        return Module['BridgeClientHaptics'].isChecked ? 1 : 0;
    },

    // Check if haptics are actually available (after status check)
    BridgeClientHaptics_IsHapticsReady: function() {
        return Module['BridgeClientHaptics'].isAvailable ? 1 : 0;
    },

    // Trigger a single vibration pulse with intensity (0-1)
    // intensity is passed as int (0-100) and converted to 0-1 range
    BridgeClientHaptics_TriggerOne: function(intensityPercent) {
        var intensity = intensityPercent / 100.0;
        Module['BridgeClientHaptics'].triggerOne(intensity);
    },

    // Play a haptic curve
    // keysPtr: pointer to UTF8 JSON string of keyframes
    // strength: multiplier as int (100 = 1.0)
    // useUnscaledTime: 1 for true, 0 for false
    BridgeClientHaptics_PlayCurve: function(keysPtr, strengthPercent, useUnscaledTime) {
        var keysJson = UTF8ToString(keysPtr);
        var strength = strengthPercent / 100.0;
        var unscaled = useUnscaledTime !== 0;
        Module['BridgeClientHaptics'].playCurve(keysJson, strength, unscaled);
    },

    // Stop any in-progress haptics
    BridgeClientHaptics_Stop: function() {
        Module['BridgeClientHaptics'].stop();
    }
});
