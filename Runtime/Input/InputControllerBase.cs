using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Dopple.InputSDK.Filters;
using Dopple.InputSDK.BridgeClient;

namespace Dopple.InputSDK
{
    /// <summary>
    /// Abstract base class for input controllers.
    /// Handles gyro initialization, mode detection, smoothing, and shared input logic.
    ///
    /// GYRO COORDINATE SYSTEM DOCUMENTATION:
    ///
    /// The controller uses Unity Input System's GravitySensor which provides
    /// gravity vector in device coordinates:
    /// - Device held upright (portrait): gravity = (0, -9.8, 0)
    /// - Device tilted left: gravity.x becomes negative
    /// - Device tilted forward: gravity.z becomes negative
    ///
    /// For WebGL, we use BridgeClient which receives gravity from a parent Unity app
    /// hosting the WebGL build in a WebView. The parent app reads Unity's Input.gyro.gravity
    /// (old input system) which has different coordinate conventions that are converted.
    ///
    /// SUBCLASSES:
    /// - RotationInputController: Extracts Z-axis rotation angle (0-360 degrees)
    /// - DirectionInputController: Extracts X/Z movement direction (Vector2)
    /// </summary>
    public abstract class InputControllerBase : MonoBehaviour
    {
        #region Configuration

        [Header("Input Settings")]
        [Tooltip("Input mode selection. Auto will detect the best available source.")]
        [SerializeField] protected InputMode inputMode = InputMode.Auto;

        [Header("Gyro Settings")]
        [Tooltip("Sensitivity multiplier for gyro input")]
        [SerializeField] protected float gyroSensitivity = 1f;

        [Tooltip("Invert the final input direction/rotation")]
        [SerializeField] protected bool invertInput = false;

        [Header("Gyro Smoothing (One Euro Filter)")]
        [Tooltip("Enable smoothing to reduce gyro jitter")]
        [SerializeField] protected bool enableGyroSmoothing = true;

        [Range(0.1f, 10f)]
        [Tooltip("Minimum cutoff frequency. Lower = less jitter, more lag")]
        [SerializeField] protected float smoothingMinCutoff = 1.0f;

        [Range(0f, 1f)]
        [Tooltip("Speed coefficient. Higher = less lag at fast movements")]
        [SerializeField] protected float smoothingBeta = 0.5f;

        [Header("External Gyro Provider")]
        [Tooltip("Optional: Assign a GameObject with a component implementing IGyroProvider")]
        [SerializeField] protected GameObject externalGyroProviderObject;

        [Header("Debug")]
        [SerializeField] protected bool showDebugInfo = false;

        #endregion

        #region State

        protected IGyroProvider externalGyroProvider;
        protected bool gyroEnabled = false;
        protected GravitySensor gravitySensor;
        protected BridgeClientGyroManager bridgeClientGyroManager;
        protected bool bridgeClientGyroEnabled = false;
        protected OneEuroFilterVector3 gravityFilter;

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Process gravity vector and update output.
        /// Called each frame when using gyro-based input modes.
        /// </summary>
        /// <param name="gravity">Raw or smoothed gravity vector</param>
        protected abstract void ProcessGravityInput(Vector3 gravity);

        /// <summary>
        /// Process mouse position input.
        /// Called when InputMode is MousePosition.
        /// </summary>
        protected abstract void UpdateMousePositionInput();

        /// <summary>
        /// Process mouse delta input.
        /// Called when InputMode is MouseDelta.
        /// </summary>
        protected abstract void UpdateMouseDeltaInput();

        /// <summary>
        /// Process scroll wheel input.
        /// Called when InputMode is ScrollWheel.
        /// </summary>
        protected abstract void UpdateScrollInput();

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // Subclasses should handle singleton setup
        }

        protected virtual void Start()
        {
            InitializeInputSources();
        }

        protected virtual void Update()
        {
            UpdateInput();
        }

