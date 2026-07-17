using UnityEngine;

namespace Growth3DCompute
{
    public class SpatialHashFaces : SpatialHashComputeRunner
    {
        private Face3D[] debugFaceData;

        protected override void SetKernels()
        {
            base.SetKernels();
        }

        ComputeBuffer faceEntryCounterBuffer;

        public override void SetValues(Grower3DComputeRunner grower3DComputeRunner)
        {
            base.SetValues(grower3DComputeRunner);

            if (faceEntryCounterBuffer == null)
                faceEntryCounterBuffer = new ComputeBuffer(1, sizeof(int));
        }

        public override void UpdateSpatialLookup(
            ref ComputeBuffer faceBuffer,
            ref ComputeBuffer lookupBuffer,
            ref ComputeBuffer startBuffer,
            int activeFaceCount)
        {
            faceEntryCounterBuffer.SetData(new int[] { 0 });
            spatialHashCompute.SetBuffer(createSpatialLookupKernel, "faceEntryCounter", 
                faceEntryCounterBuffer);

            ComputeHelper.SetBufferToKernels("nodeData", this.grower3DComputeRunner.nodeBuffer, spatialHashCompute,
                createSpatialLookupKernel, sortKernel, calculateStartIndicesKernel);

            ComputeHelper.SetBufferToKernels("edgeData", this.grower3DComputeRunner.halfEdgeBuffer, spatialHashCompute,
                createSpatialLookupKernel, sortKernel, calculateStartIndicesKernel);

            base.UpdateSpatialLookup(ref faceBuffer, ref lookupBuffer, ref startBuffer, activeFaceCount);


            if (showDebugGrid && elementCount > 0)
            {
                if (debugFaceData == null || debugFaceData.Length != elementCount)
                {
                    debugFaceData = new Face3D[elementCount];
                }
                // Pulling data from the GPU is a heavy operation, hence the boolean toggle!
                faceBuffer.GetData(debugFaceData, 0, 0, elementCount);
            }
        }

        protected override int GetLookupSize(int count)
        {
            return Mathf.NextPowerOfTwo(count * 9);
        }

        private void OnDestroy()
        {
            if (faceEntryCounterBuffer != null)
            {
                faceEntryCounterBuffer.Release();
                faceEntryCounterBuffer = null;
            }
        }
    }
}


