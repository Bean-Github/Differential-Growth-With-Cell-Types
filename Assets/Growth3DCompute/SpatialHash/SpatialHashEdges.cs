using UnityEngine;

namespace Growth3DCompute
{
    public class SpatialHashEdges : SpatialHashComputeRunner
    {

        protected override void SetKernels()
        {
            base.SetKernels();
        }

        ComputeBuffer edgeEntryCounterBuffer;

        public override void SetValues(Grower3DComputeRunner grower3DComputeRunner)
        {
            base.SetValues(grower3DComputeRunner);

            if (edgeEntryCounterBuffer == null)
                edgeEntryCounterBuffer = new ComputeBuffer(1, sizeof(int));
        }

        public override void UpdateSpatialLookup(
            ref ComputeBuffer edgeBuffer,
            ref ComputeBuffer lookupBuffer,
            ref ComputeBuffer startBuffer,
            int activeEdgeCount)
        {
            edgeEntryCounterBuffer.SetData(new int[] { 0 });
            spatialHashCompute.SetBuffer(createSpatialLookupKernel, "edgeEntryCounter", 
                edgeEntryCounterBuffer);

            ComputeHelper.SetBufferToKernels("nodeData", this.grower3DComputeRunner.nodeBuffer, spatialHashCompute,
                createSpatialLookupKernel, sortKernel, calculateStartIndicesKernel);

            base.UpdateSpatialLookup(ref edgeBuffer, ref lookupBuffer, ref startBuffer, activeEdgeCount);
        }

        protected override int GetLookupSize(int count)
        {
            return Mathf.NextPowerOfTwo(count * 4);
        }

        private void OnDestroy()
        {
            if (edgeEntryCounterBuffer != null)
            {
                edgeEntryCounterBuffer.Release();
                edgeEntryCounterBuffer = null;
            }
        }
    }
}


