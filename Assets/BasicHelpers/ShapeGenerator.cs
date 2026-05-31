using System.Collections.Generic;
using UnityEngine;

public static class ShapeGenerator
{
    /// <summary>
    /// Creates your original Icosphere. Best for uniform neighbor distances.
    /// </summary>
    public static void CreateIcosphere(out List<Vector3> vertices, out List<int> triangles, int subdivisions)
    {
        float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
        vertices = new List<Vector3>()
        {
            new Vector3(-1,  t,  0).normalized, new Vector3( 1,  t,  0).normalized,
            new Vector3(-1, -t,  0).normalized, new Vector3( 1, -t,  0).normalized,
            new Vector3( 0, -1,  t).normalized, new Vector3( 0,  1,  t).normalized,
            new Vector3( 0, -1, -t).normalized, new Vector3( 0,  1, -t).normalized,
            new Vector3( t,  0, -1).normalized, new Vector3( t,  0,  1).normalized,
            new Vector3(-t,  0, -1).normalized, new Vector3(-t,  0,  1).normalized
        };

        triangles = new List<int>()
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        };

        Subdivide(ref vertices, ref triangles, subdivisions, true);
    }

    /// <summary>
    /// Creates a Cube. If normalized is true, it balloons outward into a "Cube-Sphere".
    /// </summary>
    public static void CreateCube(out List<Vector3> vertices, out List<int> triangles, int subdivisions, bool normalized = false)
    {
        vertices = new List<Vector3>()
        {
            new Vector3(-1, -1, -1), // 0: bottom-left-front
            new Vector3( 1, -1, -1), // 1: bottom-right-front
            new Vector3( 1,  1, -1), // 2: top-right-front
            new Vector3(-1,  1, -1), // 3: top-left-front
            new Vector3(-1, -1,  1), // 4: bottom-left-back
            new Vector3( 1, -1,  1), // 5: bottom-right-back
            new Vector3( 1,  1,  1), // 6: top-right-back
            new Vector3(-1,  1,  1)  // 7: top-left-back
        };

        // Normalize starting corners if we want a spherical base
        if (normalized)
        {
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;
        }

        triangles = new List<int>()
        {
            // --- OUTSIDE SURFACES ---
            0, 2, 1,  0, 3, 2, // Front
            5, 7, 4,  5, 6, 7, // Back
            3, 6, 2,  3, 7, 6, // Top
            4, 1, 5,  4, 0, 1, // Bottom
            4, 3, 0,  4, 7, 3, // Left
            1, 6, 5,  1, 2, 6, // Right

            // --- INTERNAL VOLUME (Cross-bracing) ---
            // These triangles slice through the center to connect opposite corners
            0, 6, 1,  0, 7, 6, // Plane connecting Front-Bottom to Back-Top
            3, 5, 2,  3, 4, 5, // Plane connecting Front-Top to Back-Bottom
            0, 6, 4,  0, 2, 6, // Plane connecting Left-Bottom to Right-Top
            1, 7, 5,  1, 3, 7  // Plane connecting Right-Bottom to Left-Top
        };

        Subdivide(ref vertices, ref triangles, subdivisions, normalized);
    }

    /// <summary>
    /// Creates a 4-sided Pyramid/Tetrahedron.
    /// </summary>
    public static void CreateTetrahedron(out List<Vector3> vertices, out List<int> triangles, int subdivisions, bool normalized = true)
    {
        float s = Mathf.Sqrt(2.0f);
        vertices = new List<Vector3>()
        {
            new Vector3( 1,  0, -1 / s).normalized,
            new Vector3(-1,  0, -1 / s).normalized,
            new Vector3( 0,  1,  1 / s).normalized,
            new Vector3( 0, -1,  1 / s).normalized
        };

        triangles = new List<int>()
        {
            0, 1, 2,  0, 3, 1,  0, 2, 3,  1, 3, 2
        };

        Subdivide(ref vertices, ref triangles, subdivisions, normalized);
    }

    // --- INTERNAL SUBDIVISION LOGIC ---

    private static void Subdivide(ref List<Vector3> vertices, ref List<int> triangles, int subdivisions, bool normalizeNodes)
    {
        for (int i = 0; i < subdivisions; i++)
        {
            List<int> subdividedTriangles = new List<int>();
            Dictionary<long, int> midpointCache = new Dictionary<long, int>();

            for (int j = 0; j < triangles.Count; j += 3)
            {
                int a = triangles[j];
                int b = triangles[j + 1];
                int c = triangles[j + 2];

                int ab = GetMidpointIndex(midpointCache, vertices, a, b, normalizeNodes);
                int bc = GetMidpointIndex(midpointCache, vertices, b, c, normalizeNodes);
                int ca = GetMidpointIndex(midpointCache, vertices, c, a, normalizeNodes);

                subdividedTriangles.AddRange(new int[] { a, ab, ca });
                subdividedTriangles.AddRange(new int[] { b, bc, ab });
                subdividedTriangles.AddRange(new int[] { c, ca, bc });
                subdividedTriangles.AddRange(new int[] { ab, bc, ca });
            }
            triangles = subdividedTriangles;
        }
    }

    private static int GetMidpointIndex(Dictionary<long, int> cache, List<Vector3> vertices, int p1, int p2, bool normalize)
    {
        long first = Mathf.Min(p1, p2);
        long second = Mathf.Max(p1, p2);
        long key = (first << 32) | second;

        if (cache.TryGetValue(key, out int existingIndex))
        {
            return existingIndex;
        }

        Vector3 middle = (vertices[p1] + vertices[p2]) / 2.0f;
        if (normalize) middle = middle.normalized;

        vertices.Add(middle);

        int newIndex = vertices.Count - 1;
        cache.Add(key, newIndex);
        return newIndex;
    }
}