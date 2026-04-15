using System.Collections.Generic;
using UnityEngine;

namespace Growth3D
{
    public class ShapeGenerator3D : MonoBehaviour
    {
        NodeHoard3D _nodeHoard;

        public NodeHoard3D Initialize(float separationDistance)
        {
            _nodeHoard = new NodeHoard3D(separationDistance);

            return _nodeHoard;
        }

        public void CreateTestSphere(float startRadius)
        {
            // Adjust this for detail. 
            // 0 = tabletop die (12 vertices)
            // 2 = low poly sphere (162 vertices)
            // 3 = smooth sphere (642 vertices)
            int subdivisions = 0;

            List<Vector3> vertices = new List<Vector3>();
            List<int[]> faces = new List<int[]>();

            // We use a cache to avoid creating duplicate vertices when two triangles share the same edge
            Dictionary<long, int> midpointCache = new Dictionary<long, int>();

            // 1. Setup the Base Icosahedron (Your original code!)
            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
            vertices.Add(new Vector3(-1, t, 0).normalized * startRadius);
            vertices.Add(new Vector3(1, t, 0).normalized * startRadius);
            vertices.Add(new Vector3(-1, -t, 0).normalized * startRadius);
            vertices.Add(new Vector3(1, -t, 0).normalized * startRadius);
            vertices.Add(new Vector3(0, -1, t).normalized * startRadius);
            vertices.Add(new Vector3(0, 1, t).normalized * startRadius);
            vertices.Add(new Vector3(0, -1, -t).normalized * startRadius);
            vertices.Add(new Vector3(0, 1, -t).normalized * startRadius);
            vertices.Add(new Vector3(t, 0, -1).normalized * startRadius);
            vertices.Add(new Vector3(t, 0, 1).normalized * startRadius);
            vertices.Add(new Vector3(-t, 0, -1).normalized * startRadius);
            vertices.Add(new Vector3(-t, 0, 1).normalized * startRadius);

            faces.AddRange(new int[][] {
                    new int[]{0, 11, 5}, new int[]{0, 5, 1},  new int[]{0, 1, 7},   new int[]{0, 7, 10},  new int[]{0, 10, 11},
                    new int[]{1, 5, 9},  new int[]{5, 11, 4}, new int[]{11, 10, 2}, new int[]{10, 7, 6},  new int[]{7, 1, 8},
                    new int[]{3, 9, 4},  new int[]{3, 4, 2},  new int[]{3, 2, 6},   new int[]{3, 6, 8},   new int[]{3, 8, 9},
                    new int[]{4, 9, 5},  new int[]{2, 4, 11}, new int[]{6, 2, 10},  new int[]{8, 6, 7},   new int[]{9, 8, 1}
                });

            // Local Helper to get the midpoint of an edge, and push it out to the sphere's surface
            int GetMidpoint(int p1, int p2)
            {
                // Create a unique key for the edge so we don't calculate it twice
                long min = Mathf.Min(p1, p2);
                long max = Mathf.Max(p1, p2);
                long key = (min << 32) + max; // Bitwise shift to combine two ints into a long

                if (midpointCache.TryGetValue(key, out int index))
                {
                    return index; // We already split this edge! Return the existing midpoint.
                }

                // Calculate the new vertex position
                Vector3 middle = ((vertices[p1] + vertices[p2]) / 2f).normalized * startRadius;

                vertices.Add(middle);
                int newIndex = vertices.Count - 1;
                midpointCache.Add(key, newIndex);

                return newIndex;
            }

            // 2. Subdivide the faces
            for (int i = 0; i < subdivisions; i++)
            {
                List<int[]> nextFaces = new List<int[]>();
                foreach (int[] face in faces)
                {
                    // Replace the triangle with 4 smaller triangles
                    int a = GetMidpoint(face[0], face[1]);
                    int b = GetMidpoint(face[1], face[2]);
                    int c = GetMidpoint(face[2], face[0]);

                    nextFaces.Add(new int[] { face[0], a, c });
                    nextFaces.Add(new int[] { face[1], b, a });
                    nextFaces.Add(new int[] { face[2], c, b });
                    nextFaces.Add(new int[] { a, b, c }); // The center triangle
                }
                faces = nextFaces;
            }

            // Add to NodeHoard securely
            List<Node3D> finalNodes = new List<Node3D>();
            foreach (Vector3 vert in vertices)
            {
                finalNodes.Add(_nodeHoard.AddNode(vert));
            }

            foreach (int[] face in faces)
            {
                Node3D a = finalNodes[face[0]];
                Node3D b = finalNodes[face[1]];
                Node3D c = finalNodes[face[2]];

                _nodeHoard.AddTriangle(a, b, c);
            }

        }

    }

}
