using UnityEngine;

public class BasicParticleBufferRenderer : MonoBehaviour
{
    public Material particleMaterial;
    public Mesh particleMesh;
    public float radius = 0.05f;

    public float maxVelocity = 5.0f;

    public virtual void SetupMaterialProperties(ComputeBuffer particleBuffer, int count, out RenderParams rp)
    {
        rp = new RenderParams(particleMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();

        rp.matProps.SetFloat("_NumInstances", count);
        rp.matProps.SetBuffer("particles", particleBuffer);
        rp.matProps.SetFloat("_Radius", radius);

        rp.matProps.SetFloat("maxVelocity", maxVelocity);
    }

    public void RenderParticles(ComputeBuffer particleBuffer, int count)
    {
        SetupMaterialProperties(particleBuffer, count, out RenderParams rp);

        Graphics.RenderMeshPrimitives(rp, particleMesh, 0, count);
    }
}