        protected virtual void OnDestroy()
        {
            CleanupInputSources();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize all input sources based on current platform and settings.
        /// </summary>
        protected virtual void InitializeInputSources()
        {
            // Initialize external gyro provider
            if (externalGyroProviderObject != null)
            {
                externalGyroProvider = externalGyroProviderObject.GetComponent<IGyroProvider>();
                if (externalGyroProvider != null && showDebugInfo)
                {
                    Debug.Log($"[{GetType().Name}] Found external IGyroProvider");
                }
            }

            // Initialize gyro smoothing filter
            if (enableGyroSmoothing)
            {
                gravityFilter = new OneEuroFilterVector3(smoothingMinCutoff, smoothingBeta);
            }

            // Initialize BridgeClient gyroscope (WebGL only, highest priority)
            if (inputMode == InputMode.Auto || inputMode == InputMode.BridgeClientGyro)
            {
                InitializeBridgeClientGyro();
            }

            // Initialize native gyro (non-WebGL only)
#if !UNITY_WEBGL || UNITY_EDITOR
            if (inputMode == InputMode.Auto || inputMode == InputMode.Gyro)
            {
                InitializeNativeGyro();
            }
#endif
        }

        /// <summary>
        /// Initialize BridgeClient gyroscope for WebGL WebView scenarios.
        /// </summary>
        protected virtual void InitializeBridgeClientGyro()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            bridgeClientGyroManager = BridgeClientGyroManager.Instance;

            if (bridgeClientGyroManager == null)
            {
                bridgeClientGyroManager = FindFirstObjectByType<BridgeClientGyroManager>();
            }

            // Auto-create if not found
            if (bridgeClientGyroManager == null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[{GetType().Name}] BridgeClientGyroManager not found - creating one automatically");
                }
                var gyroGO = new GameObject("BridgeClientGyroManager");
                bridgeClientGyroManager = gyroGO.AddComponent<BridgeClientGyroManager>();
            }

            if (bridgeClientGyroManager != null)
            {
                bridgeClientGyroManager.CheckAvailability();

                if (bridgeClientGyroManager.IsAvailable)
                {
                    bridgeClientGyroManager.StartListening();
                    bridgeClientGyroEnabled = true;
                    Debug.Log($"[{GetType().Name}] BridgeClient Gyro: Started listening to parent app");
                }
                else if (showDebugInfo)
                {
                    Debug.Log($"[{GetType().Name}] BridgeClient Gyro: Not available (not running in WebView)");
                }
            }
#else
            if (showDebugInfo)
            {
                Debug.Log($"[{GetType().Name}] BridgeClient Gyro not available (not WebGL platform)");
            }
#endif
        }

        /// <summary>
        /// Initialize native gyro sensors via Unity Input System.
        /// </summary>
        protected virtual void InitializeNativeGyro()
        {
            gravitySensor = GravitySensor.current;

            // Try to find in all devices if not current
            if (gravitySensor == null)
            {
                foreach (var device in InputSystem.devices)
                {
                    if (device is GravitySensor sensor)
                    {
                        gravitySensor = sensor;
                        break;
                    }
                }
            }

            // Try to add explicitly (Android may require this)
            if (gravitySensor == null)
            {
                try
                {
                    gravitySensor = InputSystem.AddDevice<GravitySensor>();
                }
                catch (Exception e)
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning($"[{GetType().Name}] Failed to add GravitySensor: {e.Message}");
                    }
                }
            }

            if (gravitySensor != null)
            {
                if (!gravitySensor.enabled)
                {
                    InputSystem.EnableDevice(gravitySensor);
                }
                gyroEnabled = true;

                if (showDebugInfo)
                {
                    Debug.Log($"[{GetType().Name}] Gravity sensor enabled");
                }
            }
            else if (showDebugInfo)
            {
                Debug.Log($"[{GetType().Name}] Gravity sensor not available on this device");
            }
        }

        /// <summary>
        /// Cleanup input sources on destroy.
        /// </summary>
        protected virtual void CleanupInputSources()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (bridgeClientGyroManager != null)
            {
                bridgeClientGyroManager.StopListening();
            }
