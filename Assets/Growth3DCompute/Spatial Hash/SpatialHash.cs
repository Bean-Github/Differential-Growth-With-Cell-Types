using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class SpatialHashComputeRunner : MonoBehaviour
{
    float smoothingRadius;
    Bounds bounds;

    public void SetValues(float smoothingRadius, Bounds bounds)
    {
        this.smoothingRadius = smoothingRadius;
        this.bounds = bounds;
    }

    public ComputeShader spatialHashCompute;


    int createSpatialLookupKernel;
    int sortKernel;
    int calculateStartIndicesKernel;

    //ComputeBuffer spatialLookupBuffer;
    //ComputeBuffer startIndicesBuffer;

    int particleCount;

    public void UpdateSpatialLookup(ref ComputeBuffer particleBuffer, ref ComputeBuffer spatialLookupBuffer, ref ComputeBuffer startIndicesBuffer)
    {
        particleCount = particleBuffer.count;

        createSpatialLookupKernel = spatialHashCompute.FindKernel("CreateSpatialLookup");
        sortKernel = spatialHashCompute.FindKernel("SortSpatialLookup");
        calculateStartIndicesKernel = spatialHashCompute.FindKernel("CalculateStartIndices");

        spatialHashCompute.SetBuffer(createSpatialLookupKernel, "particleData", particleBuffer);
        spatialHashCompute.SetBuffer(sortKernel, "particleData", particleBuffer);
        spatialHashCompute.SetBuffer(calculateStartIndicesKernel, "particleData", particleBuffer);

        //spatialLookupBuffer = new ComputeBuffer(particleCount, sizeof(uint) * 3);
        //startIndicesBuffer = new ComputeBuffer(particleCount, sizeof(uint));

        spatialHashCompute.SetBuffer(createSpatialLookupKernel, "spatialLookup", spatialLookupBuffer);
        spatialHashCompute.SetBuffer(sortKernel, "spatialLookup", spatialLookupBuffer);
        spatialHashCompute.SetBuffer(calculateStartIndicesKernel, "spatialLookup", spatialLookupBuffer);

        spatialHashCompute.SetBuffer(calculateStartIndicesKernel, "startIndices", startIndicesBuffer);
        spatialHashCompute.SetBuffer(sortKernel, "startIndices", startIndicesBuffer);
        spatialHashCompute.SetBuffer(createSpatialLookupKernel, "startIndices", startIndicesBuffer);

        // Set other parameters
        spatialHashCompute.SetInt("particleCount", particleCount);
        spatialHashCompute.SetFloat("smoothingRadius", smoothingRadius);
        spatialHashCompute.SetVector("boundsCenter", bounds.center);
        spatialHashCompute.SetVector("boundsExtents", bounds.extents);

        Dispatch();
        // then dispatch stuff
        //return Dispatch();
    }


    // do all this within compute shader
    // Based on positions, decide which particles are in which spatial cells.
    private void Dispatch()
    {
        //// create spatial lookup
        //// One-time setup
        //ComputeBuffer argsBuffer = new ComputeBuffer(1, sizeof(int) * 3, ComputeBufferType.IndirectArguments);

        //// Fill buffer with initial group counts
        ////int[] args = new int[3] { Mathf.CeilToInt(particleCount / 64.0f), 1, 1 };
        ////argsBuffer.SetData(args);

        ////spatialHashCompute.DispatchIndirect(createSpatialLookupKernel, argsBuffer, 0);

        //int threadsPerGroup = 64;
        //int totalGroupsX = Mathf.CeilToInt(particleCount / (float)threadsPerGroup);
        //int groupsX = totalGroupsX;
        //int groupsY = 1;

        //if (totalGroupsX > 65535)
        //{
        //    groupsY = Mathf.CeilToInt(totalGroupsX / 65535f);
        //    groupsX = Mathf.CeilToInt(totalGroupsX / (float)groupsY);
        //}

        //int[] args = new int[3] { groupsX, groupsY, 1 };
        //argsBuffer.SetData(args);
        //spatialHashCompute.SetInt("totalThreadsInX", groupsX * threadsPerGroup);
        //spatialHashCompute.DispatchIndirect(createSpatialLookupKernel, argsBuffer, 0);

        

        //spatialHashCompute.DispatchIndirect(calculateStartIndicesKernel, argsBuffer, 0);

        spatialHashCompute.Dispatch(createSpatialLookupKernel, Mathf.CeilToInt(particleCount / 64f), 1, 1); // 64 threads per group?

        // Sort by cell key!!
        DispatchSort();
        // Calculate start indices of each unique cell key in the spatial lookup
        spatialHashCompute.Dispatch(calculateStartIndicesKernel, Mathf.CeilToInt(particleCount / 64f), 1, 1); // 64 threads per group

        //return (
        //    spatialLookupBuffer,
        //    startIndicesBuffer
        //);

        //argsBuffer.Release();


    }


    //private void Dispatch()
    //{
    //    int batchSize = 65536; // or whatever safe limit
    //    int numBatches = Mathf.CeilToInt((float)particleCount / batchSize);

    //    for (int b = 0; b < numBatches; b++)
    //    {
    //        int startIndex = b * batchSize;
    //        int currBatchSize = Mathf.Min(batchSize, particleCount - startIndex);

    //        spatialHashCompute.SetInt("startIndex", startIndex);
    //        spatialHashCompute.SetInt("currBatchSize", currBatchSize);


    //        int threadsPerGroup = 64;
    //        int numThreadGroups = Mathf.CeilToInt((float)currBatchSize / threadsPerGroup);

    //        // create spatial lookup
    //        spatialHashCompute.Dispatch(createSpatialLookupKernel, numThreadGroups, 1, 1); // 64 threads per group?

    //        // Sort by cell key!!
    //        DispatchSort(currBatchSize);

    //        // Calculate start indices of each unique cell key in the spatial lookup
    //        spatialHashCompute.Dispatch(calculateStartIndicesKernel, numThreadGroups, 1, 1); // 64 threads per group

    //        //return (
    //        //    spatialLookupBuffer,
    //        //    startIndicesBuffer
    //        //);
    //    }

    //}

    void DispatchSort()
    {
        int numPairs = Mathf.CeilToInt(BitonicSort.NextPowerOfTwo(particleCount) / 2);

        int numStages = (int)Mathf.Log(numPairs * 2, 2);

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


                //int numGroupsTotal = numPairs;                 // one group per pair
                //int maxX = 65535;                              // hardware cap
                //int groupsX = Mathf.Min(numGroupsTotal, maxX); // clamp X
                //int groupsY = Mathf.CeilToInt(numGroupsTotal / (float)maxX);

                //spatialHashCompute.SetInt("width", width);
                //spatialHashCompute.SetInt("height", height);
                //spatialHashCompute.SetInt("numPairs", numPairs);
                //spatialHashCompute.SetInt("groupsX", groupsX); // pass to shader

                //spatialHashCompute.Dispatch(sortKernel, groupsX, groupsY, 1);
            }
        }
    }

    //private void OnDestroy()
    //{
    //    spatialLookupBuffer?.Release();
    //    startIndicesBuffer?.Release();
    //}

    //private void OnDisable()
    //{
    //    spatialLookupBuffer?.Release();
    //    startIndicesBuffer?.Release();
    //}
}


