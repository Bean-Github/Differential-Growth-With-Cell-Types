using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Growth3DCompute
{

    public class NodeHoardGenerator
    {
        public NodeHoardCompute nodeHoard = new NodeHoardCompute();

        public void CreateTestSphere(float startRadius, int subdivisions = 0)
        {
            // Adjust this for detail. 
            // 0 = tabletop die (12 vertices)
            // 2 = low poly sphere (162 vertices)
            // 3 = smooth sphere (642 vertices)

            List<Vector3> vertices = new List<Vector3>();
            List<int[]> faces = new List<int[]>();

            // We use a cache to avoid creating duplicate vertices when two triangles share the same edge
            Dictionary<long, int> midpointCache = new Dictionary<long, int>();

            // 1. Setup the Base Icosahedron (Your original code!)
            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
            vertices.Add(new Vector3(-1, t, 0).normalized * startRadius);
            vertices.Add(new Vector3(1, t, 0).normalized * startRadius);
            vertices.Add(new Vector3(-1, -t, 0).normalized * startRadius);
            vertices.Add(new Vector3(1, -t, 0).normalized * startRadius);
            vertices.Add(new Vector3(0, -1, t).normalized * startRadius);
            vertices.Add(new Vector3(0, 1, t).normalized * startRadius);
            vertices.Add(new Vector3(0, -1, -t).normalized * startRadius);
            vertices.Add(new Vector3(0, 1, -t).normalized * startRadius);
            vertices.Add(new Vector3(t, 0, -1).normalized * startRadius);
            vertices.Add(new Vector3(t, 0, 1).normalized * startRadius);
            vertices.Add(new Vector3(-t, 0, -1).normalized * startRadius);
            vertices.Add(new Vector3(-t, 0, 1).normalized * startRadius);

            faces.AddRange(new int[][] {
                    new int[]{0, 11, 5}, new int[]{0, 5, 1},  new int[]{0, 1, 7},   new int[]{0, 7, 10},  new int[]{0, 10, 11},
                    new int[]{1, 5, 9},  new int[]{5, 11, 4}, new int[]{11, 10, 2}, new int[]{10, 7, 6},  new int[]{7, 1, 8},
                    new int[]{3, 9, 4},  new int[]{3, 4, 2},  new int[]{3, 2, 6},   new int[]{3, 6, 8},   new int[]{3, 8, 9},
                    new int[]{4, 9, 5},  new int[]{2, 4, 11}, new int[]{6, 2, 10},  new int[]{8, 6, 7},   new int[]{9, 8, 1}
                });

            // Local Helper to get the midpoint of an edge, and push it out to the sphere's surface
            int GetMidpoint(int p1, int p2)
            {
                // Create a unique key for the edge so we don't calculate it twice
                long min = Mathf.Min(p1, p2);
                long max = Mathf.Max(p1, p2);
                long key = (min << 32) + max; // Bitwise shift to combine two ints into a long

                if (midpointCache.TryGetValue(key, out int index))
                {
                    return index; // We already split this edge! Return the existing midpoint.
                }

                // Calculate the new vertex position
                Vector3 middle = ((vertices[p1] + vertices[p2]) / 2f).normalized * startRadius;

                vertices.Add(middle);
                int newIndex = vertices.Count - 1;
                midpointCache.Add(key, newIndex);

                return newIndex;
            }

            // 2. Subdivide the faces
            for (int i = 0; i < subdivisions; i++)
            {
                List<int[]> nextFaces = new List<int[]>();
                foreach (int[] face in faces)
                {
                    // Replace the triangle with 4 smaller triangles
                    int a = GetMidpoint(face[0], face[1]);
                    int b = GetMidpoint(face[1], face[2]);
                    int c = GetMidpoint(face[2], face[0]);

                    nextFaces.Add(new int[] { face[0], a, c });
                    nextFaces.Add(new int[] { face[1], b, a });
                    nextFaces.Add(new int[] { face[2], c, b });
                    nextFaces.Add(new int[] { a, b, c }); // The center triangle
                }
                faces = nextFaces;
            }

            // Add to NodeHoard securely
            List<uint> finalNodes = new List<uint>();
            foreach (Vector3 vert in vertices)
            {
                finalNodes.Add(nodeHoard.AddNode(vert));
            }

            foreach (int[] face in faces)
            {
                uint a = finalNodes[face[0]];
                uint b = finalNodes[face[1]];
                uint c = finalNodes[face[2]];

                nodeHoard.AddTriangle(a, b, c);
            }

        }

    }



    // THE MASTER STORER OF NODES AND EDGES
    // stores all nodes and edges, and provides methods for adding new nodes and connecting them to neighbors
    public class NodeHoardCompute
    {
        // storage
        public List<Node3D> allNodes;
        public List<HalfEdge3D> halfEdges;
        public List<Face3D> faces;
        private Dictionary<long, HalfEdge3D> edgeSutures;

        float globalMass = 1.0f;

        public NodeHoardCompute()
        {
            allNodes = new List<Node3D>();
            halfEdges = new List<HalfEdge3D>();
            faces = new List<Face3D>();
            edgeSutures = new Dictionary<long, HalfEdge3D>();
        }

        public NodeHoardCompute(Node3D[] nodes, HalfEdge3D[] edges, Face3D[] faces)
        {
            allNodes = nodes.ToList();
            halfEdges = edges.ToList();
            this.faces = faces.ToList();
            edgeSutures = new Dictionary<long, HalfEdge3D>();
        }

        #region Getters
        public Node3D GetNode(uint id)
        {
            return allNodes[(int)id];
        }

        public HalfEdge3D GetEdge(uint id)
        {
            return halfEdges[(int)id];
        }

        public Face3D GetFace(uint id)
        {
            return faces[(int)id];
        }

        public uint AddNode(Vector3 position)
        { 
            Node3D newNode = new Node3D { position = position, mass = globalMass };
            return AddNode(ref newNode);
        }

        uint AddNode(ref Node3D node)
        {
            node.id = (uint)allNodes.Count;
            allNodes.Add(node);
            return node.id;
        }
        uint AddEdge(ref HalfEdge3D edge)
        {
            edge.id = (uint)halfEdges.Count;
            halfEdges.Add(edge);
            return edge.id;
        }

        uint AddFace(ref Face3D face)
        {
            face.id = (uint)faces.Count;
            faces.Add(face);
            return face.id;
        }
        #endregion

        #region Edge Management
        // currently unused
        //public HalfEdge3D AddEdge(Node3D from, Node3D to)
        //{
        //    HalfEdge3D he = new HalfEdge3D();
        //    halfEdges.Add(he);
        //    he.id = (uint)halfEdges.Count - 1;

        //    he.origin = from.id;
        //    from.halfEdge = he.id; // set from's half edge to the new half edge
        //    SutureTwin(he, from, to);
        //    return he;
        //}

        public Face3D AddTriangle(uint a, uint b, uint c)
        {
            Node3D nodeA = GetNode(a);
            Node3D nodeB = GetNode(b);
            Node3D nodeC = GetNode(c);

            Face3D face = new Face3D();
            faces.Add(face);
            face.id = (uint)faces.Count - 1;

            HalfEdge3D he1 = new HalfEdge3D { origin = nodeA.id, face = face.id };
            HalfEdge3D he2 = new HalfEdge3D { origin = nodeB.id, face = face.id };
            HalfEdge3D he3 = new HalfEdge3D { origin = nodeC.id, face = face.id };

            AddEdge(ref he1);
            AddEdge(ref he2);
            AddEdge(ref he3);

            he1.next = he2.id; he1.prev = he3.id; he1.target = b;
            he2.next = he3.id; he2.prev = he1.id; he2.target = c;
            he3.next = he1.id; he3.prev = he2.id; he3.target = a;

            SutureTwin(ref he1, ref nodeA, ref nodeB);
            SutureTwin(ref he2, ref nodeB, ref nodeC);
            SutureTwin(ref he3, ref nodeC, ref nodeA);

            face.halfEdge = he1.id;
            nodeA.halfEdge = he1.id; // set its halfedge to he1
            nodeB.halfEdge = he2.id;
            nodeC.halfEdge = he3.id;

            // Write the modifications back to the master list!
            halfEdges[(int)he1.id] = he1;
            halfEdges[(int)he2.id] = he2;
            halfEdges[(int)he3.id] = he3;

            allNodes[(int)nodeA.id] = nodeA;
            allNodes[(int)nodeB.id] = nodeB;
            allNodes[(int)nodeC.id] = nodeC;

            faces[(int)face.id] = face;

            return face;
        }

        // this function checks if there is already a half-edge going in the opposite direction (to -> from).
        // If so, it stitches them together as twins.
        // If not, it stores this half-edge in the edgeSutures dictionary to potentially be stitched
        // later when we encounter the opposite direction.
        private void SutureTwin(ref HalfEdge3D halfEdge, ref Node3D from, ref Node3D to)
        {
            long min = System.Math.Min(from.id, to.id);
            long max = System.Math.Max(from.id, to.id);
            long key = (min << 32) | max;

            // if there exists and edge going to -> from, then that is our twin!
            if (edgeSutures.TryGetValue(key, out HalfEdge3D twin))
            {
                halfEdge.twin = twin.id;
                twin.twin = halfEdge.id;

                halfEdges[(int)twin.id] = twin;

                edgeSutures.Remove(key);
            }
            else
            {
                halfEdge.twin = uint.MaxValue; // No twin yet
                edgeSutures.Add(key, halfEdge);
            }
        }

        // splits a triangle!
        public void SplitTriangle(ref HalfEdge3D edgeToSplit)
        {
            // edge to split goes from LEFT to RIGHT
            Node3D topNode = GetNode(GetEdge(edgeToSplit.next).target); // (assuming standard CCW winding)
            Node3D bottomNode = GetNode(GetEdge(GetEdge(edgeToSplit.twin).next).target);
            Node3D rightNode = GetNode(edgeToSplit.target);
            Node3D leftNode = GetNode(edgeToSplit.origin);

            // create midpoint node
            Vector3 midPoint = (rightNode.position + leftNode.position) / 2.0f;
            Node3D newNode = new Node3D { position = midPoint, mass = globalMass };
            newNode.velocity = (rightNode.velocity + leftNode.velocity) / 2.0f;

            // 3. Keep the outer boundary edges, but save references to them
            HalfEdge3D eTopLeft = GetEdge(edgeToSplit.prev);
            HalfEdge3D eRightTop = GetEdge(edgeToSplit.next);
            HalfEdge3D eBottomRight = GetEdge(GetEdge(edgeToSplit.twin).prev);
            HalfEdge3D eLeftBottom = GetEdge(GetEdge(edgeToSplit.twin).next);

            // 4. We reuse the 2 existing faces and the 2 existing half-edges (the middle ones)
            Face3D topFace = GetFace(edgeToSplit.face);
            Face3D bottomFace = GetFace(GetEdge(edgeToSplit.twin).face);
            HalfEdge3D eLeftMid = edgeToSplit; // Re-purpose to go from leftNode -> mid
            HalfEdge3D eMidLeft = GetEdge(edgeToSplit.twin); // Re-purpose to go from leftNode -> mid

            // 5. Create the 6 brand NEW half-edges and 2 NEW faces required
            Face3D newTopFace = new Face3D();   // a new face on top right
            Face3D newBottomFace = new Face3D();    // a new face on bottom right

            HalfEdge3D eMidTop = new HalfEdge3D();
            HalfEdge3D eTopMid = new HalfEdge3D();
            HalfEdge3D eMidBottom = new HalfEdge3D();
            HalfEdge3D eBottomMid = new HalfEdge3D();
            HalfEdge3D eMidRight = new HalfEdge3D();
            HalfEdge3D eRightMid = new HalfEdge3D();

            // add all newly created things to the master lists to generate their IDs!
            AddNode(ref newNode);

            AddFace(ref newTopFace);
            AddFace(ref newBottomFace);

            AddEdge(ref eMidTop);
            AddEdge(ref eTopMid);
            AddEdge(ref eMidBottom);
            AddEdge(ref eBottomMid);
            AddEdge(ref eRightMid);
            AddEdge(ref eMidRight);

            // --- THE STITCHING PHASE ---
            // You now systematically assign the .next, .prev, .twin, .origin, and .face 
            // for the 4 triangles radiating from newNode.

            // Example of stitching the Top-Right triangle:
            eRightTop.face = newTopFace.id; // Outer boundary edge belongs to the new face now
            eRightTop.next = eTopMid.id;
            eRightTop.prev = eMidRight.id;

            eMidRight.origin = newNode.id;
            eMidRight.target = rightNode.id;
            eMidRight.twin = eRightMid.id;
            eMidRight.next = eRightTop.id;
            eMidRight.prev = eTopMid.id;
            eMidRight.face = newTopFace.id;

            eTopMid.origin = topNode.id;
            eTopMid.target = newNode.id;
            eTopMid.twin = eMidTop.id;
            eTopMid.next = eMidRight.id;
            eTopMid.prev = eRightTop.id;
            eTopMid.face = newTopFace.id;

            // Bottom right Triangle:
            eBottomRight.face = newBottomFace.id; // Outer boundary edge belongs to the new face now
            eBottomRight.next = eRightMid.id;
            eBottomRight.prev = eMidBottom.id;

            eMidBottom.origin = newNode.id;
            eMidBottom.target = bottomNode.id;
            eMidBottom.twin = eBottomMid.id;
            eMidBottom.next = eBottomRight.id;
            eMidBottom.prev = eRightMid.id;
            eMidBottom.face = newBottomFace.id;

            eRightMid.origin = rightNode.id;
            eRightMid.target = newNode.id;
            eRightMid.twin = eMidRight.id;
            eRightMid.next = eMidBottom.id;
            eRightMid.prev = eBottomRight.id;
            eRightMid.face = newBottomFace.id;

            // top left triangle
            eMidTop.origin = newNode.id;
            eMidTop.target = topNode.id;
            eMidTop.twin = eTopMid.id;
            eMidTop.next = eTopLeft.id;
            eMidTop.prev = eLeftMid.id;
            eMidTop.face = topFace.id;

            eTopLeft.face = topFace.id; // Outer boundary edge
            eTopLeft.next = eLeftMid.id;
            eTopLeft.prev = eMidTop.id;

            eLeftMid.origin = leftNode.id;
            eLeftMid.target = newNode.id;
            eLeftMid.twin = eMidLeft.id;
            eLeftMid.next = eMidTop.id;
            eLeftMid.prev = eTopLeft.id;
            eLeftMid.face = topFace.id;

            // bottom left triangle
            eBottomMid.origin = bottomNode.id;
            eBottomMid.target = newNode.id;
            eBottomMid.twin = eMidBottom.id;
            eBottomMid.next = eMidLeft.id;
            eBottomMid.prev = eLeftBottom.id;
            eBottomMid.face = bottomFace.id;

            eMidLeft.origin = newNode.id;
            eMidLeft.target = leftNode.id;
            eMidLeft.twin = eLeftMid.id;
            eMidLeft.next = eLeftBottom.id;
            eMidLeft.prev = eBottomMid.id;
            eMidLeft.face = bottomFace.id;

            eLeftBottom.face = bottomFace.id; // Outer boundary edge
            eLeftBottom.next = eBottomMid.id;
            eLeftBottom.prev = eMidLeft.id;

            // set face pointers
            topFace.halfEdge = eLeftMid.id;
            bottomFace.halfEdge = eMidLeft.id;
            newTopFace.halfEdge = eMidRight.id;
            newBottomFace.halfEdge = eRightMid.id;

            // set node pointers
            newNode.halfEdge = eMidRight.id;
            topNode.halfEdge = eTopLeft.id;
            leftNode.halfEdge = eLeftBottom.id;
            bottomNode.halfEdge = eBottomRight.id;
            rightNode.halfEdge = eRightTop.id;

            // Write back modified existing Edges AND the new ones we just modified
            halfEdges[(int)eTopLeft.id] = eTopLeft;
            halfEdges[(int)eRightTop.id] = eRightTop;
            halfEdges[(int)eBottomRight.id] = eBottomRight;
            halfEdges[(int)eLeftBottom.id] = eLeftBottom;
            halfEdges[(int)eLeftMid.id] = eLeftMid;
            halfEdges[(int)eMidLeft.id] = eMidLeft;

            // Write back new edges
            halfEdges[(int)eMidTop.id] = eMidTop;
            halfEdges[(int)eTopMid.id] = eTopMid;
            halfEdges[(int)eMidBottom.id] = eMidBottom;
            halfEdges[(int)eBottomMid.id] = eBottomMid;
            halfEdges[(int)eRightMid.id] = eRightMid;
            halfEdges[(int)eMidRight.id] = eMidRight;

            // Write back Faces
            faces[(int)topFace.id] = topFace;
            faces[(int)bottomFace.id] = bottomFace;
            faces[(int)newTopFace.id] = newTopFace;
            faces[(int)newBottomFace.id] = newBottomFace;

            // Write back Nodes
            allNodes[(int)topNode.id] = topNode;
            allNodes[(int)bottomNode.id] = bottomNode;
            allNodes[(int)leftNode.id] = leftNode;
            allNodes[(int)rightNode.id] = rightNode;
            allNodes[(int)newNode.id] = newNode;

        }
        #endregion
    }


}
