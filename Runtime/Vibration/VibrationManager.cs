using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Dopple.InputSDK.BridgeClient;

namespace Dopple.InputSDK.Vibration
{
    /// <summary>
    /// Singleton manager for vibration with AnimationCurve support.
    /// Supports Android (native) and WebGL (browser Vibration API + BridgeClient).
    ///
    /// Usage:
    /// VibrationManager.Instance.PlayCurve(myVibrationCurve);
    /// VibrationManager.Instance.Vibrate(100); // 100ms
    /// </summary>
    public class VibrationManager : MonoBehaviour
    {
        public static VibrationManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Enable/disable vibration globally")]
        [SerializeField] private bool vibrationEnabled = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebGL_Vibrate(int durationMs);

        [DllImport("__Internal")]
        private static extern void WebGL_VibratePattern(int[] pattern, int patternLength);

        [DllImport("__Internal")]
        private static extern void WebGL_StopVibration();

        [DllImport("__Internal")]
        private static extern int WebGL_HasVibration();

        private bool webglHasVibration;
        private BridgeClientHapticsManager bridgeClientHaptics;
        private bool bridgeClientHapticsAvailable;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaClass vibrationEffectClass;
        private AndroidJavaObject unityActivity;
        private int androidApiLevel;
        private bool hasAmplitudeControl;
        private bool isInitialized;
#endif

        public event Action OnVibrationStarted;
        public event Action OnVibrationStopped;

        public bool VibrationEnabled
        {
            get => vibrationEnabled;
            set => vibrationEnabled = value;
        }

        /// <summary>
        /// Whether the device supports amplitude control (variable intensity).
        /// WebGL with BridgeClient haptics supports variable intensity.
        /// Standard WebGL only supports on/off patterns.
        /// </summary>
        public bool HasAmplitudeControl
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return hasAmplitudeControl;
#elif UNITY_WEBGL && !UNITY_EDITOR
                return bridgeClientHapticsAvailable;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Whether vibration is available on this device/browser.
        /// </summary>
        public bool IsVibrationAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return isInitialized && vibrator != null;
#elif UNITY_WEBGL && !UNITY_EDITOR
                return webglHasVibration || bridgeClientHapticsAvailable;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Whether BridgeClient haptics are available (WebGL in WebView only).
        /// This provides variable intensity support.
        /// </summary>
        public bool IsBridgeClientHapticsAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return bridgeClientHapticsAvailable;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeVibrator();
        }

