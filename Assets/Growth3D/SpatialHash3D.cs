using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Growth3D
{
    // class to store nodes in a spatial hash for efficient neighbor lookup
    public class SpatialHash3D
    {
        public float cellSize;

        // stores nodes in a dictionary keyed by grid cell coordinates
        public Dictionary<Vector3Int, List<Node3D>> hash;
        public List<Node3D> allNodes;  // list of all nodes

        public SpatialHash3D(float cellSize)
        {
            this.cellSize = cellSize;
            hash = new Dictionary<Vector3Int, List<Node3D>>();
            allNodes = new List<Node3D>();
        }

        public void UpdateNode(Node3D node)
        {
            Vector3Int newCell = GetCellCoords(node.position);

            if (newCell == node.currentCell)
                return;

            // remove from old cell
            if (hash.TryGetValue(node.currentCell, out var oldList))
            {
                oldList.Remove(node);
                if (oldList.Count == 0)
                    hash.Remove(node.currentCell);
            }

            // add to new cell
            if (!hash.ContainsKey(newCell))
                hash[newCell] = new List<Node3D>();

            hash[newCell].Add(node);

            node.currentCell = newCell;
        }

        // add a node to the hash
        public void AddNode(Node3D node)
        {
            Vector3Int cell = GetCellCoords(node.position);
            node.currentCell = cell;

            // add to cell list
            if (!hash.ContainsKey(cell))
            {
                hash[cell] = new List<Node3D>();
            }
            hash[cell].Add(node);
            // add to global list
            allNodes.Add(node);
        }

        public void RemoveNode(Node3D node)
        {
            Vector3Int cell = GetCellCoords(node.position);

            if (hash.ContainsKey(cell))
            {
                hash[cell].Remove(node);
                if (hash[cell].Count == 0)
                {
                    hash.Remove(cell);
                }
            }

            allNodes.Remove(node);
        }

        public Node3D GetNode(int index)
        {
            return allNodes[index];
        }

        private List<Node3D> _queryResults = new List<Node3D>(1024);

        public List<Node3D> GetNearbyNodes(Node3D node, float distance)
        {
            _queryResults.Clear();
            Vector3Int cell = GetCellCoords(node.position);
            float distSqr = distance * distance;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int adjacentCell = new Vector3Int(cell.x + x, cell.y + y, cell.z + z);

                        if (hash.TryGetValue(adjacentCell, out List<Node3D> cellNodes))
                        {
                            // Iterate the cell directly
                            for (int i = 0; i < cellNodes.Count; i++)
                            {
                                Node3D other = cellNodes[i];
                                if (other == node) continue;

                                if ((other.position - node.position).sqrMagnitude <= distSqr)
                                {
                                    _queryResults.Add(other);
                                }
                            }
                        }
                    }

                }
            }
            return _queryResults;
        }

        public Vector3Int GetCellCoords(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x / cellSize);
            int y = Mathf.FloorToInt(position.y / cellSize);
            int z = Mathf.FloorToInt(position.z / cellSize);
            return new Vector3Int(x, y, z);
        }


        // DEBUG: visualize the grid in the editor
        public void DebugDrawGrid()
        {
            foreach (var kvp in hash)
            {
                Vector3Int cell = kvp.Key;

                Vector3 worldPos = new Vector3(
                    cell.x * cellSize,
                    cell.y * cellSize,
                    cell.z * cellSize
                );

                DrawCube(worldPos, cellSize, Color.cyan);

                // Optional: draw node count
                int count = kvp.Value.Count;
                Vector3 center = new Vector3(
                    worldPos.x + cellSize * 0.5f,
                    worldPos.y + cellSize * 0.5f,
                    worldPos.z + cellSize * 0.5f
                );
#if UNITY_EDITOR
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = Color.yellow;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 12;
                labelStyle.fontStyle = FontStyle.Bold;

                // pass the 3D world space 'center' directly.
                Handles.Label(center, count.ToString(), labelStyle);

                Vector3 labelCenter = center - new Vector3(0, cellSize * 0.25f, 0);

                labelStyle.normal.textColor = Color.white;
                Handles.Label(labelCenter, $"({cell.x}, {cell.y}, {cell.z})", labelStyle);
#endif
            }
        }

        // Converted from DrawCell to draw a full 3D wireframe cube
        private void DrawCube(Vector3 bottomLeftBack, float size, Color color)
        {
            // Calculate all 8 corners of the cube
            Vector3 p0 = bottomLeftBack;
            Vector3 p1 = p0 + new Vector3(size, 0, 0);
            Vector3 p2 = p0 + new Vector3(size, size, 0);
            Vector3 p3 = p0 + new Vector3(0, size, 0);

            Vector3 p4 = p0 + new Vector3(0, 0, size);
            Vector3 p5 = p1 + new Vector3(0, 0, size);
            Vector3 p6 = p2 + new Vector3(0, 0, size);
            Vector3 p7 = p3 + new Vector3(0, 0, size);

            // Front Face (Z)
            Debug.DrawLine(p0, p1, color);
            Debug.DrawLine(p1, p2, color);
            Debug.DrawLine(p2, p3, color);
            Debug.DrawLine(p3, p0, color);

            // Back Face (Z + size)
            Debug.DrawLine(p4, p5, color);
            Debug.DrawLine(p5, p6, color);
            Debug.DrawLine(p6, p7, color);
            Debug.DrawLine(p7, p4, color);

            // Connecting Lines (Depth)
            Debug.DrawLine(p0, p4, color);
            Debug.DrawLine(p1, p5, color);
            Debug.DrawLine(p2, p6, color);
            Debug.DrawLine(p3, p7, color);
        }
    }

}