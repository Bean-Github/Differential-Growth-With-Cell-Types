using Growth3DCompute;
using UnityEngine;

public class AuxinParticleBufferRenderer : BasicParticleBufferRenderer
{
    public Grower3DComputeRunner computeRunner;

    public float totalAuxin;

    public override void SetupMaterialProperties(ComputeBuffer particleBuffer, int count, out RenderParams rp)
    {
        base.SetupMaterialProperties(particleBuffer, count, out rp);

        //float totalAuxin = computeRunner.debug_totalAuxin;
        //if (totalAuxin == 0.0f)
        //{
        //    totalAuxin = 1.0f; // avoid division by zero
        //}

        rp.matProps.SetFloat("_TotalAuxin", totalAuxin);
    }
}