        private void InitializeVibrator()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroid();
#elif UNITY_WEBGL && !UNITY_EDITOR
            InitializeWebGL();
#else
            if (showDebugInfo)
            {
                Debug.Log("[VibrationManager] Running in Editor - vibration disabled");
            }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void InitializeWebGL()
        {
            try
            {
                webglHasVibration = WebGL_HasVibration() == 1;

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] WebGL Vibration API available: {webglHasVibration}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] WebGL initialization failed: {e.Message}");
                webglHasVibration = false;
            }

            // Initialize BridgeClient haptics (provides variable intensity on WebGL)
            InitializeBridgeClientHaptics();
        }

        private void InitializeBridgeClientHaptics()
        {
            bridgeClientHaptics = BridgeClientHapticsManager.Instance;

            if (bridgeClientHaptics == null)
            {
                bridgeClientHaptics = FindFirstObjectByType<BridgeClientHapticsManager>();
            }

            // Auto-create if not found
            if (bridgeClientHaptics == null)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[VibrationManager] BridgeClientHapticsManager not found - creating one automatically");
                }
                var hapticsGO = new GameObject("BridgeClientHapticsManager");
                bridgeClientHaptics = hapticsGO.AddComponent<BridgeClientHapticsManager>();
            }

            if (bridgeClientHaptics != null)
            {
                // Start checking for availability
                StartCoroutine(WaitForBridgeClientHapticsStatus());
            }
        }

        private System.Collections.IEnumerator WaitForBridgeClientHapticsStatus()
        {
            // Wait for BridgeClient haptics status check to complete
            float timeout = 2f;
            float elapsed = 0f;

            while (!bridgeClientHaptics.IsStatusChecked && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            bridgeClientHapticsAvailable = bridgeClientHaptics.CanUseHaptics;

            if (showDebugInfo)
            {
                Debug.Log($"[VibrationManager] BridgeClient Haptics available: {bridgeClientHapticsAvailable}");
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeAndroid()
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }

                using (AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    androidApiLevel = buildVersion.GetStatic<int>("SDK_INT");
                }

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] Android API level: {androidApiLevel}");
                }

                if (androidApiLevel >= 31)
                {
                    using (AndroidJavaObject vibratorManager = unityActivity.Call<AndroidJavaObject>(
                        "getSystemService", "vibrator_manager"))
                    {
                        if (vibratorManager != null)
                        {
                            vibrator = vibratorManager.Call<AndroidJavaObject>("getDefaultVibrator");
                        }
                    }

                    if (showDebugInfo)
                    {
                        Debug.Log("[VibrationManager] Using VibratorManager (API 31+)");
                    }
                }
                else
                {
                    vibrator = unityActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                    if (showDebugInfo)
                    {
                        Debug.Log("[VibrationManager] Using legacy VIBRATOR_SERVICE");
                    }
                }

                if (vibrator != null)
                {
                    bool hasVibrator = vibrator.Call<bool>("hasVibrator");

                    if (!hasVibrator)
                    {
                        Debug.LogWarning("[VibrationManager] Device does not have a vibrator");
                        vibrator = null;
                    }
                    else
                    {
                        if (androidApiLevel >= 26)
                        {
                            hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                            vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        }
                        else
                        {
                            hasAmplitudeControl = false;
                        }

                        isInitialized = true;

                        if (showDebugInfo)
                        {
                            Debug.Log($"[VibrationManager] Initialized. HasAmplitudeControl: {hasAmplitudeControl}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[VibrationManager] Failed to get vibrator service");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] Initialization failed: {e.Message}");
                isInitialized = false;
            }
        }
#endif

        /// <summary>
        /// Play a vibration curve effect.
        /// </summary>
        public void PlayCurve(VibrationCurveEffect effect)
        {
            if (effect == null)
            {
                Debug.LogWarning("[VibrationManager] Cannot play null effect");
                return;
            }

            if (!vibrationEnabled)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[VibrationManager] Vibration disabled, skipping");
                }
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            PlayCurveAndroid(effect);
#elif UNITY_WEBGL && !UNITY_EDITOR
            PlayCurveWebGL(effect);
#else
            if (showDebugInfo)
            {
                Debug.Log($"[VibrationManager] (Editor) Would play: {effect.name}, duration: {effect.duration}s, samples: {effect.SampleCount}");
            }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void PlayCurveWebGL(VibrationCurveEffect effect)
        {
            // Prefer BridgeClient haptics (variable intensity support)
            if (bridgeClientHapticsAvailable && bridgeClientHaptics != null)
            {
                try
                {
                    bridgeClientHaptics.PlayCurve(effect.intensityCurve, effect.duration);
                    OnVibrationStarted?.Invoke();

                    if (showDebugInfo)
                    {
                        Debug.Log($"[VibrationManager] BridgeClient playing curve: {effect.name}, duration: {effect.duration}s");
                    }
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VibrationManager] BridgeClient PlayCurve failed: {e.Message}");
                    // Fall through to standard WebGL
                }
            }

            // Fallback to standard WebGL Vibration API (on/off patterns only)
            if (!webglHasVibration)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[VibrationManager] WebGL vibration not available");
                }
                return;
            }

            try
            {
                // WebGL only supports on/off patterns, convert curve to pattern
                effect.GenerateOnOffPattern(out long[] longPattern);

                // Convert long[] to int[] for WebGL
                int[] pattern = new int[longPattern.Length];
                for (int i = 0; i < longPattern.Length; i++)
                {
                    pattern[i] = (int)longPattern[i];
                }

                WebGL_VibratePattern(pattern, pattern.Length);
                OnVibrationStarted?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] WebGL playing curve: {effect.name}, pattern length: {pattern.Length}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] WebGL PlayCurve failed: {e.Message}");
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PlayCurveAndroid(VibrationCurveEffect effect)
        {
            if (!isInitialized || vibrator == null)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[VibrationManager] Not initialized or no vibrator");
                }
                return;
            }

            try
            {
                if (androidApiLevel >= 26)
                {
                    PlayWaveformEffect(effect);
                }
                else
                {
                    PlayLegacyVibration(effect);
                }

                OnVibrationStarted?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] Playing curve: {effect.name}, duration: {effect.duration}s");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] PlayCurve failed: {e.Message}");
            }
        }
