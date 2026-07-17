using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Growth2D
{
    // class to store nodes in a spatial hash for efficient neighbor lookup
    public class SpatialHash2D
    {
        public float cellSize;

        // stores nodes in a dictionary keyed by grid cell coordinates
        public Dictionary<Vector2Int, List<Node2D>> hash;
        public List<Node2D> allNodes;  // list of all nodes

        public SpatialHash2D(float cellSize)
        {
            this.cellSize = cellSize;
            hash = new Dictionary<Vector2Int, List<Node2D>>();
            allNodes = new List<Node2D>();
        }

        public void UpdateNode(Node2D node)
        {
            Vector2Int newCell = GetCellCoords(node.position);

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
                hash[newCell] = new List<Node2D>();

            hash[newCell].Add(node);

            node.currentCell = newCell;
        }

        // add a node to the hash
        public void AddNode(Node2D node)
        {
            Vector2Int cell = GetCellCoords(node.position);
            node.currentCell = cell;

            // add to cell list
            if (!hash.ContainsKey(cell))
            {
                hash[cell] = new List<Node2D>();
            }
            hash[cell].Add(node);
            // add to global list
            allNodes.Add(node);
        }

        public void RemoveNode(Node2D node)
        {
            Vector2Int cell = GetCellCoords(node.position);

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

        public Node2D GetNode(int index)
        {
            return allNodes[index];
        }

        //// gets all nodes in the same and adjacent cells
        //public List<Node2D> GetNearbyNodes(Vector2 position)
        //{
        //    Vector2Int cell = GetCellCoords(position);
        //    List<Node2D> neighbors = new List<Node2D>();
        //    // check the current cell and adjacent cells
        //    for (int x = -1; x <= 1; x++)
        //    {
        //        for (int y = -1; y <= 1; y++)
        //        {
        //            Vector2Int adjacentCell = new Vector2Int(cell.x + x, cell.y + y);
        //            if (hash.ContainsKey(adjacentCell))
        //            {
        //                neighbors.AddRange(hash[adjacentCell]);
        //            }
        //        }
        //    }
        //    return neighbors;
        //}

        private List<Node2D> _queryResults = new List<Node2D>(1024);

        public List<Node2D> GetNearbyNodes(Node2D node, float distance)
        {
            _queryResults.Clear();
            Vector2Int cell = GetCellCoords(node.position);
            float distSqr = distance * distance;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int adjacentCell = new Vector2Int(cell.x + x, cell.y + y);

                    // Use TryGetValue to avoid double-lookup
                    if (hash.TryGetValue(adjacentCell, out List<Node2D> cellNodes))
                    {
                        // Iterate the cell directly
                        for (int i = 0; i < cellNodes.Count; i++)
                        {
                            Node2D other = cellNodes[i];
                            if (other == node) continue;

                            if ((other.position - node.position).sqrMagnitude <= distSqr)
                            {
                                _queryResults.Add(other);
                            }
                        }
                    }
                }
            }
            return _queryResults;
        }

        public Vector2Int GetCellCoords(Vector2 position)
        {
            int x = Mathf.FloorToInt(position.x / cellSize);
            int y = Mathf.FloorToInt(position.y / cellSize);
            return new Vector2Int(x, y);
        }


        // DEBUG: visualize the grid in the editor
        public void DebugDrawGrid()
        {
            foreach (var kvp in hash)
            {
                Vector2Int cell = kvp.Key;

                Vector2 worldPos = new Vector2(
                    cell.x * cellSize,
                    cell.y * cellSize
                );

                DrawCell(worldPos, cellSize, Color.cyan);

                // Optional: draw node count as vertical line
                int count = kvp.Value.Count;
                Vector3 center = new Vector3(
                    worldPos.x + cellSize * 0.5f,
                    worldPos.y + cellSize * 0.5f,
                    0
                );
#if UNITY_EDITOR
                // Optional: Create a GUIStyle to make the text actually readable 
                // (default Handles text is small and black/grey)
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = Color.yellow;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 12;
                labelStyle.fontStyle = FontStyle.Bold;

                // 1. Pass the world space 'center' directly. No camera conversions needed!
                Handles.Label(center, count.ToString(), labelStyle);

                // 2. Offset the second label slightly down in WORLD SPACE so they don't overlap
                Vector3 offsetCenter = center - new Vector3(0, cellSize * 0.25f, 0);

                // Override color for the coordinate text to distinguish it from the count
                labelStyle.normal.textColor = Color.white;
                Handles.Label(offsetCenter, $"({cell.x}, {cell.y})", labelStyle);
#endif
            }
        }

        private void DrawCell(Vector2 bottomLeft, float size, Color color)
        {
            Vector3 bl = new Vector3(bottomLeft.x, bottomLeft.y, 0);
            Vector3 br = bl + new Vector3(size, 0, 0);
            Vector3 tr = bl + new Vector3(size, size, 0);
            Vector3 tl = bl + new Vector3(0, size, 0);

            Debug.DrawLine(bl, br, color);
            Debug.DrawLine(br, tr, color);
            Debug.DrawLine(tr, tl, color);
            Debug.DrawLine(tl, bl, color);
        }
    }

}