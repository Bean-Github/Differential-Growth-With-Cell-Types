using UnityEngine;
using Growth3DCompute;

public static class NodeHoardMeshGenerator
{
    /// <summary>
    /// Generates a standard Unity Mesh from a NodeHoardCompute object.
    /// </summary>
    /// <param name="hoard">The populated NodeHoardCompute instance.</param>
    /// <returns>A Unity Mesh ready for rendering.</returns>
    public static Mesh GenerateMesh(NodeHoardCompute hoard)
    {
        Mesh mesh = new Mesh();
        mesh.name = "NodeHoard Mesh";

        // 1. Extract Vertices
        // Since node.id matches its index in allNodes, we can map them 1:1
        Vector3[] vertices = new Vector3[hoard.allNodes.Count];
        for (int i = 0; i < hoard.allNodes.Count; i++)
        {
            vertices[i] = hoard.allNodes[i].position;
        }

        if (vertices.Length > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // Assuming all faces are strictly triangles (based on AddTriangle/SplitTriangle)
        int[] triangles = new int[hoard.faces.Count * 3];
        int triIndex = 0;

        uint invalidID = uint.MaxValue;

        foreach (var face in hoard.faces)
        {
            if (face.halfEdge == invalidID) continue;

            // Get the first half-edge of the face
            var he1 = hoard.GetEdge(face.halfEdge);
            if (he1.next == invalidID) continue;
            // Walk to the next two half-edges to complete the triangle
            var he2 = hoard.GetEdge(he1.next);
            if (he2.next == invalidID) continue;
            var he3 = hoard.GetEdge(he2.next);

            if (he3.next != face.halfEdge)
            {
                continue; // Skip boundary n-gons
            }

            // Unity's triangle winding is usually Clockwise. 
            // If your mesh renders inside-out, reverse the order to: he1, he3, he2
            triangles[triIndex++] = (int)he1.origin;
            triangles[triIndex++] = (int)he2.origin;
            triangles[triIndex++] = (int)he3.origin;
        }

        // 3. Assign arrays to mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        // 4. Clean up the mesh for lighting and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}