#endif
        }

        #endregion

        #region Input Processing

        /// <summary>
        /// Main input update called each frame.
        /// Routes to appropriate input handler based on active mode.
        /// </summary>
        protected virtual void UpdateInput()
        {
            InputMode activeMode = GetActiveInputMode();

            switch (activeMode)
            {
                case InputMode.Gyro:
                    UpdateGyroInput();
                    break;
                case InputMode.BridgeClientGyro:
                    UpdateBridgeClientGyroInput();
                    break;
                case InputMode.MousePosition:
                    UpdateMousePositionInput();
                    break;
                case InputMode.MouseDelta:
                    UpdateMouseDeltaInput();
                    break;
                case InputMode.ScrollWheel:
                    UpdateScrollInput();
                    break;
            }
        }

        /// <summary>
        /// Get the currently active input mode, resolving Auto to actual mode.
        /// </summary>
        protected virtual InputMode GetActiveInputMode()
        {
            if (inputMode != InputMode.Auto)
            {
                return inputMode;
            }

#if UNITY_EDITOR
            return InputMode.MouseDelta;
#elif UNITY_WEBGL
            if (bridgeClientGyroEnabled)
            {
                return InputMode.BridgeClientGyro;
            }
            return InputMode.MouseDelta;
#else
            if (externalGyroProvider != null && externalGyroProvider.IsAvailable)
            {
                return InputMode.Gyro;
            }
            if (gyroEnabled)
            {
                return InputMode.Gyro;
            }
            return InputMode.MouseDelta;
#endif
        }

        /// <summary>
        /// Update using native gyro sensor.
        /// </summary>
        protected virtual void UpdateGyroInput()
        {
            Vector3 gravity = GetGravityVector();

            // Apply smoothing if enabled
            if (enableGyroSmoothing && gravityFilter != null)
            {
                gravity = gravityFilter.Filter(gravity, Time.deltaTime);
            }

            ProcessGravityInput(gravity);
        }

        /// <summary>
        /// Update using BridgeClient gyro (WebGL WebView).
        /// </summary>
        protected virtual void UpdateBridgeClientGyroInput()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!bridgeClientGyroEnabled || bridgeClientGyroManager == null)
            {
                return;
            }

            // Check if movement is allowed by parent app
            if (!BridgeClientGyroManager.gyroData.IsMovementAllowed)
            {
                return;
            }

            Vector3 gravity = BridgeClientGyroManager.gyroData.Gravity;

            // Apply smoothing if enabled
            if (enableGyroSmoothing && gravityFilter != null)
            {
                gravity = gravityFilter.Filter(gravity, Time.deltaTime);
            }

            ProcessGravityInput(gravity);
#endif
        }

        /// <summary>
        /// Get gravity vector from best available source.
        /// </summary>
        protected virtual Vector3 GetGravityVector()
        {
            // Priority: External > Native
            if (externalGyroProvider != null && externalGyroProvider.IsAvailable)
            {
                return externalGyroProvider.GetGravity();
            }

            if (gyroEnabled && gravitySensor != null && gravitySensor.enabled)
            {
                return gravitySensor.gravity.ReadValue();
            }

            return Vector3.down;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the input mode at runtime.
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            inputMode = mode;
        }

        /// <summary>
        /// Get the currently active input mode (resolves Auto).
        /// </summary>
        public InputMode GetCurrentInputMode()
        {
            return GetActiveInputMode();
        }

        /// <summary>
        /// Enable or disable gyro smoothing at runtime.
        /// </summary>
        public void SetGyroSmoothingEnabled(bool enabled)
        {
            enableGyroSmoothing = enabled;
            if (enabled && gravityFilter == null)
            {
                gravityFilter = new OneEuroFilterVector3(smoothingMinCutoff, smoothingBeta);
            }
        }

        /// <summary>
        /// Get whether gyro smoothing is enabled.
        /// </summary>
        public bool IsGyroSmoothingEnabled => enableGyroSmoothing;

        /// <summary>
        /// Update gyro smoothing filter parameters at runtime.
        /// </summary>
        /// <param name="minCutoff">Minimum cutoff frequency. Lower = less jitter, more lag (0.1-10)</param>
        /// <param name="beta">Speed coefficient. Higher = less lag at fast movements (0-1)</param>
        public void SetSmoothingParameters(float minCutoff, float beta)
        {
            smoothingMinCutoff = Mathf.Clamp(minCutoff, 0.1f, 10f);
            smoothingBeta = Mathf.Clamp01(beta);
            gravityFilter?.UpdateParameters(smoothingMinCutoff, smoothingBeta);
        }

        /// <summary>
        /// Reset the gyro smoothing filter state.
        /// Useful after significant angle changes or calibration.
        /// </summary>
        public void ResetSmoothingFilter()
        {
            gravityFilter?.Reset();
        }

        /// <summary>
        /// Set an external gyro provider at runtime.
        /// </summary>
        public void SetExternalGyroProvider(IGyroProvider provider)
        {
            externalGyroProvider = provider;
        }

        /// <summary>
        /// Check if any gyro input is available.
        /// </summary>
        public bool IsAnyGyroAvailable => gyroEnabled || bridgeClientGyroEnabled ||
            (externalGyroProvider != null && externalGyroProvider.IsAvailable);

        /// <summary>
        /// Check if BridgeClient gyroscope is enabled (WebGL WebView).
        /// </summary>
        public bool IsBridgeClientGyroEnabled => bridgeClientGyroEnabled;

        #endregion
    }
}
