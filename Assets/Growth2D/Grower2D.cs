using UnityEngine;

public class Grower2D : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural 2D Mesh";

        // Vertices in XY plane
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f)
        };

        // Two triangles (clockwise winding)
        int[] triangles = new int[]
        {
            0, 2, 1,
            2, 3, 1
        };

        // Optional UVs (needed for textures)
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
