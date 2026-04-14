using System.Collections.Generic;
using UnityEngine;

namespace Growth3D
{
    // THE MASTER STORER OF NODES AND EDGES
    // stores all nodes and edges, and provides methods for adding new nodes and connecting them to neighbors
    public class NodeHoard3D
    {
        // storage
        private SpatialHash3D m_nodeHash;
        private EdgeList3D m_edgeList;

        public NodeHoard3D(float separationDistance)
        {
            m_nodeHash = new SpatialHash3D(separationDistance); // cell size of 1 unit
            m_edgeList = new EdgeList3D();
        }

        #region Debug
        public void DebugDrawNodes()
        {
            // debug draw lines between nodes and draw a cross at each node position
            foreach (Node3D node in allNodes)
            {
                foreach (var neighbor in node.neighbors.Values)
                {
                    float intensity = m_edgeList.GetEdge(node, neighbor).Curvature;
                    Color col = new Color(0.0f, intensity, 0.0f);
                    Debug.DrawLine(node.position, neighbor.position, col);

                    Debug.DrawLine(node.position + Vector3.up * 0.1f, node.position + Vector3.down * 0.1f, Color.white);
                    Debug.DrawLine(node.position + Vector3.left * 0.1f, node.position + Vector3.right * 0.1f, Color.white);
                }
            }
        }

        public void DebugDrawGrid()
        {
            m_nodeHash.DebugDrawGrid();
        }
        #endregion

        #region Getters
        public IReadOnlyList<Node3D> allNodes => m_nodeHash.allNodes;
        public int numNodes
        {
            get => m_nodeHash.allNodes.Count;
            private set { }
        }
    
        public IReadOnlyList<Edge3D> allEdges => m_edgeList.allEdges;
        public int numEdges
        {
            get => allEdges.Count;
            private set { }
        }
        #endregion

        #region Node Management
        public Node3D GetNode(int index)
        {
            return m_nodeHash.GetNode(index);
        }

        public List<Node3D> GetNearbyNodesWithinDistance(Node3D node, float dist)
        {
            return m_nodeHash.GetNearbyNodes(node, dist);
        }

        public Node3D AddNode(Vector3 position)
        {
            Node3D newNode = new Node3D(position);
            m_nodeHash.AddNode(newNode);

            return newNode;
        }

        public void RemoveNode(Node3D node)
        {
            // remove all edges connected to this node
            foreach (var neighbor in node.neighbors.Values)
            {
                RemoveEdge(node, neighbor);
            }
            m_nodeHash.RemoveNode(node);
        }

        // add a node in the middle of an edge, inheriting average velocity and connecting to the same neighbors
        public void InsertNode(Edge3D edge)
        {
            Node3D nodeA = edge.nodeA;
            Node3D nodeB = edge.nodeB;

            if (nodeA.neighbors.ContainsKey(nodeB.id) == false || nodeB.neighbors.ContainsKey(nodeA.id) == false)
            {
                Debug.LogWarning("Attempted to insert node between non-neighboring nodes");
                return;
            }

            // Create a new node in the middle of the edge
            Vector3 midPoint = (nodeA.position + nodeB.position) / 2.0f;
            Node3D newNode = AddNode(midPoint);
            newNode.currVelocity = (nodeA.currVelocity + nodeB.currVelocity) / 2.0f; // inherit average velocity

            // Split the edge
            SplitEdge(edge, newNode);

            Debug.DrawLine(nodeA.position, nodeB.position, Color.red, 1.0f);
        }

        // UPDATES all position, curvature, and spatial hash data for all nodes. Call this once per frame.
        public void UpdateNodeData()
        {
            foreach (Node3D node in allNodes)
            {
                node.UpdatePosition();
                node.CalculateCurvature();
                m_nodeHash.UpdateNode(node);
            }
        }
        #endregion

        #region Edge Management
        public void AddEdge(Node3D nodeA, Node3D nodeB)
        {
            m_edgeList.AddEdge(nodeA, nodeB);
        }

        public void RemoveEdge(Node3D nodeA, Node3D nodeB)
        {
            m_edgeList.RemoveEdge(nodeA, nodeB);
        }

        public void SplitEdge(Edge3D edge, Node3D newNode)
        {
            m_edgeList.SplitEdge(edge, newNode);
        }

        public Edge3D GetEdge(Node3D nodeA, Node3D nodeB)
        {
            return m_edgeList.GetEdge(nodeA, nodeB);
        }

        public Edge3D GetRandomEdge()
        {
            int edgeIndex = Random.Range(0, numEdges);

            return allEdges[edgeIndex];
        }
        #endregion
    }
}

