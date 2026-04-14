using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// TODO:
/// use CountSort.compute to sort spatial hash,
/// keep create spatial lookup?
/// potentially use new buffer to store struct Entry
///{
///    uint particleIndex;
///    uint hash;
///    uint cellKey;
///    uint pad;
///}
///;
/// </summary>


using static ComputeHelper;






public class SpatialHashCountSort : MonoBehaviour
{
    float smoothingRadius;
    Bounds bounds;

    public void SetValues(float smoothingRadius, Bounds bounds)
    {
        this.smoothingRadius = smoothingRadius;
        this.bounds = bounds;
    }

    public ComputeShader spatialHashCompute;

    public ComputeShader countSortCompute;


    int createSpatialLookupKernel;
    int sortKernel;
    int calculateStartIndicesKernel;

    //ComputeBuffer spatialLookupBuffer;
    //ComputeBuffer startIndicesBuffer;

    int particleCount;


    // Count sort kernels and buffers and parameters
    int clearCountsKernel;
    int calculateCountsKernel;
    int scatterOutputsKernel;
    int copyBackKernel;

    uint maxValue;


    public void UpdateSpatialLookup(
        ref ComputeBuffer particleBuffer, 
        ref ComputeBuffer spatialLookupBuffer, 
        ref ComputeBuffer startIndicesBuffer, 
        ref ComputeBuffer sortedSpatialLookupBuffer, 
        ref ComputeBuffer countsBuffer)
    {
        particleCount = particleBuffer.count;

        createSpatialLookupKernel = spatialHashCompute.FindKernel("CreateSpatialLookup");
        sortKernel = spatialHashCompute.FindKernel("SortSpatialLookup");
        calculateStartIndicesKernel = spatialHashCompute.FindKernel("CalculateStartIndices");

        // particle data
        spatialHashCompute.SetBuffer(createSpatialLookupKernel, "particleData", particleBuffer);
        spatialHashCompute.SetBuffer(sortKernel, "particleData", particleBuffer);
        spatialHashCompute.SetBuffer(calculateStartIndicesKernel, "particleData", particleBuffer);

        // spatial lookup data
        spatialHashCompute.SetBuffer(createSpatialLookupKernel, "spatialLookup", spatialLookupBuffer);
        spatialHashCompute.SetBuffer(sortKernel, "spatialLookup", spatialLookupBuffer);
        spatialHashCompute.SetBuffer(calculateStartIndicesKernel, "spatialLookup", spatialLookupBuffer);

        // start indices data
        spatialHashCompute.SetBuffer(calculateStartIndicesKernel, "startIndices", startIndicesBuffer);
        spatialHashCompute.SetBuffer(sortKernel, "startIndices", startIndicesBuffer);
        spatialHashCompute.SetBuffer(createSpatialLookupKernel, "startIndices", startIndicesBuffer);

        // Set other parameters
        spatialHashCompute.SetInt("particleCount", particleCount);
        spatialHashCompute.SetFloat("smoothingRadius", smoothingRadius);
        spatialHashCompute.SetVector("boundsCenter", bounds.center);
        spatialHashCompute.SetVector("boundsExtents", bounds.extents);


        // set up count sort compute shader TODO: calculate maxValue
        maxValue = (uint) (particleCount - 1);

        clearCountsKernel = countSortCompute.FindKernel("ClearCounts");
        calculateCountsKernel = countSortCompute.FindKernel("CalculateCounts");
        scatterOutputsKernel = countSortCompute.FindKernel("ScatterOutput");
        copyBackKernel = countSortCompute.FindKernel("CopyBack");

        int count = spatialLookupBuffer.count;  // equal to particle count

        // release old buffers if they exist


        countSortCompute.SetBuffer(scatterOutputsKernel, "spatialLookupInputItems", spatialLookupBuffer);
        countSortCompute.SetBuffer(calculateCountsKernel, "spatialLookupInputItems", spatialLookupBuffer);
        countSortCompute.SetBuffer(copyBackKernel, "spatialLookupInputItems", spatialLookupBuffer);
        
        countSortCompute.SetBuffer(clearCountsKernel, "Counts", countsBuffer);
        countSortCompute.SetBuffer(calculateCountsKernel, "Counts", countsBuffer);
        countSortCompute.SetBuffer(scatterOutputsKernel, "Counts", countsBuffer);

        countSortCompute.SetBuffer(scatterOutputsKernel, "spatialLookupSortedItems", sortedSpatialLookupBuffer);
        countSortCompute.SetBuffer(copyBackKernel, "spatialLookupSortedItems", sortedSpatialLookupBuffer);

        // this is what im supposed to do below
        //ComputeHelper.SetBuffer(cs, keysBuffer, "InputKeys", CountKernel, ScatterOutputsKernel, CopyBackKernel);
        //ComputeHelper.SetBuffer(cs, valuesBuffer, "InputValues", ScatterOutputsKernel, CopyBackKernel);

        //ComputeHelper.SetBuffer(cs, sortedSpatialLookupBuffer, "SortedKeys", ScatterOutputsKernel, CopyBackKernel);
        //ComputeHelper.SetBuffer(cs, sortedValuesBuffer, "SortedValues", ScatterOutputsKernel, CopyBackKernel);
        //ComputeHelper.SetBuffer(cs, countsBuffer, "Counts", ClearCountsKernel, CountKernel, ScatterOutputsKernel);

        countSortCompute.SetInt("numInputs", count);


        Dispatch(ref countsBuffer);

    }


    // do all this within compute shader
    // Based on positions, decide which particles are in which spatial cells.
    private void Dispatch(ref ComputeBuffer countsBuffer)
    {

        spatialHashCompute.Dispatch(createSpatialLookupKernel, Mathf.CeilToInt(particleCount / 64f), 1, 1); // 64 threads per group?

        // Sort by cell key!!
        DispatchSort(ref countsBuffer);

        // Calculate start indices of each unique cell key in the spatial lookup
        spatialHashCompute.Dispatch(calculateStartIndicesKernel, Mathf.CeilToInt(particleCount / 64f), 1, 1); // 64 threads per group

    }

    void DispatchSort(ref ComputeBuffer countsBuffer)
    {
        int count = particleCount;

        int threadsPerGroup = Mathf.CeilToInt(count / 256f);

        countSortCompute.Dispatch(clearCountsKernel, threadsPerGroup, 1, 1);
        countSortCompute.Dispatch(calculateCountsKernel, threadsPerGroup, 1, 1);

        // CPU prefix sum instead of GPU scan
        // 1. Read counts back from GPU
        int[] counts = new int[maxValue + 1];
        countsBuffer.GetData(counts);

        // 2. Perform prefix sum on CPU
        int sum = 0;
        for (int i = 0; i <= maxValue; i++)
        {
            int temp = counts[i];
            counts[i] = sum; // store prefix sum
            sum += temp;
        }

        // 3. Upload back to GPU
        countsBuffer.SetData(counts);


        countSortCompute.Dispatch(scatterOutputsKernel, threadsPerGroup, 1, 1);
        countSortCompute.Dispatch(copyBackKernel, threadsPerGroup, 1, 1);


        //ComputeHelper.Dispatch(cs, count, kernelIndex: ClearCountsKernel);
        //ComputeHelper.Dispatch(cs, count, kernelIndex: CountKernel);

        //// prefix sum calculation on countsBuffer
        //scan.Run(countsBuffer);


        //ComputeHelper.Dispatch(cs, count, kernelIndex: ScatterOutputsKernel);
        //ComputeHelper.Dispatch(cs, count, kernelIndex: CopyBackKernel);

    }



}


