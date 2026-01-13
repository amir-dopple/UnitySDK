using System.Runtime.InteropServices;
using UnityEngine;

namespace Dopple.InputSDK.BridgeClient
{
    /// <summary>
    /// Data structure matching the JavaScript shared memory layout.
    /// Must match exactly with BridgeClientGyroPlugin.jspre handleGyroData.
    /// Total size: 14 floats = 56 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeClientGyroData
    {
        // Gravity vector (raw from parent app)
        [MarshalAs(UnmanagedType.R4)] public float gravityX;
        [MarshalAs(UnmanagedType.R4)] public float gravityY;
        [MarshalAs(UnmanagedType.R4)] public float gravityZ;

        // Smooth gravity vector (filtered by parent app)
        [MarshalAs(UnmanagedType.R4)] public float smoothGravityX;
        [MarshalAs(UnmanagedType.R4)] public float smoothGravityY;
        [MarshalAs(UnmanagedType.R4)] public float smoothGravityZ;

        // Orientation quaternion
        [MarshalAs(UnmanagedType.R4)] public float orientationX;
        [MarshalAs(UnmanagedType.R4)] public float orientationY;
        [MarshalAs(UnmanagedType.R4)] public float orientationZ;
        [MarshalAs(UnmanagedType.R4)] public float orientationW;

        // Flags
        [MarshalAs(UnmanagedType.R4)] public float gyroEnabled;
        [MarshalAs(UnmanagedType.R4)] public float allowMovement;

        // Reserved
        [MarshalAs(UnmanagedType.R4)] public float reserved1;
        [MarshalAs(UnmanagedType.R4)] public float reserved2;

        // Helper properties
        public Vector3 Gravity => new Vector3(gravityX, gravityY, gravityZ);
        public Vector3 SmoothGravity => new Vector3(smoothGravityX, smoothGravityY, smoothGravityZ);
        public Quaternion Orientation => new Quaternion(orientationX, orientationY, orientationZ, orientationW);
        public bool IsGyroEnabled => gyroEnabled > 0.5f;
        public bool IsMovementAllowed => allowMovement > 0.5f;
    }
}
