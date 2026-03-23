using UnityEngine;

namespace CameraControllers.Runtime
{
    public static class SharpnessUtility
    {
        /// <summary>
        /// This ensures smooth camera interpolations with varying framerates.
        /// </summary>
        public static float GetSharpnessInterpolant(float sharpness, float dt)
        {
            return Mathf.Clamp(1f - Mathf.Exp(-sharpness * dt), 0, 1);
        }
    }
}