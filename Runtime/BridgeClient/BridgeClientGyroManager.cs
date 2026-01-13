using System.Runtime.InteropServices;
using UnityEngine;

namespace Dopple.InputSDK.BridgeClient
{
    /// <summary>
    /// Singleton manager for BridgeClient gyroscope data.
    /// Receives gyro data from parent Unity app via WebView bridge.
    ///
    /// This component enables WebGL games running inside a native app's WebView
    /// to receive gyroscope data from the parent app.
    ///
    /// Usage:
    /// 1. Add BridgeClientGyroManager to a GameObject in your scene
    /// 2. The manager will auto-detect if BridgeClient is available
    /// 3. Access gyro data via BridgeClientGyroManager.gyroData.Gravity
    /// </summary>
    [DisallowMultipleComponent]
    public class BridgeClientGyroManager : MonoBehaviour
    {
        #region DllImport

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int BridgeClientGyro_IsAvailable();

        [DllImport("__Internal")]
        private static extern void BridgeClientGyro_Start(int hz);

        [DllImport("__Internal")]
        private static extern void BridgeClientGyro_Stop();

        [DllImport("__Internal")]
        private static extern void BridgeClientGyro_RegisterDataPointer(ref BridgeClientGyroData data);

        [DllImport("__Internal")]
        private static extern int BridgeClientGyro_IsListening();
#endif

        #endregion

        #region Static Data

        /// <summary>
        /// Gyro data - updated by JavaScript event handler via shared memory.
        /// </summary>
        public static BridgeClientGyroData gyroData;

        #endregion

        #region Singleton

        private static BridgeClientGyroManager m_Instance;
        public static BridgeClientGyroManager Instance => m_Instance;

        #endregion

        #region State

        private bool isRegistered = false;
        private bool isListening = false;

        [Header("Settings")]
        [SerializeField] private int updateHz = 60;
        [SerializeField] private bool autoStart = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        public bool IsAvailable { get; private set; }
        public bool IsListening => isListening;

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

            CheckAvailability();
        }

        private void Start()
        {
            if (autoStart && IsAvailable)
            {
                StartListening();
            }
        }

        private void OnDestroy()
        {
            if (m_Instance == this)
            {
                StopListening();
                m_Instance = null;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Check if BridgeClient.GyroDataBridge is available.
        /// </summary>
        public void CheckAvailability()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IsAvailable = BridgeClientGyro_IsAvailable() != 0;
            if (showDebugInfo)
            {
                Debug.Log($"[BridgeClientGyroManager] Availability check: {IsAvailable}");
            }
#else
            IsAvailable = false;
            if (showDebugInfo)
            {
                Debug.Log("[BridgeClientGyroManager] Not WebGL platform - unavailable");
            }
#endif
        }

        /// <summary>
        /// Start listening to gyro events from BridgeClient.
        /// </summary>
        public void StartListening()
        {
            if (isListening) return;
            if (!IsAvailable)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("[BridgeClientGyroManager] Cannot start - BridgeClient not available");
                }
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // Register data pointer if not already done
            if (!isRegistered)
            {
                BridgeClientGyro_RegisterDataPointer(ref gyroData);
                isRegistered = true;
                if (showDebugInfo)
                {
                    Debug.Log("[BridgeClientGyroManager] Data pointer registered");
                }
            }

            BridgeClientGyro_Start(updateHz);
            isListening = true;

            if (showDebugInfo)
            {
                Debug.Log($"[BridgeClientGyroManager] Started listening at {updateHz} Hz");
            }
#endif
        }

        /// <summary>
        /// Stop listening to gyro events.
        /// </summary>
        public void StopListening()
        {
            if (!isListening) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            BridgeClientGyro_Stop();
            isListening = false;

            if (showDebugInfo)
            {
                Debug.Log("[BridgeClientGyroManager] Stopped listening");
            }
#endif
        }

        #endregion

        #region Convenience Properties

        /// <summary>
        /// Get raw gravity vector from parent app.
        /// </summary>
        public Vector3 Gravity => gyroData.Gravity;

        /// <summary>
        /// Get smooth gravity vector (pre-filtered by parent app).
        /// </summary>
        public Vector3 SmoothGravity => gyroData.SmoothGravity;

        /// <summary>
        /// Get device orientation quaternion.
        /// </summary>
        public Quaternion Orientation => gyroData.Orientation;

        /// <summary>
        /// Check if gyro is enabled in parent app.
        /// </summary>
        public bool IsGyroEnabled => gyroData.IsGyroEnabled;

        /// <summary>
        /// Check if movement is allowed by parent app.
        /// </summary>
        public bool IsMovementAllowed => gyroData.IsMovementAllowed;

        #endregion
    }
}
