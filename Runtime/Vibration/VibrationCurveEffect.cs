using System.Collections.Generic;
using UnityEngine;

namespace Dopple.InputSDK.Vibration
{
    /// <summary>
    /// ScriptableObject defining a vibration pattern using an AnimationCurve.
    /// The curve maps normalized time (0-1) to intensity (0-1).
    ///
    /// Create assets via: Create > Dopple > Vibration Curve Effect
    /// </summary>
    [CreateAssetMenu(fileName = "VibrationCurve_", menuName = "Dopple/Vibration Curve Effect", order = 10)]
    public class VibrationCurveEffect : ScriptableObject
    {
        [Header("Curve Configuration")]
        [Tooltip("Vibration intensity over time. X = normalized time (0-1), Y = intensity (0-1)")]
        public AnimationCurve intensityCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1f),
            new Keyframe(1f, 0f)
        );

        [Header("Timing")]
        [Tooltip("Total duration of the vibration effect in seconds")]
        [Min(0.02f)]
        public float duration = 0.2f;

        [Tooltip("How many times per second to sample the curve (20-100 recommended)")]
        [Range(20, 100)]
        public int sampleRate = 50;

        [Header("Amplitude Remapping")]
        [Tooltip("Minimum amplitude output (0-255 on Android)")]
        [Range(0, 255)]
        public int minAmplitude = 0;

        [Tooltip("Maximum amplitude output (0-255 on Android)")]
        [Range(0, 255)]
        public int maxAmplitude = 255;

        [Header("Fallback for Non-Amplitude Devices")]
        [Tooltip("Intensity threshold for on/off vibration on devices without amplitude control")]
        [Range(0f, 1f)]
        public float onOffThreshold = 0.3f;

        [Header("Metadata")]
        [Tooltip("Optional description for editor reference")]
        [TextArea(2, 4)]
        public string description;

        /// <summary>
        /// Get the number of samples based on duration and sample rate.
        /// </summary>
        public int SampleCount => Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));

        /// <summary>
        /// Get the time interval between samples in milliseconds.
        /// </summary>
        public int SampleIntervalMs => Mathf.Max(10, Mathf.RoundToInt(1000f / sampleRate));

        /// <summary>
        /// Sample the curve at a normalized time (0-1) and return Android amplitude (0-255).
        /// </summary>
        public int SampleAmplitude(float normalizedTime)
        {
            float intensity = Mathf.Clamp01(intensityCurve.Evaluate(normalizedTime));
            return Mathf.RoundToInt(Mathf.Lerp(minAmplitude, maxAmplitude, intensity));
        }

        /// <summary>
        /// Sample the curve at a normalized time for devices without amplitude control.
        /// Returns true if vibrator should be on.
        /// </summary>
        public bool SampleOnOff(float normalizedTime)
        {
            float intensity = Mathf.Clamp01(intensityCurve.Evaluate(normalizedTime));
            return intensity >= onOffThreshold;
        }

        /// <summary>
        /// Generate the full waveform arrays for Android VibrationEffect.
        /// </summary>
        /// <param name="timings">Output: array of timing durations in milliseconds</param>
        /// <param name="amplitudes">Output: array of amplitudes (0-255)</param>
        public void GenerateWaveform(out long[] timings, out int[] amplitudes)
        {
            int sampleCount = SampleCount;
            int intervalMs = SampleIntervalMs;

            timings = new long[sampleCount];
            amplitudes = new int[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float normalizedTime = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                timings[i] = i == 0 ? 0 : intervalMs;
                amplitudes[i] = SampleAmplitude(normalizedTime);
            }
        }

        /// <summary>
        /// Generate on/off pattern for devices without amplitude control.
        /// Pattern format: [off duration, on duration, off duration, on duration, ...]
        /// </summary>
        /// <param name="timings">Output: array of timing durations in milliseconds</param>
        public void GenerateOnOffPattern(out long[] timings)
        {
            int sampleCount = SampleCount;
            int intervalMs = SampleIntervalMs;

            var timingsList = new List<long>();

            bool currentState = false;
            long currentDuration = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                float normalizedTime = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                bool shouldBeOn = SampleOnOff(normalizedTime);

                if (i == 0)
                {
                    currentState = shouldBeOn;
                    if (!shouldBeOn)
                    {
                        currentDuration = intervalMs;
                    }
                    else
                    {
                        timingsList.Add(0);
                        currentDuration = intervalMs;
                    }
                    continue;
                }

                if (shouldBeOn == currentState)
                {
                    currentDuration += intervalMs;
                }
                else
                {
                    timingsList.Add(currentDuration);
                    currentState = shouldBeOn;
                    currentDuration = intervalMs;
                }
            }

            if (currentDuration > 0)
            {
                timingsList.Add(currentDuration);
            }

            if (timingsList.Count < 2)
            {
                long totalDuration = (long)(duration * 1000);
                timingsList.Clear();
                timingsList.Add(0);
                timingsList.Add(totalDuration);
            }

            timings = timingsList.ToArray();
        }

        private void OnValidate()
        {
            if (minAmplitude > maxAmplitude)
            {
                minAmplitude = maxAmplitude;
            }

            duration = Mathf.Max(0.02f, duration);
        }
    }
}