#endif

        /// <summary>
        /// Play a vibration curve inline without a ScriptableObject.
        /// </summary>
        public void PlayCurve(AnimationCurve curve, float duration, int sampleRate = 50)
        {
            if (curve == null)
            {
                Debug.LogWarning("[VibrationManager] Cannot play null curve");
                return;
            }

            if (!vibrationEnabled)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            PlayInlineCurveAndroid(curve, duration, sampleRate);
#elif UNITY_WEBGL && !UNITY_EDITOR
            PlayInlineCurveWebGL(curve, duration, sampleRate);
#else
            if (showDebugInfo)
            {
                Debug.Log($"[VibrationManager] (Editor) Would play inline curve, duration: {duration}s");
            }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void PlayInlineCurveWebGL(AnimationCurve curve, float duration, int sampleRate)
        {
            // Prefer BridgeClient haptics (variable intensity support)
            if (bridgeClientHapticsAvailable && bridgeClientHaptics != null)
            {
                try
                {
                    bridgeClientHaptics.PlayCurve(curve, duration);
                    OnVibrationStarted?.Invoke();

                    if (showDebugInfo)
                    {
                        Debug.Log($"[VibrationManager] BridgeClient playing inline curve, duration: {duration}s");
                    }
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VibrationManager] BridgeClient PlayCurve (inline) failed: {e.Message}");
                    // Fall through to standard WebGL
                }
            }

            // Fallback to standard WebGL Vibration API (on/off patterns only)
            if (!webglHasVibration)
            {
                return;
            }

            try
            {
                // Convert curve to on/off pattern for WebGL
                int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
                int intervalMs = Mathf.Max(10, Mathf.RoundToInt(1000f / sampleRate));
                float threshold = 0.3f;

                var patternList = new System.Collections.Generic.List<int>();
                bool isOn = false;
                int currentDuration = 0;

                for (int i = 0; i < sampleCount; i++)
                {
                    float normalizedTime = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                    float intensity = Mathf.Clamp01(curve.Evaluate(normalizedTime));
                    bool shouldBeOn = intensity >= threshold;

                    if (i == 0)
                    {
                        if (!shouldBeOn)
                        {
                            currentDuration = intervalMs;
                            isOn = false;
                        }
                        else
                        {
                            patternList.Add(0);
                            currentDuration = intervalMs;
                            isOn = true;
                        }
                        continue;
                    }

                    if (shouldBeOn == isOn)
                    {
                        currentDuration += intervalMs;
                    }
                    else
                    {
                        patternList.Add(currentDuration);
                        currentDuration = intervalMs;
                        isOn = shouldBeOn;
                    }
                }

                if (currentDuration > 0)
                {
                    patternList.Add(currentDuration);
                }

                if (patternList.Count < 2)
                {
                    patternList.Clear();
                    patternList.Add(0);
                    patternList.Add((int)(duration * 1000));
                }

                int[] pattern = patternList.ToArray();
                WebGL_VibratePattern(pattern, pattern.Length);
                OnVibrationStarted?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] WebGL playing inline curve, duration: {duration}s");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] WebGL PlayCurve (inline) failed: {e.Message}");
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PlayInlineCurveAndroid(AnimationCurve curve, float duration, int sampleRate)
        {
            if (!isInitialized || vibrator == null)
            {
                return;
            }

            try
            {
                int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
                int intervalMs = Mathf.Max(10, Mathf.RoundToInt(1000f / sampleRate));

                long[] timings = new long[sampleCount];
                int[] amplitudes = new int[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    float normalizedTime = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                    float intensity = Mathf.Clamp01(curve.Evaluate(normalizedTime));

                    timings[i] = i == 0 ? 0 : intervalMs;
                    amplitudes[i] = Mathf.RoundToInt(intensity * 255);
                }

                if (androidApiLevel >= 26)
                {
                    if (hasAmplitudeControl)
                    {
                        PlayWaveformWithAmplitude(timings, amplitudes);
                    }
                    else
                    {
                        PlayWaveformOnOff(timings, amplitudes, 0.3f);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", (long)(duration * 1000));
                }

                OnVibrationStarted?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] Playing inline curve, duration: {duration}s");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] PlayCurve (inline) failed: {e.Message}");
            }
        }
