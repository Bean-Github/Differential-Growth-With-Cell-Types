using UnityEngine;

public class GrowingHelpers2D 
{
    // get a smoothed Gaussian falloff based on distance from a center point
    public static float GetSmoothFalloff2D(Vector2 point, Vector2 center, float radius)
    {
        float x = (point - center).magnitude;

        float invSmoothStep = (1.0f - 
            (3.0f * (x / radius) * (x / radius)) + 
            (2.0f * (x / radius) * (x / radius) * (x / radius)));

        float areaFactor = radius / 2.0f;

        // returns a smoothstep falloff with an area of 1 under the curve
        return invSmoothStep / areaFactor;
    }
}
