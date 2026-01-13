// WebGL Vibration Plugin - JavaScript Library
// Provides access to the browser's Vibration API
// https://developer.mozilla.org/en-US/docs/Web/API/Vibration_API

mergeInto(LibraryManager.library, {
    // Check if the browser supports the Vibration API
    WebGL_HasVibration: function() {
        return (typeof navigator !== 'undefined' && 'vibrate' in navigator) ? 1 : 0;
    },

    // Vibrate for a specified duration in milliseconds
    WebGL_Vibrate: function(durationMs) {
        if (typeof navigator !== 'undefined' && navigator.vibrate) {
            try {
                navigator.vibrate(durationMs);
            } catch (e) {
                console.warn('[VibrationPlugin] Vibrate failed:', e);
            }
        }
    },

    // Vibrate with a pattern (alternating vibrate/pause durations)
    // patternPtr points to an int array, patternLength is the count
    WebGL_VibratePattern: function(patternPtr, patternLength) {
        if (typeof navigator !== 'undefined' && navigator.vibrate) {
            try {
                var pattern = [];
                for (var i = 0; i < patternLength; i++) {
                    pattern.push(HEAP32[(patternPtr >> 2) + i]);
                }
                navigator.vibrate(pattern);
            } catch (e) {
                console.warn('[VibrationPlugin] VibratePattern failed:', e);
            }
        }
    },

    // Stop any ongoing vibration
    WebGL_StopVibration: function() {
        if (typeof navigator !== 'undefined' && navigator.vibrate) {
            try {
                navigator.vibrate(0);
            } catch (e) {
                console.warn('[VibrationPlugin] StopVibration failed:', e);
            }
        }
    }
});
