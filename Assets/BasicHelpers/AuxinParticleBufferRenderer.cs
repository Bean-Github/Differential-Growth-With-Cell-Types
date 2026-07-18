using Growth3DCompute;
using UnityEngine;

public class AuxinParticleBufferRenderer : BasicParticleBufferRenderer
{
    public Grower3DComputeRunner computeRunner;

    public override void SetupMaterialProperties(ComputeBuffer particleBuffer, int count, out RenderParams rp)
    {
        base.SetupMaterialProperties(particleBuffer, count, out rp);

        rp.matProps.SetFloat("_TotalAuxin", computeRunner.debug_totalAuxin);
    }
}



