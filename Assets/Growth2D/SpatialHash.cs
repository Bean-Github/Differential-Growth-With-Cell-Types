using UnityEngine;
using System.Collections.Generic;

// class to store nodes in a spatial hash for efficient neighbor lookup
public class SpatialHash
{
    public float cellSize;

    // stores nodes in a dictionary keyed by grid cell coordinates
    public Dictionary<Vector2Int, List<Node2D>> hash;
    public List<Node2D> nodes;  // list of all nodes

    public SpatialHash(float cellSize)
    {
        this.cellSize = cellSize;
        hash = new Dictionary<Vector2Int, List<Node2D>>();
        nodes = new List<Node2D>();
    }

    public void AddNode(Node2D node)
    {
        Vector2Int cell = GetCellCoords(node.position);
        if (!hash.ContainsKey(cell))
        {
            hash[cell] = new List<Node2D>();
        }
        hash[cell].Add(node);

        nodes.Add(node);
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

        nodes.Remove(node);
    }

    public Node2D GetNode(int id)
    {
        return nodes[id];
    }

    // gets all nodes in the same and adjacent cells
    public List<Node2D> GetNeighbors(Vector2 position)
    {
        Vector2Int cell = GetCellCoords(position);
        List<Node2D> neighbors = new List<Node2D>();
        // check the current cell and adjacent cells
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int adjacentCell = new Vector2Int(cell.x + x, cell.y + y);
                if (hash.ContainsKey(adjacentCell))
                {
                    neighbors.AddRange(hash[adjacentCell]);
                }
            }
        }
        return neighbors;
    }

    public Node2D[] GetAllWithinDistance(Node2D node2D, float distance)
    {
        List<Node2D> nearbyNodes = new List<Node2D>();
        List<Node2D> candidates = GetNeighbors(node2D.position);
        float distSqr = distance * distance;
        foreach (Node2D other in candidates)
        {
            if (other.id == node2D.id)
            {
                continue;
            }
            float sqrMag = (other.position - node2D.position).sqrMagnitude;
            if (sqrMag <= distSqr)
            {
                nearbyNodes.Add(other);
            }
        }
        return nearbyNodes.ToArray();
    }


    public Vector2Int GetCellCoords(Vector2 position)
    {
        int x = Mathf.FloorToInt(position.x / cellSize);
        int y = Mathf.FloorToInt(position.y / cellSize);
        return new Vector2Int(x, y);
    }


}
