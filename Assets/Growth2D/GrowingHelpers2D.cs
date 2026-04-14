using UnityEngine;

namespace Growth2D
{
    public class GrowingHelpers2D
    {
        // get a smoothed Gaussian falloff based on distance from a center point
        public static float GetSmoothFalloff2D(Vector2 point, Vector2 center, float radius)
        {
            float distSqr = (point - center).sqrMagnitude;
            float radiusSqr = radius * radius;

            if (distSqr >= radiusSqr) return 0f;

            float x = Mathf.Sqrt(distSqr);
            float t = x / radius;

            float invSmoothStep = (1.0f -
                (3.0f * t * t) +
                (2.0f * t * t * t));

            float areaFactor = radius / 2.0f;

            // returns a smoothstep falloff with an area of 1 under the curve
            return invSmoothStep / areaFactor;
        }


        // W(r, h) = 15 / (pi h^6) * (h - r)^3
        // h is radius of kernel
        // r = distance between nodes
        public static float GetSpikyKernel2D(Vector2 point, Vector2 center, float radius)
        {
            float distSqr = (point - center).sqrMagnitude;
            float h = radius;
            float hSqr = h * h;

            if (distSqr >= hSqr) return 0f;

            float r = Mathf.Sqrt(distSqr);

            // The "Spiky" part: (h - r)^2 or (h - r)^3
            // We use the 2D normalization factor: 10 / (PI * h^5)
            float volumeFactor = 10f / (Mathf.PI * Mathf.Pow(h, 5));
            float diff = h - r;

            return diff * diff * volumeFactor;
        }
    }

}
