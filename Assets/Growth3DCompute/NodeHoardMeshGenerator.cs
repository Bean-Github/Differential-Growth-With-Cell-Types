using Growth3DCompute;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public static class NodeHoardMeshGenerator
{
    /// <param name="hoard">The populated NodeHoardCompute instance.</param>
    /// <returns>A Unity Mesh ready for rendering.</returns>
    public static Mesh GenerateMesh(NodeHoardCompute hoard)
    {
        Mesh mesh = new Mesh();
        mesh.name = "NodeHoard Mesh";

        int nodeCount = hoard.allNodes.Count;

        // Extract Vertices - Duplicate them!
        // First half is for the "front" faces, second half is for the "back" faces.
        Vector3[] vertices = new Vector3[nodeCount];
        Color[] colors = new Color[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            Vector3 pos = hoard.allNodes[i].position;
            vertices[i] = pos;                 // Front vertices

            Color typeColor = hoard.allNodes[i].type.color;
            colors[i] = typeColor;             // Front vertex color

            Debug.Log($"Vertex {i}: Position = {pos}, Color = {typeColor}");
        }

        if (vertices.Length > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // We need twice as many triangles (Front + Back)
        int[] triangles = new int[hoard.faces.Count * 3];
        int triIndex = 0;

        uint invalidID = uint.MaxValue;

        foreach (var face in hoard.faces)
        {
            if (face.halfEdge == invalidID) continue;

            var he1 = hoard.GetEdge(face.halfEdge);
            if (he1.next == invalidID) continue;

            var he2 = hoard.GetEdge(he1.next);
            if (he2.next == invalidID) continue;

            var he3 = hoard.GetEdge(he2.next);

            if (he3.next != face.halfEdge)
            {
                continue; // Skip boundary n-gons
            }

            int v1 = (int)he1.origin;
            int v2 = (int)he2.origin;
            int v3 = (int)he3.origin;

            // FACE
            triangles[triIndex++] = v1;
            triangles[triIndex++] = v2;
            triangles[triIndex++] = v3;
        }

        // Assign arrays to mesh
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;

        // Clean up the mesh for lighting and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}



