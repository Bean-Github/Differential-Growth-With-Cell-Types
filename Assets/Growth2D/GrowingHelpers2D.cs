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

    // 0 = straight line, 
    public static float GetCurvature2D(Node2D nodeA, Node2D nodeB)
    {
        // from A to B = B - A
        Vector2 edge = nodeB.position - nodeA.position;

        float curvatureSum = 0.0f;
        int numContributions = 0;

        foreach (Node2D neighbor in nodeA.neighbors.Values)
        {
            if (neighbor.id == nodeB.id)
            {
                continue;
            }

            Vector2 toA = (neighbor.position - nodeA.position).normalized;

            float dot = Vector2.Dot(edge.normalized, toA.normalized);

            curvatureSum += (1.0f - Mathf.Abs(dot));
            numContributions++;
        }

        foreach(Node2D neighbor in nodeB.neighbors.Values)
        {
            if (neighbor.id == nodeA.id)
            {
                continue;
            }
            Vector2 toB = (neighbor.position - nodeB.position).normalized;
            float dot = Vector2.Dot(edge.normalized, toB.normalized);
            dot = (dot + 1.0f) / 2.0f;

            curvatureSum += (1.0f - Mathf.Abs(dot));
            numContributions++;
        }

        return curvatureSum / Mathf.Max(1, numContributions);
    }
}
