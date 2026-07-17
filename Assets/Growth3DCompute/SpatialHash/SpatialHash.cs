using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Growth3DCompute
{
    public class SpatialHashComputeRunner : MonoBehaviour
    {
        public ComputeShader spatialHashCompute;

        [Header("Debug")]
        public bool showDebugGrid = false; // ONLY enable this when debugging! It causes lag.

        protected float cellSize;
        //Bounds bounds;

        protected Grower3DComputeRunner grower3DComputeRunner;

        public virtual void SetValues(Grower3DComputeRunner grower3DComputeRunner)
        {
            this.cellSize = grower3DComputeRunner.separationDistance;
            spatialHashCompute.SetFloat("cellSize", cellSize);

            this.grower3DComputeRunner = grower3DComputeRunner;
        }

        protected int clearStartIndicesKernel;
        protected int clearSpatialLookupKernel;
        protected int createSpatialLookupKernel;
        protected int sortKernel;
        protected int calculateStartIndicesKernel;
         
        protected int elementCount;
        protected int paddedLookupCount;
        protected int hashTableSize;

        protected void Start()
        {
            SetKernels();
        }

        protected virtual void SetKernels()
        {
            clearStartIndicesKernel = spatialHashCompute.FindKernel("ClearStartIndices");
            clearSpatialLookupKernel = spatialHashCompute.FindKernel("ClearSpatialLookup");
            createSpatialLookupKernel = spatialHashCompute.FindKernel("CreateSpatialLookup");
            sortKernel = spatialHashCompute.FindKernel("SortSpatialLookup");
            calculateStartIndicesKernel = spatialHashCompute.FindKernel("CalculateStartIndices");
        }

        public virtual void UpdateSpatialLookup(
            ref ComputeBuffer elementBuffer, 
            ref ComputeBuffer spatialLookupBuffer, 
            ref ComputeBuffer startIndicesBuffer, 
            int activeElementCount
            )
        {
            elementCount = activeElementCount;
            paddedLookupCount = GetLookupSize(elementCount);
            this.hashTableSize = startIndicesBuffer.count;

            // link buffers to kernels
            ComputeHelper.SetBufferToKernels("startIndices", startIndicesBuffer, spatialHashCompute, 
                clearStartIndicesKernel, createSpatialLookupKernel, sortKernel, calculateStartIndicesKernel);

            ComputeHelper.SetBufferToKernels("spatialLookup", spatialLookupBuffer, spatialHashCompute, 
                clearSpatialLookupKernel, createSpatialLookupKernel, sortKernel, calculateStartIndicesKernel);

            ComputeHelper.SetBufferToKernels("elementData", elementBuffer, spatialHashCompute, 
                createSpatialLookupKernel, sortKernel, calculateStartIndicesKernel);

            // Set other parameters
            spatialHashCompute.SetInt("elementCount", elementCount);               // Small
            spatialHashCompute.SetInt("paddedLookupCount", paddedLookupCount);   // Small Padded (# entries we need)
            spatialHashCompute.SetInt("hashTableSize", hashTableSize);       // Massive

            Dispatch();
        }


        // do all this within compute shader
        // Based on positions, decide which particles are in which spatial cells.
        protected void Dispatch()
        {
            spatialHashCompute.Dispatch(clearStartIndicesKernel, Mathf.CeilToInt(hashTableSize / 64f), 1, 1);
            spatialHashCompute.Dispatch(clearSpatialLookupKernel, Mathf.CeilToInt(paddedLookupCount / 64f), 1, 1);

            spatialHashCompute.Dispatch(createSpatialLookupKernel, Mathf.CeilToInt(elementCount / 64f), 1, 1); // 64 threads per group?

            // Sort by cell key!!
            DispatchSort();

            // Calculate start indices of each unique cell key in the spatial lookup
            spatialHashCompute.Dispatch(calculateStartIndicesKernel, Mathf.CeilToInt(paddedLookupCount / 64f), 1, 1); // 64 threads per group
        }

        protected virtual int GetLookupSize(int elementCount)
        {
            return Mathf.NextPowerOfTwo(elementCount);
        }

        // Bitonic sort in compute shader, sort by cell key, so that particles in the same cell are adjacent in the buffer.
        protected void DispatchSort()
        {
            int numPairs = Mathf.CeilToInt(paddedLookupCount / 2.0f);

            int numStages = Mathf.RoundToInt(Mathf.Log(paddedLookupCount, 2));

            for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
            {
                int height = 1 << (stageIndex + 1);  // for determining accending or descending order, 2 4 8 16 32

                for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
                {
                    int width = 1 << (stageIndex - stepIndex);  // 2 ^ (stageIndex - stepIndex)

                    spatialHashCompute.SetInt("width", width);
                    spatialHashCompute.SetInt("height", height);


                    int numGroupsX = Mathf.CeilToInt((float)numPairs / 64f);

                    spatialHashCompute.Dispatch(sortKernel, numGroupsX, 1, 1); // one thread per pair

                }
            }
        }


    }

}