#endif

        /// <summary>
        /// Play a simple vibration for a duration (no curve).
        /// </summary>
        public void Vibrate(long durationMs, int amplitude = 255)
        {
            if (!vibrationEnabled)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(durationMs, amplitude);
#elif UNITY_WEBGL && !UNITY_EDITOR
            VibrateWebGL((int)durationMs, amplitude);
#else
            if (showDebugInfo)
            {
                Debug.Log($"[VibrationManager] (Editor) Would vibrate: {durationMs}ms, amplitude: {amplitude}");
            }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void VibrateWebGL(int durationMs, int amplitude = 255)
        {
            // Prefer BridgeClient haptics (variable intensity support)
            if (bridgeClientHapticsAvailable && bridgeClientHaptics != null)
            {
                try
                {
                    // Convert amplitude (0-255) to intensity (0-1)
                    float intensity = amplitude / 255f;

                    // Use PlayPulse for simple vibration
                    float durationSeconds = durationMs / 1000f;
                    bridgeClientHaptics.PlayPulse(durationSeconds, intensity);
                    OnVibrationStarted?.Invoke();

                    if (showDebugInfo)
                    {
                        Debug.Log($"[VibrationManager] BridgeClient vibrate: {durationMs}ms, intensity: {intensity:F2}");
                    }
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VibrationManager] BridgeClient Vibrate failed: {e.Message}");
                    // Fall through to standard WebGL
                }
            }

            // Fallback to standard WebGL Vibration API
            if (!webglHasVibration)
            {
                return;
            }

            try
            {
                WebGL_Vibrate(durationMs);
                OnVibrationStarted?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] WebGL vibrate: {durationMs}ms");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] WebGL Vibrate failed: {e.Message}");
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void VibrateAndroid(long durationMs, int amplitude)
        {
            if (!isInitialized || vibrator == null)
            {
                return;
            }

            try
            {
                if (androidApiLevel >= 26)
                {
                    amplitude = Mathf.Clamp(amplitude, 1, 255);

                    AndroidJavaObject effect;
                    if (hasAmplitudeControl)
                    {
                        effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", durationMs, amplitude);
                    }
                    else
                    {
                        effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", durationMs,
                            vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE"));
                    }

                    vibrator.Call("vibrate", effect);
                    effect.Dispose();
                }
                else
                {
                    vibrator.Call("vibrate", durationMs);
                }

                OnVibrationStarted?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log($"[VibrationManager] Vibrate: {durationMs}ms, amplitude: {amplitude}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] Vibrate failed: {e.Message}");
            }
        }
#endif

        /// <summary>
        /// Stop any ongoing vibration.
        /// </summary>
        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            StopAndroid();
#elif UNITY_WEBGL && !UNITY_EDITOR
            StopWebGL();
