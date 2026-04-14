using System.Collections.Generic;
using UnityEngine;

namespace Growth2D
{
    // stores all your edges, one can get an edge by the two nodes it connects
    public class EdgeList2D
    {
        public Dictionary<(int, int), Edge2D> edges;
        public List<Edge2D> allEdges;

        int m_nextID;

        public EdgeList2D()
        {
            edges = new Dictionary<(int, int), Edge2D>();
            allEdges = new List<Edge2D>();
            m_nextID = 0;
        }

        public Edge2D GetEdge(Node2D nodeA, Node2D nodeB)
        {
            var key = GetEdgeKey(nodeA, nodeB);
            if (edges.ContainsKey(key))
            {
                return edges[key];
            }
            return null;
        }

        public void AddEdge(Node2D nodeA, Node2D nodeB)
        {
            var key = GetEdgeKey(nodeA, nodeB);
            if (!edges.ContainsKey(key))
            {
                nodeA.AddNeighbor(nodeB);
                nodeB.AddNeighbor(nodeA);

                edges[key] = new Edge2D(nodeA, nodeB, m_nextID);
                allEdges.Add(edges[key]);
                m_nextID++;
            }
        }

        public void RemoveEdge(Node2D nodeA, Node2D nodeB)
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
        public void SplitEdge(Edge2D edge2D, Node2D newNode)
        {
            Node2D nodeA = edge2D.nodeA;
            Node2D nodeB = edge2D.nodeB;

            // Remove old edge properly
            RemoveEdge(nodeA, nodeB);

            // Add the two new edges
            AddEdge(nodeA, newNode);
            AddEdge(newNode, nodeB);
        }

        public static (int, int) GetEdgeKey(Node2D nodeA, Node2D nodeB)
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

