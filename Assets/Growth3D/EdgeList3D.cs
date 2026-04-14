using System.Collections.Generic;
using UnityEngine;

namespace Growth3D
{
    // stores all your edges, one can get an edge by the two nodes it connects
    public class EdgeList3D
    {
        public Dictionary<(int, int), Edge3D> edges;
        public List<Edge3D> allEdges;

        int m_nextID;

        public EdgeList3D()
        {
            edges = new Dictionary<(int, int), Edge3D>();
            allEdges = new List<Edge3D>();
            m_nextID = 0;
        }

        public Edge3D GetEdge(Node3D nodeA, Node3D nodeB)
        {
            var key = GetEdgeKey(nodeA, nodeB);
            if (edges.ContainsKey(key))
            {
                return edges[key];
            }
            return null;
        }

        public void AddEdge(Node3D nodeA, Node3D nodeB)
        {
            var key = GetEdgeKey(nodeA, nodeB);
            if (!edges.ContainsKey(key))
            {
                nodeA.AddNeighbor(nodeB);
                nodeB.AddNeighbor(nodeA);

                edges[key] = new Edge3D(nodeA, nodeB, m_nextID);
                allEdges.Add(edges[key]);
                m_nextID++;
            }
        }

        public void RemoveEdge(Node3D nodeA, Node3D nodeB)
        {
            var key = GetEdgeKey(nodeA, nodeB);
            if (edges.TryGetValue(key, out var edgeToRemove))
            {
                nodeA.RemoveNeighbor(nodeB);
                nodeB.RemoveNeighbor(nodeA);

                // Fast List Removal (Swap and Pop)
                int indexToRemove = allEdges.IndexOf(edgeToRemove);
                int lastIndex = allEdges.Count - 1;

                if (indexToRemove != lastIndex)
                {
                    allEdges[indexToRemove] = allEdges[lastIndex];
                }

                allEdges.RemoveAt(lastIndex);

                edges.Remove(key);
            }
        }

        // splits an edge into two edges by placing a new node in the middle
        // A->B becomes A->newNode (old edge) and newNode->B (new created edge)
        public void SplitEdge(Edge3D edge2D, Node3D newNode)
        {
            Node3D nodeA = edge2D.nodeA;
            Node3D nodeB = edge2D.nodeB;

            // Remove old edge properly
            RemoveEdge(nodeA, nodeB);

            // Add the two new edges
            AddEdge(nodeA, newNode);
            AddEdge(newNode, nodeB);
        }

        public static (int, int) GetEdgeKey(Node3D nodeA, Node3D nodeB)
        {
            if (nodeA == null || nodeB == null)
            {
                Debug.LogError("Attempted to get edge key for null nodes");
                return (-1, -1);
            }

            return (Mathf.Min(nodeA.id, nodeB.id), Mathf.Max(nodeA.id, nodeB.id));
        }

    }


}

