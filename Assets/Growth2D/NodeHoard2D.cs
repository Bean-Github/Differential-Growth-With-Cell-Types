using System.Collections.Generic;
using UnityEngine;

namespace Growth2D
{
    // THE MASTER STORER OF NODES AND EDGES
    // stores all nodes and edges, and provides methods for adding new nodes and connecting them to neighbors
    public class NodeHoard2D
    {
        // storage
        private SpatialHash2D m_nodeHash;
        private EdgeList2D m_edgeList;

        public NodeHoard2D(float separationDistance)
        {
            m_nodeHash = new SpatialHash2D(separationDistance); // cell size of 1 unit
            m_edgeList = new EdgeList2D();
        }

        #region Debug
        public void DebugDrawNodes()
        {
            // debug draw lines between nodes and draw a cross at each node position
            foreach (Node2D node in allNodes)
            {
                foreach (var neighbor in node.neighbors.Values)
                {
                    float intensity = m_edgeList.GetEdge(node, neighbor).Curvature;
                    Color col = new Color(0.0f, intensity, 0.0f);
                    Debug.DrawLine(node.position, neighbor.position, col);

                    Debug.DrawLine(node.position + Vector2.up * 0.1f, node.position + Vector2.down * 0.1f, Color.white);
                    Debug.DrawLine(node.position + Vector2.left * 0.1f, node.position + Vector2.right * 0.1f, Color.white);
                }
            }
        }

        public void DebugDrawGrid()
        {
            m_nodeHash.DebugDrawGrid();
        }
        #endregion

        #region Getters
        public IReadOnlyList<Node2D> allNodes => m_nodeHash.allNodes;
        public int numNodes
        {
            get => m_nodeHash.allNodes.Count;
            private set { }
        }
    
        public IReadOnlyList<Edge2D> allEdges => m_edgeList.allEdges;
        public int numEdges
        {
            get => allEdges.Count;
            private set { }
        }
        #endregion

        #region Node Management
        public Node2D GetNode(int index)
        {
            return m_nodeHash.GetNode(index);
        }

        public List<Node2D> GetNearbyNodesWithinDistance(Node2D node, float dist)
        {
            return m_nodeHash.GetNearbyNodes(node, dist);
        }

        public Node2D AddNode(Vector2 position)
        {
            Node2D newNode = new Node2D(position);
            m_nodeHash.AddNode(newNode);

            return newNode;
        }

        public void RemoveNode(Node2D node)
        {
            // remove all edges connected to this node
            foreach (var neighbor in node.neighbors.Values)
            {
                RemoveEdge(node, neighbor);
            }
            m_nodeHash.RemoveNode(node);
        }

        // add a node in the middle of an edge, inheriting average velocity and connecting to the same neighbors
        public void InsertNode(Edge2D edge)
        {
            Node2D nodeA = edge.nodeA;
            Node2D nodeB = edge.nodeB;

            if (nodeA.neighbors.ContainsKey(nodeB.id) == false || nodeB.neighbors.ContainsKey(nodeA.id) == false)
            {
                Debug.LogWarning("Attempted to insert node between non-neighboring nodes");
                return;
            }

            // Create a new node in the middle of the edge
            Vector2 midPoint = (nodeA.position + nodeB.position) / 2.0f;
            Node2D newNode = AddNode(midPoint);
            newNode.currVelocity = (nodeA.currVelocity + nodeB.currVelocity) / 2.0f; // inherit average velocity

            // Split the edge
            SplitEdge(edge, newNode);

            Debug.DrawLine(nodeA.position, nodeB.position, Color.red, 1.0f);
        }

        // UPDATES all position, curvature, and spatial hash data for all nodes. Call this once per frame.
        public void UpdateNodeData()
        {
            foreach (Node2D node in allNodes)
            {
                node.UpdatePosition();
                node.CalculateCurvature();
                m_nodeHash.UpdateNode(node);
            }
        }
        #endregion

        #region Edge Management
        public void AddEdge(Node2D nodeA, Node2D nodeB)
        {
            m_edgeList.AddEdge(nodeA, nodeB);
        }

        public void RemoveEdge(Node2D nodeA, Node2D nodeB)
        {
            m_edgeList.RemoveEdge(nodeA, nodeB);
        }

        public void SplitEdge(Edge2D edge, Node2D newNode)
        {
            m_edgeList.SplitEdge(edge, newNode);
        }

        public Edge2D GetEdge(Node2D nodeA, Node2D nodeB)
        {
            return m_edgeList.GetEdge(nodeA, nodeB);
        }

        public Edge2D GetRandomEdge()
        {
            int edgeIndex = Random.Range(0, numEdges);

            return allEdges[edgeIndex];
        }
        #endregion
    }
}

