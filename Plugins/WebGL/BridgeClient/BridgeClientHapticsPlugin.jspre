// BridgeClient Haptics Plugin - Pre-processed JavaScript
// Loaded before Emscripten runtime initialization
// Provides variable intensity haptics via parent Unity app's HapticsBridge

Module['BridgeClientHaptics'] = Module['BridgeClientHaptics'] || {};

// State
Module['BridgeClientHaptics'].isAvailable = false;
Module['BridgeClientHaptics'].isChecked = false;

// Check if BridgeClient.HapticsBridge is available
Module['BridgeClientHaptics'].checkAvailability = function() {
    return typeof window !== 'undefined' &&
           window.BridgeClient &&
           window.BridgeClient.HapticsBridge;
};

// Get haptics status from parent Unity app
Module['BridgeClientHaptics'].getStatus = async function() {
    if (!Module['BridgeClientHaptics'].checkAvailability()) {
        return { available: false, reason: 'BridgeClient.HapticsBridge not found' };
    }

    try {
        var status = await window.BridgeClient.HapticsBridge.getStatus();
        Module['BridgeClientHaptics'].isAvailable = status.available === true;
        Module['BridgeClientHaptics'].isChecked = true;
        return status;
    } catch (err) {
        console.error('[BridgeClientHaptics] getStatus failed:', err);
        Module['BridgeClientHaptics'].isAvailable = false;
        Module['BridgeClientHaptics'].isChecked = true;
        return { available: false, reason: err.message || 'Unknown error' };
    }
};

// Trigger a single vibration pulse with intensity (0-1)
Module['BridgeClientHaptics'].triggerOne = async function(intensity) {
    if (!Module['BridgeClientHaptics'].checkAvailability()) {
        console.warn('[BridgeClientHaptics] HapticsBridge not available');
        return false;
    }

    try {
        // Clamp intensity to 0-1 range
        var clampedIntensity = Math.max(0, Math.min(1, intensity));
        await window.BridgeClient.HapticsBridge.triggerOne({ intensity: clampedIntensity });
        return true;
    } catch (err) {
        console.error('[BridgeClientHaptics] triggerOne failed:', err);
        return false;
    }
};

// Play a haptic curve with keyframes
// keys: array of { time, value, inTangent?, outTangent? }
// strength: multiplier (default 1)
// useUnscaledTime: use realtime timing (default false)
Module['BridgeClientHaptics'].playCurve = async function(keysJson, strength, useUnscaledTime) {
    if (!Module['BridgeClientHaptics'].checkAvailability()) {
        console.warn('[BridgeClientHaptics] HapticsBridge not available');
        return false;
    }

    try {
        var keys = JSON.parse(keysJson);

        // Validate keys
        if (!Array.isArray(keys) || keys.length < 2) {
            console.error('[BridgeClientHaptics] playCurve requires at least 2 keyframes');
            return false;
        }

        var payload = { keys: keys };

        if (typeof strength === 'number' && strength !== 1) {
            payload.strength = strength;
        }

        if (useUnscaledTime === true) {
            payload.useUnscaledTime = true;
        }

        await window.BridgeClient.HapticsBridge.playCurve(payload);
        return true;
    } catch (err) {
        console.error('[BridgeClientHaptics] playCurve failed:', err);
        return false;
    }
};

// Stop any in-progress haptics
Module['BridgeClientHaptics'].stop = async function() {
    if (!Module['BridgeClientHaptics'].checkAvailability()) {
        return false;
    }

    try {
        await window.BridgeClient.HapticsBridge.stop();
        return true;
    } catch (err) {
        console.error('[BridgeClientHaptics] stop failed:', err);
        return false;
    }
};

// Callback storage for async operations
Module['BridgeClientHaptics'].pendingCallbacks = {};
Module['BridgeClientHaptics'].callbackId = 0;

// Execute async operation and call back to C# when done
Module['BridgeClientHaptics'].executeAsync = function(operation, callbackPtr) {
    var id = ++Module['BridgeClientHaptics'].callbackId;

    operation.then(function(result) {
        // Call back to C# with success
        if (callbackPtr) {
            var resultInt = result ? 1 : 0;
            {{{ makeDynCall('vii', 'callbackPtr') }}}(id, resultInt);
        }
    }).catch(function(err) {
        console.error('[BridgeClientHaptics] Async operation failed:', err);
        // Call back to C# with failure
        if (callbackPtr) {
            {{{ makeDynCall('vii', 'callbackPtr') }}}(id, 0);
        }
    });

    return id;
};
