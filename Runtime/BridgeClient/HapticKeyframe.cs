using System;

namespace Dopple.InputSDK.BridgeClient
{
    /// <summary>
    /// Keyframe for haptic curves, matching HapticsBridge API format.
    /// </summary>
    [Serializable]
    public struct HapticKeyframe
    {
        public float time;
        public float value;
        public float? inTangent;
        public float? outTangent;

        public HapticKeyframe(float time, float value)
        {
            this.time = time;
            this.value = value;
            this.inTangent = null;
            this.outTangent = null;
        }

        public HapticKeyframe(float time, float value, float inTangent, float outTangent)
        {
            this.time = time;
            this.value = value;
            this.inTangent = inTangent;
            this.outTangent = outTangent;
        }
    }
}
