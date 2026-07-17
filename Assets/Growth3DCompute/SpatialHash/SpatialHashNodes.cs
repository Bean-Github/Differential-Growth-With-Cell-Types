using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace Growth3DCompute
{

    public class SpatialHashNodes : SpatialHashComputeRunner
    {
        private Node3D[] debugNodeData;


        public override void UpdateSpatialLookup(ref ComputeBuffer particleBuffer, ref ComputeBuffer spatialLookupBuffer, ref ComputeBuffer startIndicesBuffer, int activeNodeCount)
        {
            base.UpdateSpatialLookup(ref particleBuffer, ref spatialLookupBuffer, ref startIndicesBuffer, activeNodeCount);



            if (showDebugGrid && elementCount > 0)
            {
                if (debugNodeData == null || debugNodeData.Length != elementCount)
                {
                    debugNodeData = new Node3D[elementCount];
                }
                // Pulling data from the GPU is a heavy operation, hence the boolean toggle!
                particleBuffer.GetData(debugNodeData, 0, 0, elementCount);
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGrid || debugNodeData == null || !Application.isPlaying) return;

            // Group particles into cells on the CPU using the same math as the shader
            Dictionary<Vector3Int, int> gridCounts = new Dictionary<Vector3Int, int>();

            for (int i = 0; i < elementCount; i++)
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
}
