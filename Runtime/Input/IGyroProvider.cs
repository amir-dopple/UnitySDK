using UnityEngine;

namespace Dopple.InputSDK
{
    /// <summary>
    /// Interface for external gyro providers.
    /// Implement this interface to provide custom gyro data from external sources
    /// such as custom hardware controllers or third-party gyro libraries.
    /// </summary>
    public interface IGyroProvider
    {
        /// <summary>
        /// Get the rotation quaternion from the external gyro source.
        /// Used by RotationInputController to extract Z-axis rotation.
        /// </summary>
        /// <returns>Device orientation as a quaternion</returns>
        Quaternion GetRotatingObjectRotation();

        /// <summary>
        /// Get raw gravity vector from the external gyro source.
        /// Used by DirectionInputController for tilt-based movement.
        /// </summary>
        /// <returns>Gravity vector in device coordinates</returns>
        Vector3 GetGravity();

        /// <summary>
        /// Whether this provider is currently available and providing valid data.
        /// </summary>
        bool IsAvailable { get; }
    }
}
