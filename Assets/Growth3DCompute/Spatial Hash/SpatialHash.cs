using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class SpatialHashComputeRunner : MonoBehaviour
{
    public ComputeShader spatialHashCompute;

    float cellSize;
    Bounds bounds;

    public void SetValues(float cellSize, Bounds bounds)
    {
        this.cellSize = cellSize;
        this.bounds = bounds;

        spatialHashCompute.SetFloat("smoothingRadius", cellSize);
        spatialHashCompute.SetVector("boundsCenter", bounds.center);
        spatialHashCompute.SetVector("boundsExtents", bounds.extents);
    }

    int createSpatialLookupKernel;
    int sortKernel;
    int calculateStartIndicesKernel;

    int nodeCount;

    private void Start()
    {
        createSpatialLookupKernel = spatialHashCompute.FindKernel("CreateSpatialLookup");
        sortKernel = spatialHashCompute.FindKernel("SortSpatialLookup");
        calculateStartIndicesKernel = spatialHashCompute.FindKernel("CalculateStartIndices");
    }

    public void UpdateSpatialLookup(ref ComputeBuffer particleBuffer, ref ComputeBuffer spatialLookupBuffer, ref ComputeBuffer startIndicesBuffer)
    {
        nodeCount = particleBuffer.count;
        //powerOfTwoCount = Mathf.NextPowerOfTwo(nodeCount);

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
        spatialHashCompute.SetInt("nodeCount", nodeCount);
        //spatialHashCompute.SetInt("paddedNodeCount", powerOfTwoCount);

        Dispatch();
    }


    // do all this within compute shader
    // Based on positions, decide which particles are in which spatial cells.
    private void Dispatch()
    {
        spatialHashCompute.Dispatch(createSpatialLookupKernel, Mathf.CeilToInt(nodeCount / 64f), 1, 1); // 64 threads per group?

        // Sort by cell key!!
        DispatchSort();

        // Calculate start indices of each unique cell key in the spatial lookup
        spatialHashCompute.Dispatch(calculateStartIndicesKernel, Mathf.CeilToInt(nodeCount / 64f), 1, 1); // 64 threads per group

    }

    // Bitonic sort in compute shader, sort by cell key, so that particles in the same cell are adjacent in the buffer.
    void DispatchSort()
    {
        int numPairs = Mathf.CeilToInt(nodeCount / 2.0f);

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

//    // DEBUG: visualize the grid in the editor
//    public void DebugDrawGrid()
//    {
//        foreach (var kvp in hash)
//        {
//            Vector3Int cell = kvp.Key;

//            Vector3 worldPos = new Vector3(
//                cell.x * cellSize,
//                cell.y * cellSize,
//                cell.z * cellSize
//            );

//            DrawCube(worldPos, cellSize, Color.cyan);

//            // Optional: draw node count
//            int count = kvp.Value.Count;
//            Vector3 center = new Vector3(
//                worldPos.x + cellSize * 0.5f,
//                worldPos.y + cellSize * 0.5f,
//                worldPos.z + cellSize * 0.5f
//            );
//#if UNITY_EDITOR
//            GUIStyle labelStyle = new GUIStyle();
//            labelStyle.normal.textColor = Color.yellow;
//            labelStyle.alignment = TextAnchor.MiddleCenter;
//            labelStyle.fontSize = 12;
//            labelStyle.fontStyle = FontStyle.Bold;

//            // pass the 3D world space 'center' directly.
//            Handles.Label(center, count.ToString(), labelStyle);

//            Vector3 labelCenter = center - new Vector3(0, cellSize * 0.25f, 0);

//            labelStyle.normal.textColor = Color.white;
//            Handles.Label(labelCenter, $"({cell.x}, {cell.y}, {cell.z})", labelStyle);
//#endif
//        }
//    }

//    // Converted from DrawCell to draw a full 3D wireframe cube
//    private void DrawCube(Vector3 bottomLeftBack, float size, Color color)
//    {
//        // Calculate all 8 corners of the cube
//        Vector3 p0 = bottomLeftBack;
//        Vector3 p1 = p0 + new Vector3(size, 0, 0);
//        Vector3 p2 = p0 + new Vector3(size, size, 0);
//        Vector3 p3 = p0 + new Vector3(0, size, 0);

//        Vector3 p4 = p0 + new Vector3(0, 0, size);
//        Vector3 p5 = p1 + new Vector3(0, 0, size);
//        Vector3 p6 = p2 + new Vector3(0, 0, size);
//        Vector3 p7 = p3 + new Vector3(0, 0, size);

//        // Front Face (Z)
//        Debug.DrawLine(p0, p1, color);
//        Debug.DrawLine(p1, p2, color);
//        Debug.DrawLine(p2, p3, color);
//        Debug.DrawLine(p3, p0, color);

//        // Back Face (Z + size)
//        Debug.DrawLine(p4, p5, color);
//        Debug.DrawLine(p5, p6, color);
//        Debug.DrawLine(p6, p7, color);
//        Debug.DrawLine(p7, p4, color);

//        // Connecting Lines (Depth)
//        Debug.DrawLine(p0, p4, color);
//        Debug.DrawLine(p1, p5, color);
//        Debug.DrawLine(p2, p6, color);
//        Debug.DrawLine(p3, p7, color);
//    }

}


