using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dopple.InputSDK
{
    /// <summary>
    /// Input controller that outputs a rotation angle (0-360 degrees).
    /// Used for games where device tilt controls a rotating object (like Breakout paddle).
    ///
    /// OUTPUT: CurrentRotationAngle (float, 0-360)
    ///
    /// Z-AXIS ROTATION EXTRACTION:
    /// 1. Project gravity vector onto XY plane (removes forward/back tilt component)
    /// 2. Calculate rotation from Vector3.down to projected gravity
    /// 3. Extract Z euler angle from quaternion
    /// 4. Apply sensitivity, inversion, and offset
    ///
    /// COORDINATE MAPPING:
    /// - Device upright: angle = 0 (or calibrated offset)
    /// - Device tilted right: angle increases
    /// - Device tilted left: angle decreases (wraps at 360)
    ///
    /// Example usage:
    /// float angle = RotationInputController.Instance.CurrentRotationAngle;
    /// paddle.rotation = Quaternion.Euler(0, 0, angle);
    /// </summary>
    public class RotationInputController : InputControllerBase
    {
        #region Singleton

        public static RotationInputController Instance { get; private set; }

        #endregion

        #region Additional Configuration

        [Header("Rotation Settings")]
        [Tooltip("Offset added to the rotation angle (for calibration)")]
        [SerializeField] private float gyroRotationOffset = 0f;

        [Tooltip("Invert gyro movement direction specifically")]
        [SerializeField] private bool invertGyro = false;

        [Header("Mouse Settings")]
        [Tooltip("Sensitivity for mouse delta input")]
        [SerializeField] private float mouseSensitivity = 0.5f;

        [Tooltip("Sensitivity for scroll wheel input")]
        [SerializeField] private float scrollSensitivity = 30f;

        #endregion

        #region Output

        /// <summary>
        /// Current rotation angle in degrees (0-360).
        /// This is the main output - use this to control your rotating object.
        /// </summary>
        public float CurrentRotationAngle { get; private set; }

        /// <summary>
        /// Rotation delta since last frame (in degrees).
        /// Positive = clockwise, negative = counter-clockwise.
        /// </summary>
        public float RotationDelta { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when rotation angle changes.
        /// </summary>
        public event Action<float> OnRotationChanged;

        #endregion

        #region Internal State

        private float accumulatedAngle = 0f;
        private float previousAngle = 0f;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            base.Awake();
        }

        protected override void Update()
        {
            previousAngle = CurrentRotationAngle;
            base.Update();
            RotationDelta = Mathf.DeltaAngle(previousAngle, CurrentRotationAngle);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnDestroy();
        }

        #endregion

        #region Input Processing

        /// <summary>
        /// Process gravity vector to extract Z-axis rotation.
        /// </summary>
        protected override void ProcessGravityInput(Vector3 gravity)
        {
            // Project gravity onto XY plane to get Z rotation
            // This removes the forward/back tilt component, leaving only left/right tilt
            Vector3 projectedGravity = Vector3.ProjectOnPlane(gravity, Vector3.forward).normalized;

            Quaternion gyroRotation;
            if (projectedGravity.sqrMagnitude > 0.01f)
            {
                // Calculate rotation from "down" to projected gravity
                gyroRotation = Quaternion.FromToRotation(Vector3.down, projectedGravity);
            }
            else
            {
                gyroRotation = Quaternion.identity;
            }

            // Extract Z euler angle
            Vector3 gyroEuler = gyroRotation.eulerAngles;
            float zRotation = gyroEuler.z * gyroSensitivity;

            // Apply gyro-specific inversion
            if (invertGyro)
            {
                zRotation = -zRotation;
            }

            // Apply general inversion (if both are on, they cancel out)
            if (invertInput)
            {
                zRotation = -zRotation;
            }

            // Apply offset and wrap to 0-360
            zRotation += gyroRotationOffset;
            CurrentRotationAngle = Mathf.Repeat(zRotation, 360f);
            OnRotationChanged?.Invoke(CurrentRotationAngle);
        }

        protected override void UpdateMousePositionInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Map mouse X position (0 to Screen.width) to angle (0-360)
            float normalizedX = mouse.position.ReadValue().x / Screen.width;
            float rawAngle = normalizedX * 360f * mouseSensitivity;

            if (invertInput)
            {
                rawAngle = 360f - rawAngle;
            }

            CurrentRotationAngle = Mathf.Repeat(rawAngle, 360f);
            OnRotationChanged?.Invoke(CurrentRotationAngle);
        }

        protected override void UpdateMouseDeltaInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Accumulate mouse X movement
            float mouseDelta = mouse.delta.ReadValue().x * mouseSensitivity * 0.1f;

            if (invertInput)
            {
                mouseDelta = -mouseDelta;
            }

            accumulatedAngle += mouseDelta;
            CurrentRotationAngle = Mathf.Repeat(accumulatedAngle, 360f);
            OnRotationChanged?.Invoke(CurrentRotationAngle);
        }

        protected override void UpdateScrollInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Accumulate scroll wheel movement
            float scrollDelta = mouse.scroll.ReadValue().y * scrollSensitivity * 0.01f;

            if (invertInput)
            {
                scrollDelta = -scrollDelta;
            }

            accumulatedAngle += scrollDelta;
            CurrentRotationAngle = Mathf.Repeat(accumulatedAngle, 360f);
            OnRotationChanged?.Invoke(CurrentRotationAngle);
        }

        #endregion

        #region Calibration

        /// <summary>
        /// Calibrate gyro to current position (sets offset so current position = 0).
        /// </summary>
        public void CalibrateGyro()
        {
            CalibrateGyroToAngle(0f);
        }

        /// <summary>
        /// Calibrate gyro so current gyro reading maps to the specified target angle.
        /// Use this to prevent paddle jumping when starting/resuming the game.
        /// </summary>
        /// <param name="targetAngle">The angle the paddle should show after calibration</param>
        public void CalibrateGyroToAngle(float targetAngle)
        {
            float rawGyroZ = GetRawGyroZRotation();

            // Calculate offset: targetAngle = rawGyroZ * sensitivity + offset
            float effectiveRaw = rawGyroZ * gyroSensitivity;
            if (invertGyro) effectiveRaw = -effectiveRaw;
            if (invertInput) effectiveRaw = -effectiveRaw;

            gyroRotationOffset = targetAngle - effectiveRaw;

            if (showDebugInfo)
            {
                Debug.Log($"[RotationInputController] Gyro calibrated to {targetAngle:F1}, raw: {rawGyroZ:F1}, offset: {gyroRotationOffset:F1}");
            }
        }

        /// <summary>
        /// Get the raw gyro Z rotation (before sensitivity/inversion/offset).
        /// </summary>
        private float GetRawGyroZRotation()
        {
            Quaternion gyroRotation = Quaternion.identity;

            if (externalGyroProvider != null && externalGyroProvider.IsAvailable)
            {
                gyroRotation = externalGyroProvider.GetRotatingObjectRotation();
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            else if (bridgeClientGyroEnabled)
            {
                Vector3 gravity = BridgeClient.BridgeClientGyroManager.gyroData.Gravity;
                Vector3 projectedGravity = Vector3.ProjectOnPlane(gravity, Vector3.forward).normalized;
                if (projectedGravity.sqrMagnitude > 0.01f)
                {
                    gyroRotation = Quaternion.FromToRotation(Vector3.down, projectedGravity);
                }
            }
#endif
            else if (gyroEnabled && gravitySensor != null)
            {
                Vector3 gravity = gravitySensor.gravity.ReadValue();
                Vector3 projectedGravity = Vector3.ProjectOnPlane(gravity, Vector3.forward).normalized;
                if (projectedGravity.sqrMagnitude > 0.01f)
                {
                    gyroRotation = Quaternion.FromToRotation(Vector3.down, projectedGravity);
                }
            }

            return gyroRotation.eulerAngles.z;
        }

        /// <summary>
        /// Reset the accumulated angle for delta-based inputs.
        /// </summary>
        public void ResetAccumulatedAngle(float angle = 0f)
        {
            accumulatedAngle = angle;
            CurrentRotationAngle = Mathf.Repeat(angle, 360f);
        }

        /// <summary>
        /// Set the gyro rotation offset for calibration.
        /// </summary>
        public void SetGyroOffset(float offset)
        {
            gyroRotationOffset = offset;
        }

        /// <summary>
        /// Set the gyro sensitivity multiplier.
        /// </summary>
        public void SetGyroSensitivity(float sensitivity)
        {
            gyroSensitivity = sensitivity;
        }

        /// <summary>
        /// Set whether gyro input should be inverted.
        /// </summary>
        public void SetInvertGyro(bool invert)
        {
            invertGyro = invert;
        }

        /// <summary>
        /// Get whether gyro input is inverted.
        /// </summary>
        public bool IsGyroInverted => invertGyro;

        #endregion
    }
}
