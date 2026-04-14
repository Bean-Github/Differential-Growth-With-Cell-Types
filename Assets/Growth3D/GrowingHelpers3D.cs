using UnityEngine;

namespace Growth3D
{
    public class GrowingHelpers3D
    {
        // get a smoothed Gaussian falloff based on distance from a center point
        public static float GetSmoothFalloff3D(Vector3 point, Vector3 center, float radius)
        {
            float distSqr = (point - center).sqrMagnitude;
            float radiusSqr = radius * radius;

            if (distSqr >= radiusSqr) return 0f;

            float r = Mathf.Sqrt(distSqr);
            float t = r / radius;

            // (1 at center, 0 at edge)
            float invSmoothStep = 1.0f - (3.0f * t * t) + (2.0f * t * t * t);

            // the volume integral of this curve over a sphere is (4 * PI * radius^3) / 15
            float volumeFactor = 15.0f / (4.0f * Mathf.PI * radius * radius * radius);

            return invSmoothStep * volumeFactor;
        }


        // W(r, h) = 15 / (pi h^6) * (h - r)^3
        // h is radius of kernel
        // r = distance between nodes
        public static float GetSpikyKernel3D(Vector3 point, Vector3 center, float radius)
        {
            float distSqr = (point - center).sqrMagnitude;
            float h = radius;
            float hSqr = h * h;

            if (distSqr >= hSqr) return 0f;

            float r = Mathf.Sqrt(distSqr);

            // The correct 3D normalization factor and cubic falloff
            float volumeFactor = 15f / (Mathf.PI * Mathf.Pow(h, 6));
            float diff = h - r;

            return diff * diff * diff * volumeFactor;
        }

        // Gradient of the Spiky Kernel (used to calculate pressure forces between nodes)
        // Gradient W = -45 / (pi h^6) * (h - r)^2 * (direction vector)
        public static Vector3 GetSpikyKernelGradient3D(Vector3 point, Vector3 center, float radius)
        {
            Vector3 diffVec = point - center;
            float distSqr = diffVec.sqrMagnitude;
            float h = radius;

            // Avoid division by zero at the exact center
            if (distSqr >= h * h || distSqr == 0f) return Vector3.zero;

            float r = Mathf.Sqrt(distSqr);
            float diff = h - r;

            // The derivative of the 3D spiky kernel
            float gradientFactor = -45f / (Mathf.PI * Mathf.Pow(h, 6));

            // Normalize direction and multiply by the derivative magnitude
            return (diffVec / r) * (gradientFactor * diff * diff);
        }
    }
}
