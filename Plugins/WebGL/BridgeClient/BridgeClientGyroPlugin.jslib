// BridgeClient Gyro Plugin - JavaScript Library
// Exports functions callable from C# via DllImport
// Uses shared memory for zero-copy data transfer

mergeInto(LibraryManager.library, {
    // Check if BridgeClient.GyroDataBridge is available
    BridgeClientGyro_IsAvailable: function() {
        return Module['BridgeClientGyro'].checkAvailability() ? 1 : 0;
    },

    // Start listening to gyro events at specified frequency
    // Returns immediately, actual start is async
    BridgeClientGyro_Start: function(hz) {
        Module['BridgeClientGyro'].start(hz);
    },

    // Stop listening to gyro events
    BridgeClientGyro_Stop: function() {
        Module['BridgeClientGyro'].stop();
    },

    // Register shared memory pointer for gyro data
    // dataPtr points to BridgeClientGyroData struct (14 floats = 56 bytes)
    BridgeClientGyro_RegisterDataPointer: function(dataPtr) {
        Module['BridgeClientGyro'].dataRef = dataPtr;
        Module['BridgeClientGyro'].dataArr = new Float32Array(HEAP8.buffer, dataPtr, 14);
    },

    // Check if currently listening
    BridgeClientGyro_IsListening: function() {
        return Module['BridgeClientGyro'].isListening ? 1 : 0;
    }
});
