using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Growth3DCompute
{
    public class SpatialHashFaces : SpatialHashComputeRunner
    {
        private Face3D[] debugFaceData;
        private HalfEdge3D[] debugHalfEdgeData;
        private Node3D[] debugNodeData;

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
                    debugFaceData = new Face3D[elementCount];
                faceBuffer.GetData(debugFaceData, 0, 0, elementCount);

                int heCount = grower3DComputeRunner.halfEdgeCount;
                if (debugHalfEdgeData == null || debugHalfEdgeData.Length != heCount)
                    debugHalfEdgeData = new HalfEdge3D[heCount];
                grower3DComputeRunner.halfEdgeBuffer.GetData(debugHalfEdgeData, 0, 0, heCount);

                int nCount = grower3DComputeRunner.nodeCount;
                if (debugNodeData == null || debugNodeData.Length != nCount)
                    debugNodeData = new Node3D[nCount];
                grower3DComputeRunner.nodeBuffer.GetData(debugNodeData, 0, 0, nCount);
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

        private void OnDrawGizmos()
        {
            if (!showDebugGrid || debugFaceData == null || debugHalfEdgeData == null || debugNodeData == null || !Application.isPlaying) return;

            // Group faces into cells on the CPU using the same bounding box math as the shader
            Dictionary<Vector3Int, int> gridCounts = new Dictionary<Vector3Int, int>();

            for (int i = 0; i < elementCount; i++)
            {
                Face3D face = debugFaceData[i];

                // Replicate the HLSL Half-Edge traversal to get the 3 vertices
                uint edge1 = face.halfEdge;
                uint edge2 = debugHalfEdgeData[edge1].next;
                uint edge3 = debugHalfEdgeData[edge2].next;

                uint nA = debugHalfEdgeData[edge1].target;
                uint nB = debugHalfEdgeData[edge2].target;
                uint nC = debugHalfEdgeData[edge3].target;

                Vector3 a = debugNodeData[nA].position;
                Vector3 b = debugNodeData[nB].position;
                Vector3 c = debugNodeData[nC].position;

                // Find the bounding box of the face
                Vector3 minPos = Vector3.Min(a, Vector3.Min(b, c));
                Vector3 maxPos = Vector3.Max(a, Vector3.Max(b, c));

                // Replicate HLSL floor math for min and max cells
                Vector3Int minCell = new Vector3Int(
                    Mathf.FloorToInt(minPos.x / cellSize),
                    Mathf.FloorToInt(minPos.y / cellSize),
                    Mathf.FloorToInt(minPos.z / cellSize)
                );

                Vector3Int maxCell = new Vector3Int(
                    Mathf.FloorToInt(maxPos.x / cellSize),
                    Mathf.FloorToInt(maxPos.y / cellSize),
                    Mathf.FloorToInt(maxPos.z / cellSize)
                );

                // Loop through all cells the face overlaps
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    for (int y = minCell.y; y <= maxCell.y; y++)
                    {
                        for (int z = minCell.z; z <= maxCell.z; z++)
                        {
                            Vector3Int cell3D = new Vector3Int(x, y, z);

                            if (!gridCounts.ContainsKey(cell3D))
                            {
                                gridCounts[cell3D] = 0;
                            }
                            gridCounts[cell3D]++;
                        }
                    }
                }
            }

            // Draw the cubes and text
            Gizmos.color = Color.magenta; // Using magenta to differentiate Face grid from Node grid
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
                labelStyle.normal.textColor = Color.magenta;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 12;
                labelStyle.fontStyle = FontStyle.Bold;

                // Draw face count in the center of the cell
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
}


