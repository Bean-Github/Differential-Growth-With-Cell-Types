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
        public List<Edge3D> halfEdges;
        public List<Face3D> faces;
        private Dictionary<long, Edge3D> edgeSutures;

        public NodeHoard3D(float separationDistance)
        {
            m_nodeHash = new SpatialHash3D(separationDistance); // cell size of 1 unit

            halfEdges = new List<Edge3D>();
            faces = new List<Face3D>();
            edgeSutures = new Dictionary<long, Edge3D>();
        }

        #region Debug
        public void DebugDrawNodes()
        {
            // debug draw lines between nodes and draw a cross at each node position
            foreach (Node3D node in allNodes)
            {
                foreach (Edge3D edge in node.edges)
                {
                    float intensity = edge.Curvature;
                    Color col = new Color(0.0f, intensity, 0.0f);
                    Debug.DrawLine(node.position, edge.twin.origin.position, col);

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
        #endregion

        #region Node Management
        public Node3D GetNode(int index)
        {
            return m_nodeHash.GetNode(index);
        }

        public Node3D AddNode(Vector3 position)
        {
            Node3D newNode = new Node3D(position);
            m_nodeHash.AddNode(newNode);
            return newNode;
        }

        public List<Node3D> GetNearbyNodesWithinDistance(Node3D node, float dist)
        {
            return m_nodeHash.GetNearbyNodes(node, dist);
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
        public Face3D AddTriangle(Node3D a, Node3D b, Node3D c)
        {
            Face3D face = new Face3D();
            faces.Add(face);

            Edge3D he1 = new Edge3D { origin = a, face = face };
            Edge3D he2 = new Edge3D { origin = b, face = face };
            Edge3D he3 = new Edge3D { origin = c, face = face };

            halfEdges.AddRange(new[] { he1, he2, he3 });

            he1.next = he2; he1.prev = he3;
            he2.next = he3; he2.prev = he1;
            he3.next = he1; he3.prev = he2;

            face.halfEdge = he1;
            a.halfEdge ??= he1; // if v1 is not null, set its halfedge to he1
            b.halfEdge ??= he2;
            c.halfEdge ??= he3;

            SutureTwin(he1, a, b);
            SutureTwin(he2, b, c);
            SutureTwin(he3, c, a);

            return face;
        }

        private void SutureTwin(Edge3D halfEdge, Node3D from, Node3D to)
        {
            int min = Mathf.Min(from.id, to.id);
            int max = Mathf.Max(from.id, to.id);
            long key = ((long)min << 32) + max;

            // if there exists and edge going to -> from, then that is our twin!
            if (edgeSutures.TryGetValue(key, out Edge3D twin))
            {
                halfEdge.twin = twin;
                twin.twin = halfEdge;
                edgeSutures.Remove(key);
            }
            else
            {
                edgeSutures.Add(key, halfEdge);
            }
        }

        // splits a triangle!
        public void SplitTriangle(Edge3D edgeToSplit)
        {
            // edge to split goes from LEFT to RIGHT
            Node3D topNode = edgeToSplit.next.target; // (assuming standard CCW winding)
            Node3D bottomNode = edgeToSplit.twin.next.target;
            Node3D rightNode = edgeToSplit.target;
            Node3D leftNode = edgeToSplit.origin;

            // create midpoint node
            Vector3 midPoint = (rightNode.position + leftNode.position) / 2.0f;
            Node3D newNode = AddNode(midPoint);
            newNode.currVelocity = (rightNode.currVelocity + leftNode.currVelocity) / 2.0f;

            // 3. Keep the outer boundary edges, but save references to them
            Edge3D eTopLeft = edgeToSplit.prev;
            Edge3D eRightTop = edgeToSplit.next;
            Edge3D eBottomRight = edgeToSplit.twin.prev;
            Edge3D eLeftBottom = edgeToSplit.twin.next;

            // 4. We reuse the 2 existing faces and the 2 existing half-edges (the middle ones)
            Face3D topFace = edgeToSplit.face;
            Face3D bottomFace = edgeToSplit.twin.face;
            Edge3D eLeftMid = edgeToSplit; // Re-purpose to go from leftNode -> mid
            Edge3D eMidLeft = edgeToSplit.twin; // Re-purpose to go from leftNode -> mid

            // 5. Create the 6 brand NEW half-edges and 2 NEW faces required
            Face3D newTopFace = new Face3D();   // a new face on top right
            Face3D newBottomFace = new Face3D();    // a new face on bottom right
            faces.Add(newTopFace);
            faces.Add(newBottomFace);

            Edge3D eMidTop = new Edge3D();
            Edge3D eTopMid = new Edge3D();
            Edge3D eMidBottom = new Edge3D();
            Edge3D eBottomMid = new Edge3D();
            Edge3D eMidRight = new Edge3D();
            Edge3D eRightMid = new Edge3D();

            // Add these 6 to your halfEdges list...
            halfEdges.Add(eMidTop);
            halfEdges.Add(eTopMid);
            halfEdges.Add(eMidBottom);
            halfEdges.Add(eBottomMid);
            halfEdges.Add(eRightMid);
            halfEdges.Add(eLeftMid);

            // --- THE STITCHING PHASE ---
            // You now systematically assign the .next, .prev, .twin, .origin, and .face 
            // for the 4 triangles radiating from newNode.

            // Example of stitching the Top-Right triangle:
            eRightTop.face = newTopFace; // Outer boundary edge belongs to the new face now
            eRightTop.next = eTopMid;
            eRightTop.prev = eMidRight;

            eMidRight.origin = newNode;
            eMidRight.twin = eRightMid;
            eMidRight.next = eRightTop;
            eMidRight.prev = eTopMid;
            eMidRight.face = newTopFace;

            eTopMid.origin = topNode;
            eTopMid.twin = eMidTop;
            eTopMid.next = eMidRight;
            eTopMid.prev = eRightTop;
            eTopMid.face = newTopFace;

            // Bottom right Triangle:
            eBottomRight.face = newBottomFace; // Outer boundary edge belongs to the new face now
            eBottomRight.next = eRightMid;
            eBottomRight.prev = eMidBottom;

            eMidBottom.origin = newNode;
            eMidBottom.twin = eBottomMid;
            eMidBottom.next = eBottomRight;
            eMidBottom.prev = eRightMid;
            eMidBottom.face = newBottomFace;

            eRightMid.origin = rightNode;
            eRightMid.twin = eMidRight;
            eRightMid.next = eMidBottom;
            eRightMid.prev = eBottomRight;
            eRightMid.face = newBottomFace;

            // top left triangle
            eMidTop.origin = newNode;
            eMidTop.twin = eTopMid;
            eMidTop.next = eTopLeft;
            eMidTop.prev = eLeftMid;
            eMidTop.face = topFace;

            eTopLeft.face = topFace; // Outer boundary edge
            eTopLeft.next = eLeftMid;
            eTopLeft.prev = eMidTop;

            eLeftMid.origin = leftNode;
            eLeftMid.twin = eMidLeft;
            eLeftMid.next = eMidTop;
            eLeftMid.prev = eTopLeft;
            eLeftMid.face = topFace;

            // bottom left triangle
            eBottomMid.origin = bottomNode;
            eBottomMid.twin = eMidBottom;
            eBottomMid.next = eMidLeft;
            eBottomMid.prev = eLeftBottom;
            eBottomMid.face = bottomFace;

            eMidLeft.origin = newNode;
            eMidLeft.twin = eLeftMid;
            eMidLeft.next = eLeftBottom;
            eMidLeft.prev = eBottomMid;
            eMidLeft.face = bottomFace;

            eLeftBottom.face = bottomFace; // Outer boundary edge
            eLeftBottom.next = eBottomMid;
            eLeftBottom.prev = eMidLeft;

            // set face pointers
            topFace.halfEdge = eLeftMid;
            bottomFace.halfEdge = eMidLeft;
            newTopFace.halfEdge = eMidRight;
            newBottomFace.halfEdge = eRightMid;

            // set node pointers
            newNode.halfEdge = eMidRight;
            topNode.halfEdge = eTopLeft;
            leftNode.halfEdge = eLeftBottom;
            bottomNode.halfEdge = eBottomRight;
            rightNode.halfEdge = eRightTop;
        }
        #endregion
    }
}

