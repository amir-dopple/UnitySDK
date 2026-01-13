using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Dopple.InputSDK.BridgeClient
{
    /// <summary>
    /// Singleton manager for BridgeClient haptics.
    /// Provides variable intensity haptics via parent Unity app's HapticsBridge.
    ///
    /// This component enables WebGL games running inside a native app's WebView
    /// to trigger haptic feedback through the parent app.
    /// </summary>
    [DisallowMultipleComponent]
    public class BridgeClientHapticsManager : MonoBehaviour
    {
        #region DllImport

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int BridgeClientHaptics_IsAvailable();

        [DllImport("__Internal")]
        private static extern void BridgeClientHaptics_CheckStatus();

        [DllImport("__Internal")]
        private static extern int BridgeClientHaptics_IsStatusChecked();

        [DllImport("__Internal")]
        private static extern int BridgeClientHaptics_IsHapticsReady();

        [DllImport("__Internal")]
        private static extern void BridgeClientHaptics_TriggerOne(int intensityPercent);

        [DllImport("__Internal")]
        private static extern void BridgeClientHaptics_PlayCurve(string keysJson, int strengthPercent, int useUnscaledTime);

        [DllImport("__Internal")]
        private static extern void BridgeClientHaptics_Stop();
#endif

        #endregion

        #region Singleton

        private static BridgeClientHapticsManager m_Instance;
        public static BridgeClientHapticsManager Instance => m_Instance;

        #endregion

        #region State

        [Header("Settings")]
        [SerializeField] private bool autoCheckStatus = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        /// <summary>
        /// Whether BridgeClient.HapticsBridge exists in the environment.
        /// </summary>
        public bool IsBridgeAvailable { get; private set; }

        /// <summary>
        /// Whether haptics status has been checked with parent app.
        /// </summary>
        public bool IsStatusChecked { get; private set; }

        /// <summary>
        /// Whether haptics are actually available (parent app has CurveVibrator).
        /// </summary>
        public bool IsHapticsReady { get; private set; }

        /// <summary>
        /// Whether haptics can be used (bridge available AND haptics ready).
        /// </summary>
        public bool CanUseHaptics => IsBridgeAvailable && IsHapticsReady;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (m_Instance != null && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            m_Instance = this;

            CheckBridgeAvailability();
        }

        private void Start()
        {
            if (autoCheckStatus && IsBridgeAvailable)
            {
                CheckHapticsStatus();
            }
        }

        private void Update()
        {
            // Poll for status check completion
            if (IsBridgeAvailable && !IsStatusChecked)
            {
                UpdateStatusCheck();
            }
        }

        private void OnDestroy()
        {
            if (m_Instance == this)
            {
                m_Instance = null;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Check if BridgeClient.HapticsBridge is available.
        /// </summary>
        public void CheckBridgeAvailability()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IsBridgeAvailable = BridgeClientHaptics_IsAvailable() != 0;

            if (showDebugInfo)
            {
                Debug.Log($"[BridgeClientHapticsManager] Bridge availability: {IsBridgeAvailable}");
            }
#else
            IsBridgeAvailable = false;

            if (showDebugInfo)
            {
                Debug.Log("[BridgeClientHapticsManager] Not WebGL platform - unavailable");
            }
#endif
        }

        /// <summary>
        /// Check haptics status with parent Unity app.
        /// This is async - poll IsStatusChecked and IsHapticsReady for results.
        /// </summary>
        public void CheckHapticsStatus()
        {
            if (!IsBridgeAvailable)
            {
                IsStatusChecked = true;
                IsHapticsReady = false;
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            BridgeClientHaptics_CheckStatus();

            if (showDebugInfo)
            {
                Debug.Log("[BridgeClientHapticsManager] Checking haptics status with parent app...");
            }
#endif
        }

        private void UpdateStatusCheck()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (BridgeClientHaptics_IsStatusChecked() != 0)
            {
                IsStatusChecked = true;
                IsHapticsReady = BridgeClientHaptics_IsHapticsReady() != 0;

                if (showDebugInfo)
                {
                    Debug.Log($"[BridgeClientHapticsManager] Status check complete. Haptics ready: {IsHapticsReady}");
                }
            }
#endif
        }

        #endregion

        #region Public API

        /// <summary>
        /// Trigger a single vibration pulse with variable intensity.
        /// </summary>
        /// <param name="intensity">Intensity from 0 to 1</param>
        public void TriggerOne(float intensity)
        {
            if (!CanUseHaptics)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BridgeClientHapticsManager] TriggerOne: Haptics not available");
                }
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            int intensityPercent = Mathf.RoundToInt(Mathf.Clamp01(intensity) * 100);
            BridgeClientHaptics_TriggerOne(intensityPercent);

            if (showDebugInfo)
            {
                Debug.Log($"[BridgeClientHapticsManager] TriggerOne: intensity={intensity:F2}");
            }
#endif
        }

        /// <summary>
        /// Play a haptic curve with keyframes.
        /// </summary>
        /// <param name="keyframes">Array of keyframes (time, value pairs)</param>
        /// <param name="strength">Multiplier for intensity (default 1)</param>
        /// <param name="useUnscaledTime">Use realtime timing (default false)</param>
        public void PlayCurve(HapticKeyframe[] keyframes, float strength = 1f, bool useUnscaledTime = false)
        {
            if (!CanUseHaptics)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BridgeClientHapticsManager] PlayCurve: Haptics not available");
                }
                return;
            }

            if (keyframes == null || keyframes.Length < 2)
            {
                Debug.LogWarning("[BridgeClientHapticsManager] PlayCurve requires at least 2 keyframes");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            string keysJson = KeyframesToJson(keyframes);
            int strengthPercent = Mathf.RoundToInt(Mathf.Clamp(strength, 0f, 10f) * 100);
            int unscaled = useUnscaledTime ? 1 : 0;

            BridgeClientHaptics_PlayCurve(keysJson, strengthPercent, unscaled);

            if (showDebugInfo)
            {
                Debug.Log($"[BridgeClientHapticsManager] PlayCurve: {keyframes.Length} keyframes, strength={strength:F2}");
            }
#endif
        }

        /// <summary>
        /// Play a haptic curve from an AnimationCurve.
        /// </summary>
        /// <param name="curve">Unity AnimationCurve to convert</param>
        /// <param name="duration">Duration in seconds</param>
        /// <param name="strength">Multiplier for intensity (default 1)</param>
        /// <param name="useUnscaledTime">Use realtime timing (default false)</param>
        public void PlayCurve(AnimationCurve curve, float duration, float strength = 1f, bool useUnscaledTime = false)
        {
            if (curve == null || curve.length < 2)
            {
                Debug.LogWarning("[BridgeClientHapticsManager] PlayCurve requires a valid AnimationCurve");
                return;
            }

            // Convert AnimationCurve to HapticKeyframes
            var keyframes = new HapticKeyframe[curve.length];
            for (int i = 0; i < curve.length; i++)
            {
                var key = curve[i];
                // Scale time from normalized (0-1) to actual duration
                float time = key.time * duration;
                float value = Mathf.Clamp01(key.value);

                keyframes[i] = new HapticKeyframe(time, value, key.inTangent, key.outTangent);
            }

            PlayCurve(keyframes, strength, useUnscaledTime);
        }

        /// <summary>
        /// Play a simple on-then-off haptic pulse.
        /// </summary>
        /// <param name="durationSeconds">Duration of the pulse in seconds</param>
        /// <param name="intensity">Intensity from 0 to 1</param>
        public void PlayPulse(float durationSeconds, float intensity = 1f)
        {
            var keyframes = new HapticKeyframe[]
            {
                new HapticKeyframe(0f, intensity),
                new HapticKeyframe(durationSeconds, 0f)
            };

            PlayCurve(keyframes);
        }

        /// <summary>
        /// Play a ramp-up-then-down haptic effect.
        /// </summary>
        /// <param name="durationSeconds">Total duration in seconds</param>
        /// <param name="peakIntensity">Peak intensity from 0 to 1</param>
        public void PlayRamp(float durationSeconds, float peakIntensity = 1f)
        {
            float halfDuration = durationSeconds / 2f;
            var keyframes = new HapticKeyframe[]
            {
                new HapticKeyframe(0f, 0f),
                new HapticKeyframe(halfDuration, peakIntensity),
                new HapticKeyframe(durationSeconds, 0f)
            };

            PlayCurve(keyframes);
        }

        /// <summary>
        /// Stop any in-progress haptics.
        /// </summary>
        public void Stop()
        {
            if (!IsBridgeAvailable)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            BridgeClientHaptics_Stop();

            if (showDebugInfo)
            {
                Debug.Log("[BridgeClientHapticsManager] Stop");
            }
#endif
        }

        #endregion

        #region Helpers

        private string KeyframesToJson(HapticKeyframe[] keyframes)
        {
            // Build JSON manually to avoid JsonUtility issues with nullable fields
            var parts = new List<string>();

            foreach (var kf in keyframes)
            {
                var kvPairs = new List<string>
                {
                    $"\"time\":{kf.time.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    $"\"value\":{kf.value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                };

                if (kf.inTangent.HasValue)
                {
                    kvPairs.Add($"\"inTangent\":{kf.inTangent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                if (kf.outTangent.HasValue)
                {
                    kvPairs.Add($"\"outTangent\":{kf.outTangent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                parts.Add("{" + string.Join(",", kvPairs) + "}");
            }

            return "[" + string.Join(",", parts) + "]";
        }

        #endregion
    }
}
