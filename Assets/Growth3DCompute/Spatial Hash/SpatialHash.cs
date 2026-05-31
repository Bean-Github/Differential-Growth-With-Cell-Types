using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class SpatialHashComputeRunner : MonoBehaviour
{
    public ComputeShader spatialHashCompute;

    [Header("Debug")]
    public bool showDebugGrid = false; // ONLY enable this when debugging! It causes lag.

    private Node3D[] debugNodeData;

    float cellSize;
    //Bounds bounds;

    public void SetValues(float cellSize)
    {
        this.cellSize = cellSize;
        spatialHashCompute.SetFloat("cellSize", cellSize);
    }

    int clearStartIndicesKernel;
    int createSpatialLookupKernel;
    int sortKernel;
    int calculateStartIndicesKernel;

    int nodeCount;
    int paddedNodeCount;
    int hashTableSize;

    private void Start()
    {
        clearStartIndicesKernel = spatialHashCompute.FindKernel("ClearStartIndices");
        createSpatialLookupKernel = spatialHashCompute.FindKernel("CreateSpatialLookup");
        sortKernel = spatialHashCompute.FindKernel("SortSpatialLookup");
        calculateStartIndicesKernel = spatialHashCompute.FindKernel("CalculateStartIndices");
    }

    public void UpdateSpatialLookup(ref ComputeBuffer particleBuffer, ref ComputeBuffer spatialLookupBuffer, ref ComputeBuffer startIndicesBuffer, int activeNodeCount)
    {
        nodeCount = activeNodeCount;
        paddedNodeCount = Mathf.NextPowerOfTwo(nodeCount);
        hashTableSize = startIndicesBuffer.count;

        spatialHashCompute.SetBuffer(clearStartIndicesKernel, "startIndices", startIndicesBuffer);

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
        spatialHashCompute.SetInt("nodeCount", nodeCount);               // Small
        spatialHashCompute.SetInt("paddedNodeCount", paddedNodeCount);   // Small Padded
        spatialHashCompute.SetInt("hashTableSize", hashTableSize);       // Massive

        Dispatch();

        if (showDebugGrid && nodeCount > 0)
        {
            if (debugNodeData == null || debugNodeData.Length != nodeCount)
            {
                debugNodeData = new Node3D[nodeCount];
            }
            // Pulling data from the GPU is a heavy operation, hence the boolean toggle!
            particleBuffer.GetData(debugNodeData, 0, 0, nodeCount);
        }
    }


    // do all this within compute shader
    // Based on positions, decide which particles are in which spatial cells.
    private void Dispatch()
    {
        int clearGroups = Mathf.CeilToInt(hashTableSize / 64f);
        spatialHashCompute.Dispatch(clearStartIndicesKernel, clearGroups, 1, 1);

        spatialHashCompute.Dispatch(createSpatialLookupKernel, Mathf.CeilToInt(paddedNodeCount / 64f), 1, 1); // 64 threads per group?

        // Sort by cell key!!
        DispatchSort();

        // Calculate start indices of each unique cell key in the spatial lookup
        spatialHashCompute.Dispatch(calculateStartIndicesKernel, Mathf.CeilToInt(paddedNodeCount / 64f), 1, 1); // 64 threads per group

    }

    // Bitonic sort in compute shader, sort by cell key, so that particles in the same cell are adjacent in the buffer.
    void DispatchSort()
    {
        int numPairs = Mathf.CeilToInt(paddedNodeCount / 2.0f);

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

            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGrid || debugNodeData == null || !Application.isPlaying) return;

        // Group particles into cells on the CPU using the same math as the shader
        Dictionary<Vector3Int, int> gridCounts = new Dictionary<Vector3Int, int>();

        for (int i = 0; i < nodeCount; i++)
        {
            Vector3 pos = debugNodeData[i].position;

            // Replicate HLSL floor math
            Vector3Int cell3D = new Vector3Int(
                Mathf.FloorToInt(pos.x / cellSize),
                Mathf.FloorToInt(pos.y / cellSize),
                Mathf.FloorToInt(pos.z / cellSize)
            );

            if (!gridCounts.ContainsKey(cell3D))
            {
                gridCounts[cell3D] = 0;
            }
            gridCounts[cell3D]++;
        }

        // Draw the cubes and text
        Gizmos.color = Color.cyan;
        foreach (var kvp in gridCounts)
        {
            Vector3Int cell = kvp.Key;
            int count = kvp.Value;

            // Calculate the exact world space bounds of this specific cell
            Vector3 worldPos = new Vector3(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize);
            Vector3 center = worldPos + (Vector3.one * (cellSize * 0.5f));

            // Draw a neat wire cube using Unity's built in Gizmo system
            Gizmos.DrawWireCube(center, Vector3.one * cellSize);

#if UNITY_EDITOR
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.yellow;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 12;
            labelStyle.fontStyle = FontStyle.Bold;

            // Draw particle count in the center of the cell
            Handles.Label(center, count.ToString(), labelStyle);

            // Draw coordinates slightly below
            Vector3 labelCenter = center - new Vector3(0, cellSize * 0.25f, 0);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 10;
            Handles.Label(labelCenter, $"({cell.x}, {cell.y}, {cell.z})", labelStyle);
#endif
        }
    }

}


