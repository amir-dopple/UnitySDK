// BridgeClient Gyro Plugin - Pre-processed JavaScript
// Loaded before Emscripten runtime initialization
// Receives gyro data from parent Unity app via BridgeClient.GyroDataBridge

Module['BridgeClientGyro'] = Module['BridgeClientGyro'] || {};

// State
Module['BridgeClientGyro'].isAvailable = false;
Module['BridgeClientGyro'].isListening = false;
Module['BridgeClientGyro'].unsubscribe = null;
Module['BridgeClientGyro'].dataRef = null;
Module['BridgeClientGyro'].dataArr = null;

// Check if BridgeClient.GyroDataBridge is available
Module['BridgeClientGyro'].checkAvailability = function() {
    return typeof window !== 'undefined' &&
           window.BridgeClient &&
           window.BridgeClient.GyroDataBridge;
};

// Start listening to gyro data from BridgeClient
Module['BridgeClientGyro'].start = async function(hz) {
    if (Module['BridgeClientGyro'].isListening) {
        console.log('[BridgeClientGyro] Already listening');
        return true;
    }

    if (!Module['BridgeClientGyro'].checkAvailability()) {
        console.warn('[BridgeClientGyro] BridgeClient.GyroDataBridge not available');
        return false;
    }

    try {
        var gyro = window.BridgeClient.GyroDataBridge;

        // Start the gyro data stream from parent Unity app
        await gyro.start({ hz: hz || 60 });

        // Subscribe to gyro data events
        Module['BridgeClientGyro'].unsubscribe = gyro.onEvent('gyroData', function(payload) {
            Module['BridgeClientGyro'].handleGyroData(payload);
        });

        Module['BridgeClientGyro'].isAvailable = true;
        Module['BridgeClientGyro'].isListening = true;
        console.log('[BridgeClientGyro] Started listening at ' + (hz || 60) + ' Hz');
        return true;
    } catch (err) {
        console.error('[BridgeClientGyro] Failed to start:', err);
        return false;
    }
};

// Stop listening to gyro data
Module['BridgeClientGyro'].stop = function() {
    if (!Module['BridgeClientGyro'].isListening) {
        return;
    }

    // Unsubscribe from events
    if (Module['BridgeClientGyro'].unsubscribe) {
        Module['BridgeClientGyro'].unsubscribe();
        Module['BridgeClientGyro'].unsubscribe = null;
    }

    // Stop the gyro data stream
    if (Module['BridgeClientGyro'].checkAvailability()) {
        try {
            window.BridgeClient.GyroDataBridge.stop();
        } catch (err) {
            console.warn('[BridgeClientGyro] Error stopping:', err);
        }
    }

    Module['BridgeClientGyro'].isListening = false;
    console.log('[BridgeClientGyro] Stopped listening');
};

// Handle incoming gyro data - write to shared memory
// Data layout (14 floats = 56 bytes):
//   [0-2]   gravity (x, y, z)
//   [3-5]   smoothGravity (x, y, z)
//   [6-9]   orientation quaternion (x, y, z, w)
//   [10]    gyroEnabled (1.0 or 0.0)
//   [11]    allowMovement (1.0 or 0.0)
//   [12-13] reserved
Module['BridgeClientGyro'].handleGyroData = function(payload) {
    // Skip if no data pointer registered
    if (!Module['BridgeClientGyro'].dataArr) {
        return;
    }

    var arr = Module['BridgeClientGyro'].dataArr;

    // Check for buffer resize (Emscripten can resize HEAP)
    if (arr.byteLength === 0) {
        arr = new Float32Array(HEAP8.buffer, Module['BridgeClientGyro'].dataRef, 14);
        Module['BridgeClientGyro'].dataArr = arr;
    }

    // Gravity (3 floats)
    arr[0] = payload.gravity ? payload.gravity.x : 0;
    arr[1] = payload.gravity ? payload.gravity.y : 0;
    arr[2] = payload.gravity ? payload.gravity.z : 0;

    // Smooth Gravity (3 floats)
    arr[3] = payload.smoothGravity ? payload.smoothGravity.x : 0;
    arr[4] = payload.smoothGravity ? payload.smoothGravity.y : 0;
    arr[5] = payload.smoothGravity ? payload.smoothGravity.z : 0;

    // Orientation quaternion (4 floats)
    arr[6] = payload.orientation ? payload.orientation.x : 0;
    arr[7] = payload.orientation ? payload.orientation.y : 0;
    arr[8] = payload.orientation ? payload.orientation.z : 0;
    arr[9] = payload.orientation ? payload.orientation.w : 1;

    // Flags (as floats for simplicity)
    arr[10] = payload.gyroEnabled ? 1 : 0;
    arr[11] = payload.allowMovement ? 1 : 0;

    // Reserved
    arr[12] = 0;
    arr[13] = 0;
};
