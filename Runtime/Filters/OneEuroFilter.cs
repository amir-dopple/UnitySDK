using UnityEngine;

namespace Dopple.InputSDK.Filters
{
    /// <summary>
    /// One Euro Filter - A speed-adaptive low-pass filter for noisy signal smoothing.
    ///
    /// Based on the algorithm by Gery Casiez, Nicolas Roussel, and Daniel Vogel (CHI 2012).
    /// Reference: http://cristal.univ-lille.fr/~casiez/1euro/
    ///
    /// The filter adapts its cutoff frequency based on signal speed:
    /// - At low speeds: uses low cutoff to reduce jitter
    /// - At high speeds: uses high cutoff to reduce lag
    /// </summary>
    public class OneEuroFilter
    {
        private float minCutoff;
        private float beta;
        private float dCutoff;

        private LowPassFilter xFilter;
        private LowPassFilter dxFilter;
        private bool initialized;

        /// <summary>
        /// Create a new One Euro Filter.
        /// </summary>
        /// <param name="minCutoff">Minimum cutoff frequency. Lower = less jitter, more lag. Default: 1.0</param>
        /// <param name="beta">Speed coefficient. Higher = less lag at fast movements. Default: 0.5</param>
        /// <param name="dCutoff">Derivative cutoff frequency. Usually left at default. Default: 1.0</param>
        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.5f, float dCutoff = 1.0f)
        {
            this.minCutoff = minCutoff;
            this.beta = beta;
            this.dCutoff = dCutoff;

            xFilter = new LowPassFilter();
            dxFilter = new LowPassFilter();
            initialized = false;
        }

        /// <summary>
        /// Update filter parameters at runtime.
        /// </summary>
        public void UpdateParameters(float minCutoff, float beta, float dCutoff = 1.0f)
        {
            this.minCutoff = minCutoff;
            this.beta = beta;
            this.dCutoff = dCutoff;
        }

        /// <summary>
        /// Reset the filter state.
        /// </summary>
        public void Reset()
        {
            initialized = false;
            xFilter.Reset();
            dxFilter.Reset();
        }

        /// <summary>
        /// Filter a value.
        /// </summary>
        /// <param name="x">The noisy input value</param>
        /// <param name="deltaTime">Time since last sample (Time.deltaTime)</param>
        /// <returns>Filtered value</returns>
        public float Filter(float x, float deltaTime)
        {
            if (deltaTime <= 0) deltaTime = 0.016f; // Default to ~60fps

            float rate = 1.0f / deltaTime;

            if (!initialized)
            {
                initialized = true;
                xFilter.SetValue(x);
                dxFilter.SetValue(0);
                return x;
            }

            // Estimate derivative
            float dx = (x - xFilter.Value) * rate;
            float edx = dxFilter.Filter(dx, Alpha(rate, dCutoff));

            // Adaptive cutoff based on derivative (speed)
            float cutoff = minCutoff + beta * Mathf.Abs(edx);

            return xFilter.Filter(x, Alpha(rate, cutoff));
        }

        private static float Alpha(float rate, float cutoff)
        {
            float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
            float te = 1.0f / rate;
            return 1.0f / (1.0f + tau / te);
        }

        private class LowPassFilter
        {
            private float y;
            private bool initialized;

            public float Value => y;

            public void SetValue(float value)
            {
                y = value;
                initialized = true;
            }

            public void Reset()
            {
                initialized = false;
            }

            public float Filter(float x, float alpha)
            {
                if (!initialized)
                {
                    initialized = true;
                    y = x;
                }
                else
                {
                    y = alpha * x + (1.0f - alpha) * y;
                }
                return y;
            }
        }
    }

    /// <summary>
    /// One Euro Filter for Vector2 values.
    /// Applies independent filtering to each component.
    /// </summary>
    public class OneEuroFilterVector2
    {
        private OneEuroFilter xFilter;
        private OneEuroFilter yFilter;

        /// <summary>
        /// Create a new One Euro Filter for Vector2.
        /// </summary>
        public OneEuroFilterVector2(float minCutoff = 1.0f, float beta = 0.5f, float dCutoff = 1.0f)
        {
            xFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
            yFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
        }

        /// <summary>
        /// Update filter parameters at runtime.
        /// </summary>
        public void UpdateParameters(float minCutoff, float beta, float dCutoff = 1.0f)
        {
            xFilter.UpdateParameters(minCutoff, beta, dCutoff);
            yFilter.UpdateParameters(minCutoff, beta, dCutoff);
        }

        /// <summary>
        /// Reset the filter state.
        /// </summary>
        public void Reset()
        {
            xFilter.Reset();
            yFilter.Reset();
        }

        /// <summary>
        /// Filter a Vector2 value.
        /// </summary>
        public Vector2 Filter(Vector2 v, float deltaTime)
        {
            return new Vector2(
                xFilter.Filter(v.x, deltaTime),
                yFilter.Filter(v.y, deltaTime)
            );
        }
    }

    /// <summary>
    /// One Euro Filter for Vector3 values.
    /// Applies independent filtering to each component.
    /// </summary>
    public class OneEuroFilterVector3
    {
        private OneEuroFilter xFilter;
        private OneEuroFilter yFilter;
        private OneEuroFilter zFilter;

        /// <summary>
        /// Create a new One Euro Filter for Vector3.
        /// </summary>
        public OneEuroFilterVector3(float minCutoff = 1.0f, float beta = 0.5f, float dCutoff = 1.0f)
        {
            xFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
            yFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
            zFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
        }

        /// <summary>
        /// Update filter parameters at runtime.
        /// </summary>
        public void UpdateParameters(float minCutoff, float beta, float dCutoff = 1.0f)
        {
            xFilter.UpdateParameters(minCutoff, beta, dCutoff);
            yFilter.UpdateParameters(minCutoff, beta, dCutoff);
            zFilter.UpdateParameters(minCutoff, beta, dCutoff);
        }

        /// <summary>
        /// Reset the filter state.
        /// </summary>
        public void Reset()
        {
            xFilter.Reset();
            yFilter.Reset();
            zFilter.Reset();
        }

        /// <summary>
        /// Filter a Vector3 value.
        /// </summary>
        public Vector3 Filter(Vector3 v, float deltaTime)
        {
            return new Vector3(
                xFilter.Filter(v.x, deltaTime),
                yFilter.Filter(v.y, deltaTime),
                zFilter.Filter(v.z, deltaTime)
            );
        }
    }
}
