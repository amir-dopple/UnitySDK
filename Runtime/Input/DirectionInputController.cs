using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dopple.InputSDK
{
    /// <summary>
    /// Input controller that outputs a 2D movement direction.
    /// Used for games where device tilt controls character movement.
    ///
    /// OUTPUT: MovementInput (Vector2, normalized direction)
    ///
    /// X/Z DIRECTION EXTRACTION:
    /// 1. Calibrate neutral position (stores referenceGravity when player holds device)
    /// 2. Calculate delta: deltaGravity = gravity - referenceGravity
    /// 3. Map delta to movement:
    ///    - X = deltaGravity.y (tilt left/right)
    ///    - Z = -deltaGravity.x (tilt forward/backward)
    /// 4. Apply deadzone, sensitivity, and inversion
    ///
    /// COORDINATE MAPPING:
    /// - Tilt device forward: MovementInput.y positive (forward movement)
    /// - Tilt device right: MovementInput.x positive (right movement)
    /// - Calibration stores "neutral" position when player holds device comfortably
    ///
    /// Example usage:
    /// Vector2 input = DirectionInputController.Instance.MovementInput;
    /// Vector3 movement = new Vector3(input.x, 0, input.y);
    /// character.Move(movement * speed * Time.deltaTime);
    /// </summary>
    public class DirectionInputController : InputControllerBase
    {
        #region Singleton

        public static DirectionInputController Instance { get; private set; }

        #endregion

        #region Additional Configuration

        [Header("Direction Settings")]
        [Tooltip("Deadzone for gyro tilt (values below this are treated as zero)")]
        [SerializeField] private float gyroDeadzone = 0.1f;

        [Tooltip("Invert X-axis gyro movement")]
        [SerializeField] private bool invertGyroX = false;

        [Tooltip("Invert Z-axis gyro movement")]
        [SerializeField] private bool invertGyroZ = false;

        [Header("Character Reference")]
        [Tooltip("Reference to the character transform for mouse direction calculation")]
        [SerializeField] private Transform characterTransform;

        [Header("Mouse Settings")]
        [Tooltip("Distance at which character stops moving toward mouse")]
        [SerializeField] private float mouseArrivalDistance = 0.5f;

        [Tooltip("Distance at which character moves at full speed toward mouse")]
        [SerializeField] private float mouseMaxDistance = 5f;

        [Tooltip("How fast the mouse direction smooths")]
        [Range(1f, 30f)]
        [SerializeField] private float mouseDirectionSmoothSpeed = 10f;

        #endregion

        #region Output

        /// <summary>
        /// Normalized movement direction (X/Z plane).
        /// X = left/right, Y = forward/backward.
        /// Convert to 3D: new Vector3(input.x, 0, input.y)
        /// </summary>
        public Vector2 MovementInput { get; private set; }

        /// <summary>
        /// Target world position (where cursor is pointing - for visualization).
        /// </summary>
        public Vector3 TargetWorldPosition { get; private set; }

        /// <summary>
        /// Whether we have a valid target position.
        /// </summary>
        public bool HasTargetPosition { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when jump is pressed.
        /// </summary>
        public static event Action OnJumpPressed;

        /// <summary>
        /// Event fired when jump is released.
        /// </summary>
        public static event Action OnJumpReleased;

        #endregion

        #region Internal State

        private Vector3 referenceGravity;
        private bool gyroCalibrated = false;
        private Camera mainCamera;
        private Vector2 smoothedMouseDirection;
        private Vector3 virtualCursorPosition;

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

        protected override void Start()
        {
            base.Start();
            mainCamera = Camera.main;

            // Initialize virtual cursor to character position
            if (characterTransform != null)
            {
                virtualCursorPosition = characterTransform.position;
                virtualCursorPosition.y = 0;
            }
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                OnJumpPressed = null;
                OnJumpReleased = null;
            }
            base.OnDestroy();
        }

        #endregion

        #region Input Processing

        /// <summary>
        /// Process gravity vector to extract X/Z movement direction.
        /// </summary>
        protected override void ProcessGravityInput(Vector3 gravity)
        {
            // Auto-calibrate on first valid gravity reading
            if (!gyroCalibrated && gravity.magnitude > 0.5f)
            {
                CalibrateGyro(gravity);
            }

            // Calculate delta from calibrated reference position
            Vector3 deltaGravity = gravity - referenceGravity;

            // Extract tilt from delta gravity
            // deltaGravity.y -> character X movement (tilt left/right)
            // deltaGravity.x -> character Z movement (tilt forward/backward), INVERTED
            Vector2 tilt = new Vector2(deltaGravity.y, -deltaGravity.x);

            // Apply deadzone (with smooth remapping, not hard cutoff)
            float tiltMag = tilt.magnitude;
            if (tiltMag < gyroDeadzone)
            {
                tilt = Vector2.zero;
            }
            else
            {
                // Remap from [deadzone, 1] to [0, 1] for smooth response
                float remappedMag = (tiltMag - gyroDeadzone) / (1f - gyroDeadzone);
                tilt = tilt.normalized * remappedMag;
            }

            // Apply sensitivity
            tilt *= gyroSensitivity;

            // Apply inversion
            if (invertGyroX) tilt.x = -tilt.x;
            if (invertGyroZ) tilt.y = -tilt.y;
            if (invertInput)
            {
                tilt.x = -tilt.x;
                tilt.y = -tilt.y;
            }

            // Clamp magnitude to 1
            MovementInput = Vector2.ClampMagnitude(tilt, 1f);

            // Update virtual cursor for visualization
            UpdateVirtualCursor(MovementInput);
        }

        /// <summary>
        /// Update virtual cursor position for visualization.
        /// </summary>
        private void UpdateVirtualCursor(Vector2 tilt)
        {
            if (characterTransform != null)
            {
                Vector3 offset = new Vector3(tilt.x, 0, tilt.y) * 2f;
                virtualCursorPosition = characterTransform.position + offset;
                virtualCursorPosition.y = 0;
                TargetWorldPosition = virtualCursorPosition;
                HasTargetPosition = tilt.magnitude > 0.01f;
            }
        }

        protected override void UpdateMousePositionInput()
        {
            var mouse = Mouse.current;
            if (mouse == null || mainCamera == null || characterTransform == null)
            {
                MovementInput = Vector2.zero;
                HasTargetPosition = false;
                return;
            }

            Vector2 mouseScreenPos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);
                TargetWorldPosition = worldPos;
                HasTargetPosition = true;

                Vector3 characterPos = characterTransform.position;
                Vector3 toTarget = worldPos - characterPos;
                toTarget.y = 0;

                float distanceToTarget = toTarget.magnitude;

                // Within arrival distance - smoothly decay to zero
                if (distanceToTarget < mouseArrivalDistance)
                {
                    smoothedMouseDirection = Vector2.Lerp(smoothedMouseDirection, Vector2.zero,
                        mouseDirectionSmoothSpeed * Time.deltaTime);
                    MovementInput = smoothedMouseDirection;
                    return;
                }

                // Calculate raw direction
                Vector2 rawDirection = new Vector2(toTarget.x, toTarget.z).normalized;

                // Smooth the direction
                smoothedMouseDirection = Vector2.Lerp(smoothedMouseDirection, rawDirection,
                    mouseDirectionSmoothSpeed * Time.deltaTime);

                // Scale speed by distance
                float speedScale = Mathf.InverseLerp(mouseArrivalDistance, mouseMaxDistance, distanceToTarget);
                speedScale = Mathf.Clamp01(speedScale);

                MovementInput = smoothedMouseDirection * speedScale;
            }
            else
            {
                MovementInput = Vector2.zero;
                HasTargetPosition = false;
            }
        }

        protected override void UpdateMouseDeltaInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                MovementInput = Vector2.zero;
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * 0.01f;

            if (invertInput)
            {
                delta = -delta;
            }

            // Use delta as direction input (clamped)
            MovementInput = Vector2.ClampMagnitude(delta * gyroSensitivity, 1f);
            HasTargetPosition = false;
        }

        protected override void UpdateScrollInput()
        {
            // Scroll wheel not meaningful for direction - use mouse delta instead
            UpdateMouseDeltaInput();
        }

        #endregion

        #region Calibration

        /// <summary>
        /// Calibrate current device position as neutral (no movement).
        /// Call this when the player is holding the device in their preferred position.
        /// </summary>
        public void CalibrateGyro()
        {
            if (gyroEnabled && gravitySensor != null)
            {
                CalibrateGyro(gravitySensor.gravity.ReadValue());
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            else if (bridgeClientGyroEnabled)
            {
                CalibrateGyro(BridgeClient.BridgeClientGyroManager.gyroData.Gravity);
            }
#endif
        }

        private void CalibrateGyro(Vector3 gravity)
        {
            referenceGravity = gravity;
            gyroCalibrated = true;
            smoothedMouseDirection = Vector2.zero;
            gravityFilter?.Reset();

            if (showDebugInfo)
            {
                Debug.Log($"[DirectionInputController] Gyro calibrated. Reference: {referenceGravity}");
            }
        }

        /// <summary>
        /// Reset calibration - will auto-calibrate on next valid reading.
        /// </summary>
        public void ResetCalibration()
        {
            gyroCalibrated = false;
            referenceGravity = Vector3.zero;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the character transform reference for mouse direction calculation.
        /// </summary>
        public void SetCharacterTransform(Transform character)
        {
            characterTransform = character;
        }

        /// <summary>
        /// Set whether X-axis gyro should be inverted.
        /// </summary>
        public void SetInvertGyroX(bool invert)
        {
            invertGyroX = invert;
        }

        /// <summary>
        /// Set whether Z-axis gyro should be inverted.
        /// </summary>
        public void SetInvertGyroZ(bool invert)
        {
            invertGyroZ = invert;
        }

        /// <summary>
        /// Set the gyro deadzone.
        /// </summary>
        public void SetGyroDeadzone(float deadzone)
        {
            gyroDeadzone = Mathf.Clamp01(deadzone);
        }

        #endregion
    }
}
