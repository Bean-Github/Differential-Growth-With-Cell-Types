using UnityEngine;

public class BasicEdgeBufferRenderer : MonoBehaviour
{
    public Material edgeMaterial;
    public float maxVelocity = 5.0f;

    public void RenderEdges(ComputeBuffer particleBuffer, ComputeBuffer halfEdgeBuffer, int count, int halfEdgeCount)
    {
        if (count <= 0 || halfEdgeCount <= 0) return;

        Bounds bounds = new Bounds(Vector3.zero, 10000 * Vector3.one);

        // Bind everything DIRECTLY to the material
        edgeMaterial.SetBuffer("particles", particleBuffer);
        edgeMaterial.SetBuffer("halfEdges", halfEdgeBuffer);
        edgeMaterial.SetFloat("maxVelocity", maxVelocity);

        Graphics.DrawProcedural(edgeMaterial, bounds, MeshTopology.Lines, 2, halfEdgeCount);
    }
}