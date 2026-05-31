using UnityEngine;

public class BasicEdgeBufferRenderer : MonoBehaviour
{
    public Material edgeMaterial;
    public float maxVelocity = 5.0f;

    private Mesh lineMesh;

    void Start()
    {
        CreateLineMesh();
    }

    // We build a dummy 2-vertex mesh. 
    // Vertices don't matter because we will position them manually in the vertex shader.
    private void CreateLineMesh()
    {
        lineMesh = new Mesh();
        lineMesh.vertices = new Vector3[2] { Vector3.zero, Vector3.forward };
        lineMesh.SetIndices(new int[2] { 0, 1 }, MeshTopology.Lines, 0);
    }

    public void RenderEdges(ComputeBuffer particleBuffer, int count)
    {
        if (count <= 0) return;

        RenderParams rp = new RenderParams(edgeMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one);
        rp.matProps = new MaterialPropertyBlock();

        rp.matProps.SetBuffer("particles", particleBuffer);
        rp.matProps.SetFloat("maxVelocity", maxVelocity);

        // Crucial: Each node has 8 potential neighbors. 
        // We dispatch (count * 8) instances so every edge opportunity is checked.
        int totalLineInstances = count * 8;

        Graphics.RenderMeshPrimitives(rp, lineMesh, 0, totalLineInstances);
    }

    private void OnDestroy()
    {
        if (lineMesh != null) Destroy(lineMesh);
    }
}