#else
            if (showDebugInfo)
            {
                Debug.Log("[VibrationManager] (Editor) Would stop vibration");
            }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void StopWebGL()
        {
            bool stopped = false;

            // Stop BridgeClient haptics
            if (bridgeClientHapticsAvailable && bridgeClientHaptics != null)
            {
                try
                {
                    bridgeClientHaptics.Stop();
                    stopped = true;

                    if (showDebugInfo)
                    {
                        Debug.Log("[VibrationManager] BridgeClient haptics stopped");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VibrationManager] BridgeClient Stop failed: {e.Message}");
                }
            }

            // Also stop standard WebGL vibration
            if (webglHasVibration)
            {
                try
                {
                    WebGL_StopVibration();
                    stopped = true;

                    if (showDebugInfo)
                    {
                        Debug.Log("[VibrationManager] WebGL vibration stopped");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VibrationManager] WebGL Stop failed: {e.Message}");
                }
            }

            if (stopped)
            {
                OnVibrationStopped?.Invoke();
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void StopAndroid()
        {
            if (!isInitialized || vibrator == null)
            {
                return;
            }

            try
            {
                vibrator.Call("cancel");
                OnVibrationStopped?.Invoke();

                if (showDebugInfo)
                {
                    Debug.Log("[VibrationManager] Vibration cancelled");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VibrationManager] Stop failed: {e.Message}");
            }
        }

        private void PlayWaveformEffect(VibrationCurveEffect effect)
        {
            effect.GenerateWaveform(out long[] timings, out int[] amplitudes);

            if (hasAmplitudeControl)
            {
                PlayWaveformWithAmplitude(timings, amplitudes);
            }
            else
            {
                PlayWaveformOnOff(timings, amplitudes, effect.onOffThreshold);
            }
        }

        private void PlayWaveformWithAmplitude(long[] timings, int[] amplitudes)
        {
            using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                    "createWaveform", timings, amplitudes, -1);

                vibrator.Call("vibrate", effect);
                effect.Dispose();
            }
        }

        private void PlayWaveformOnOff(long[] timings, int[] amplitudes, float threshold)
        {
            var patternList = new System.Collections.Generic.List<long>();

            int thresholdInt = Mathf.RoundToInt(threshold * 255);
            bool isOn = false;
            long currentDuration = 0;

            for (int i = 0; i < timings.Length; i++)
            {
                bool shouldBeOn = amplitudes[i] >= thresholdInt;
                long timing = timings[i];

                if (i == 0)
                {
                    if (!shouldBeOn)
                    {
                        currentDuration = timing > 0 ? timing : 20;
                        isOn = false;
                    }
                    else
                    {
                        patternList.Add(0);
                        currentDuration = timing > 0 ? timing : 20;
                        isOn = true;
                    }
                    continue;
                }

                if (shouldBeOn == isOn)
                {
                    currentDuration += timing;
                }
                else
                {
                    patternList.Add(currentDuration);
                    currentDuration = timing;
                    isOn = shouldBeOn;
                }
            }

            if (currentDuration > 0)
            {
                patternList.Add(currentDuration);
            }

            if (patternList.Count < 2)
            {
                long totalDuration = 0;
                foreach (var t in timings) totalDuration += t;
                if (totalDuration == 0) totalDuration = 100;
                patternList.Clear();
                patternList.Add(0);
                patternList.Add(totalDuration);
            }

            long[] pattern = patternList.ToArray();

            using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                    "createWaveform", pattern, -1);

                vibrator.Call("vibrate", effect);
                effect.Dispose();
            }
        }

        private void PlayLegacyVibration(VibrationCurveEffect effect)
        {
            effect.GenerateOnOffPattern(out long[] pattern);

            if (pattern.Length >= 2)
            {
                vibrator.Call("vibrate", pattern, -1);
            }
            else
            {
                vibrator.Call("vibrate", (long)(effect.duration * 1000));
            }
        }
#endif

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (vibrator != null)
            {
                try
                {
                    vibrator.Call("cancel");
                }
                catch { }
                vibrator.Dispose();
                vibrator = null;
            }

            if (vibrationEffectClass != null)
            {
                vibrationEffectClass.Dispose();
                vibrationEffectClass = null;
            }

            if (unityActivity != null)
            {
                unityActivity.Dispose();
                unityActivity = null;
            }
#endif

            OnVibrationStarted = null;
            OnVibrationStopped = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Stop();
            }
        }
    }
}
